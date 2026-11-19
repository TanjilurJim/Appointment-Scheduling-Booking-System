using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.DAL.Common;
namespace AppointmentSchedulingBookingSystem.DAL.Customers;

public interface ICustomerRepository
{
    Task<Customer?> FindCustomerAsync(string userId);
    Task<List<Consultant>> GetSchedulesAsync(DateOnly date, int? consultantId = null);
    Task<List<Appointment>> GetCustomerAppointmentsAsync(int customerId, DateOnly from, DateOnly to);
    Task<IDataTransaction> BeginBookingAsync();
    Task AddAppointmentAsync(Appointment appointment);
}

// A competing booking or schedule change requires the customer to refresh availability.
public class BookingConflictException(Exception inner) : Exception("Availability changed. Please try again.", inner);
