using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.BLL.Common;
using AppointmentSchedulingBookingSystem.DAL.Customers;
namespace AppointmentSchedulingBookingSystem.BLL.Customers;

public class CustomerService(ICustomerRepository repository, TimeProvider clock) : ICustomerService
{
    private static readonly TimeZoneInfo BookingZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
    private DateTime LocalNow => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BookingZone).DateTime;

    public async Task<CustomerBookingViewModel> GetBookingPageAsync(string userId, DateOnly? date)
    {
        var now = LocalNow;
        var today = DateOnly.FromDateTime(now);
        var selected = date ?? today;
        var customer = await repository.FindCustomerAsync(userId);
        var schedules = await repository.GetSchedulesAsync(selected);
        var ownAppointments = customer == null ? new List<Appointment>()
            : await repository.GetCustomerAppointmentsAsync(customer.Id, selected, selected);
        var model = new CustomerBookingViewModel
        {
            Date = selected, Today = today, HasCustomerProfile = customer != null,
            Appointments = ownAppointments.Select(a => new CustomerAppointmentViewModel
            {
                Id = a.Id, ConsultantName = a.Consultant.Name, Date = a.Date,
                StartTime = a.StartTime, EndTime = a.EndTime, Status = a.Status.ToString()
            }).ToList()
        };
        foreach (var consultant in schedules)
        {
            model.Consultants.Add(new ConsultantBookingViewModel
            {
                Id = consultant.Id, Name = consultant.Name, Specialization = consultant.Specialization,
                SlotDurationMinutes = consultant.SlotDurationMinutes,
                WorkingDays = consultant.Availabilities.OrderBy(a => ((int)a.DayOfWeek + 6) % 7)
                    .Select(a => new WorkingDayOptionViewModel
                    {
                        DayOfWeek = a.DayOfWeek, StartTime = a.StartTime, EndTime = a.EndTime
                    }).ToList(),
                Slots = BuildSlots(consultant, selected, ownAppointments, now)
            });
        }
        return model;
    }

    public async Task<OperationResult> BookAsync(string userId, BookAppointmentViewModel request)
    {
        if (request.ConsultantId <= 0 || !request.Date.HasValue || !request.StartTime.HasValue)
            return OperationResult.Fail("Choose a consultant, date and available slot.");
        var customer = await repository.FindCustomerAsync(userId);
        if (customer == null)
            return OperationResult.Fail("Your customer profile needs to be completed by an administrator before booking.");
        try
        {
            await using var transaction = await repository.BeginBookingAsync();
            // Re-read current hours, duration, breaks and appointments inside the transaction.
            var schedules = await repository.GetSchedulesAsync(request.Date.Value, request.ConsultantId);
            var consultant = schedules.SingleOrDefault();
            if (consultant == null) return OperationResult.Fail("This consultant is no longer available.");
            var ownAppointments = await repository.GetCustomerAppointmentsAsync(customer.Id, request.Date.Value, request.Date.Value);
            var slot = BuildSlots(consultant, request.Date.Value, ownAppointments, LocalNow)
                .SingleOrDefault(s => s.StartTime == request.StartTime.Value);
            if (slot == null || !slot.IsAvailable)
                return OperationResult.Fail("This slot is unavailable. Please choose another available slot.");

            await repository.AddAppointmentAsync(new Appointment
            {
                CustomerId = customer.Id, ConsultantId = consultant.Id,
                Date = request.Date.Value, StartTime = slot.StartTime, EndTime = slot.EndTime,
                Status = AppointmentStatus.Confirmed
            });
            await transaction.CommitAsync();
            return OperationResult.Success($"Appointment confirmed with {consultant.Name} on {request.Date.Value:dd MMM yyyy}, {slot.StartTime:HH:mm} - {slot.EndTime:HH:mm} (Bangladesh time).");
        }
        catch (BookingConflictException)
        {
            return OperationResult.Fail("Availability changed while you were booking. Please check the refreshed slots and try again.");
        }
    }

    private static List<BookingSlotViewModel> BuildSlots(Consultant consultant, DateOnly date,
        IEnumerable<Appointment> ownAppointments, DateTime now)
    {
        var slots = new List<BookingSlotViewModel>();
        var hours = consultant.Availabilities.FirstOrDefault(a => a.DayOfWeek == date.DayOfWeek);
        if (!consultant.IsActive || hours == null || consultant.SlotDurationMinutes is < 1 or > 1440)
            return slots;
        var duration = TimeSpan.FromMinutes(consultant.SlotDurationMinutes);
        var endOfWork = hours.EndTime.ToTimeSpan();
        // TimeSpan arithmetic avoids TimeOnly wrapping around midnight.
        for (var start = hours.StartTime.ToTimeSpan(); start + duration <= endOfWork; start += duration)
        {
            var slot = new BookingSlotViewModel
            {
                StartTime = TimeOnly.FromTimeSpan(start), EndTime = TimeOnly.FromTimeSpan(start + duration)
            };
            if (date.ToDateTime(slot.StartTime) <= now)
                slot.UnavailableReason = "Past time";
            else if (consultant.Breaks.Any(b => (b.DayOfWeek == date.DayOfWeek || b.Date == date) &&
                Overlaps(slot, b.StartTime, b.EndTime)))
                slot.UnavailableReason = "Break";
            else if (consultant.Appointments.Any(a => a.Date == date && a.Status != AppointmentStatus.Cancelled &&
                Overlaps(slot, a.StartTime, a.EndTime)))
                slot.UnavailableReason = "Booked";
            else if (ownAppointments.Any(a => a.Date == date && a.Status != AppointmentStatus.Cancelled &&
                Overlaps(slot, a.StartTime, a.EndTime)))
                slot.UnavailableReason = "Conflicts with your appointment";
            slots.Add(slot);
        }
        return slots;
    }

    private static bool Overlaps(BookingSlotViewModel slot, TimeOnly start, TimeOnly end) =>
        slot.StartTime < end && slot.EndTime > start;
}
