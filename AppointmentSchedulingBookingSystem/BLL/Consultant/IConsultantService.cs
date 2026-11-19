using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.BLL.Common;
namespace AppointmentSchedulingBookingSystem.BLL.Consultants;
public interface IConsultantService
{
    Task<ConsultantWeekViewModel> GetWeekAsync(string userId);
    Task<ConsultantScheduleViewModel?> GetScheduleAsync(string userId);
    Task<OperationResult> SaveScheduleAsync(string userId, ConsultantScheduleViewModel model);
    Task<ConsultantBreakListViewModel?> GetBreaksAsync(string userId);
    Task<OperationResult> AddBreakAsync(string userId, ConsultantBreakViewModel model);
    Task<OperationResult> DeleteBreakAsync(string userId, int id);
}
