using System.ComponentModel.DataAnnotations;
namespace AppointmentSchedulingBookingSystem.ViewModels;

public class CustomerBookingViewModel
{
    public DateOnly Date { get; set; }
    public DateOnly Today { get; set; }
    public bool HasCustomerProfile { get; set; }
    public List<ConsultantBookingViewModel> Consultants { get; set; } = new();
    public List<CustomerAppointmentViewModel> Appointments { get; set; } = new();
}

public class ConsultantBookingViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Specialization { get; set; } = "";
    public int SlotDurationMinutes { get; set; }
    public List<WorkingDayOptionViewModel> WorkingDays { get; set; } = new();
    public List<BookingSlotViewModel> Slots { get; set; } = new();
}

public class BookingSlotViewModel
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? UnavailableReason { get; set; }
    public bool IsAvailable => UnavailableReason == null;
}

public class BookAppointmentViewModel
{
    [Range(1, int.MaxValue)]
    public int ConsultantId { get; set; }
    [Required]
    public DateOnly? Date { get; set; }
    [Required]
    public TimeOnly? StartTime { get; set; }
}

public class CustomerAppointmentViewModel
{
    public int Id { get; set; }
    public string ConsultantName { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "";
}
