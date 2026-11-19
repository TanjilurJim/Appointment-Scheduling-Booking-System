using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.BLL.Common;
namespace AppointmentSchedulingBookingSystem.BLL.Admin;
public interface IAdminService
{
    Task<List<AdminUserViewModel>> GetUsersAsync();
    Task<OperationResult> UpdateUserAsync(UpdateUserViewModel model, IReadOnlySet<string> suppliedFields);
}
