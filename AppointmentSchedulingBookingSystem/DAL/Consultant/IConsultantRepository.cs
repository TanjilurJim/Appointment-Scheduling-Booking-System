using AppointmentSchedulingBookingSystem.Models;
namespace AppointmentSchedulingBookingSystem.DAL.Consultants;

public interface IConsultantRepository
{
    Task<List<ConsultantBreak>> GetWeekBreaksAsync(int consultantId, DateOnly weekStart, DateOnly weekEnd);
    Task<List<ConsultantAvailability>> ReadAvailabilityAsync(int consultantId);
    Task<List<ConsultantAvailability>> GetAvailabilityAsync(int consultantId);
    Task<List<ConsultantBreak>> GetRecurringBreaksAsync(int consultantId, List<DayOfWeek> removedDayNames);
    Task<List<ConsultantBreak>> GetBreaksAsync(int consultantId);
    Task<ConsultantAvailability?> FindAvailabilityAsync(int consultantId, DayOfWeek targetDay);
    Task<ConsultantBreak?> FindBreakAsync(int consultantId, int id);
    Task<Consultant?> FindConsultantAsync(string userId);
    void RemoveBreaks(IEnumerable<ConsultantBreak> breaks);
    void RemoveAvailability(IEnumerable<ConsultantAvailability> days);
    void AddAvailability(ConsultantAvailability day);
    void AddBreak(ConsultantBreak item);
    void RemoveBreak(ConsultantBreak item);
    Task SaveAsync();
}
