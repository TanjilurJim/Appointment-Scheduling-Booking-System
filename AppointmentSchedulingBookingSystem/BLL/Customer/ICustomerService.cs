using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.BLL.Common;
namespace AppointmentSchedulingBookingSystem.BLL.Customers;

public interface ICustomerService
{
    Task<CustomerBookingViewModel> GetBookingPageAsync(string userId, DateOnly? date);
    Task<OperationResult> BookAsync(string userId, BookAppointmentViewModel request);
}
