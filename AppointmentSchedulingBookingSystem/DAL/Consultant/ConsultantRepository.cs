using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using Microsoft.EntityFrameworkCore;
namespace AppointmentSchedulingBookingSystem.DAL.Consultants;

public class ConsultantRepository(AppointmentSchedulingBookingSystemIdentityDbContext context) : IConsultantRepository
{
    private readonly AppointmentSchedulingBookingSystemIdentityDbContext _context = context;

    public Task<List<ConsultantBreak>> GetWeekBreaksAsync(int consultantId, DateOnly weekStart, DateOnly weekEnd) =>
        _context.ConsultantBreaks.AsNoTracking()
            .Where(b => b.ConsultantId == consultantId &&
                (b.DayOfWeek != null || (b.Date >= weekStart && b.Date <= weekEnd)))
            .OrderBy(b => b.StartTime).ToListAsync();

    public Task<List<ConsultantAvailability>> ReadAvailabilityAsync(int consultantId) =>
        _context.ConsultantAvailabilities
            .Where(a => a.ConsultantId == consultantId)
            .AsNoTracking()
            .ToListAsync();

    public Task<List<ConsultantAvailability>> GetAvailabilityAsync(int consultantId) =>
        _context.ConsultantAvailabilities
            .Where(a => a.ConsultantId == consultantId)
            .ToListAsync();

    public Task<List<ConsultantBreak>> GetRecurringBreaksAsync(int consultantId, List<DayOfWeek> removedDayNames) =>
        _context.ConsultantBreaks
            .Where(b =>
                b.ConsultantId == consultantId &&
                b.DayOfWeek.HasValue &&
                removedDayNames.Contains(b.DayOfWeek.Value))
            .ToListAsync();

    public Task<List<ConsultantBreak>> GetBreaksAsync(int consultantId) =>
        _context.ConsultantBreaks
            .AsNoTracking()
            .Where(b => b.ConsultantId == consultantId)
            .OrderBy(b => b.DayOfWeek)
            .ThenBy(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToListAsync();

    public Task<ConsultantAvailability?> FindAvailabilityAsync(int consultantId, DayOfWeek targetDay) =>
        _context.ConsultantAvailabilities
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.ConsultantId == consultantId &&
                a.DayOfWeek == targetDay);


    public Task<ConsultantBreak?> FindBreakAsync(int consultantId, int id) =>
        _context.ConsultantBreaks
            .FirstOrDefaultAsync(b =>
                b.Id == id &&
                b.ConsultantId == consultantId);

    public Task<Consultant?> FindConsultantAsync(string userId) => _context.Consultants.FirstOrDefaultAsync(c => c.UserId == userId);

    public void RemoveBreaks(IEnumerable<ConsultantBreak> breaks) => _context.ConsultantBreaks.RemoveRange(breaks);

    public void RemoveAvailability(IEnumerable<ConsultantAvailability> days) => _context.ConsultantAvailabilities.RemoveRange(days);

    public void AddAvailability(ConsultantAvailability day) => _context.ConsultantAvailabilities.Add(day);

    public void AddBreak(ConsultantBreak item) => _context.ConsultantBreaks.Add(item);

    public void RemoveBreak(ConsultantBreak item) => _context.ConsultantBreaks.Remove(item);

    public Task SaveAsync() => _context.SaveChangesAsync();
}
