using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSchedulingBookingSystem.Controllers;

[Authorize(Roles = "Consultant")]
public class ConsultantController : Controller
{
    private readonly AppointmentSchedulingBookingSystemIdentityDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ConsultantController(
        AppointmentSchedulingBookingSystemIdentityDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Schedule()
    {
        var consultant = await GetCurrentConsultantAsync();

        if (consultant == null)
            return NotFound("Consultant profile not found.");

        var availabilities = await _context.ConsultantAvailabilities
            .Where(a => a.ConsultantId == consultant.Id)
            .AsNoTracking()
            .ToListAsync();

        var model = new ConsultantScheduleViewModel
        {
            SlotDurationMinutes = consultant.SlotDurationMinutes,
            WorkingDays = Enum.GetValues<DayOfWeek>()
                .Select(day =>
                {
                    var availability = availabilities
                        .FirstOrDefault(a => a.DayOfWeek == day);

                    return new WorkingDayViewModel
                    {
                        DayOfWeek = day,
                        IsWorking = availability != null,
                        StartTime = availability?.StartTime ?? new TimeOnly(9, 0),
                        EndTime = availability?.EndTime ?? new TimeOnly(17, 0)
                    };
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(ConsultantScheduleViewModel model)
    {
        var consultant = await GetCurrentConsultantAsync();

        if (consultant == null)
            return NotFound();

        if (model.WorkingDays == null ||
            model.WorkingDays.Count != 7 ||
            model.WorkingDays.Select(d => d.DayOfWeek).Distinct().Count() != 7 ||
            model.WorkingDays.Any(d => !Enum.IsDefined(d.DayOfWeek)))
        {
            ModelState.AddModelError(
                "", "The schedule must contain each weekday exactly once.");
        }

        if (model.WorkingDays != null)
        {
            foreach (var day in model.WorkingDays.Where(d => d.IsWorking))
            {
                if (day.StartTime >= day.EndTime)
                {
                    ModelState.AddModelError(
                        "",
                        $"{day.DayOfWeek}: End time must be after start time.");
                }

                if (day.EndTime > day.StartTime &&
                    (day.EndTime - day.StartTime).TotalMinutes <
                    model.SlotDurationMinutes)
                {
                    ModelState.AddModelError(
                        "",
                        $"{day.DayOfWeek}: Working hours must fit at least one appointment.");
                }
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _context.ConsultantAvailabilities
            .Where(a => a.ConsultantId == consultant.Id)
            .ToListAsync();

        var workingDays = model.WorkingDays
            .Where(d => d.IsWorking)
            .ToList();

        var removedDays = existing
            .Where(a => workingDays.All(d => d.DayOfWeek != a.DayOfWeek))
            .ToList();

        // Preserve recurring breaks on active days.
        // Remove recurring breaks when their working day is removed.
        var removedDayNames = removedDays.Select(a => a.DayOfWeek).ToList();

        var obsoleteBreaks = await _context.ConsultantBreaks
            .Where(b =>
                b.ConsultantId == consultant.Id &&
                b.DayOfWeek.HasValue &&
                removedDayNames.Contains(b.DayOfWeek.Value))
            .ToListAsync();

        _context.ConsultantBreaks.RemoveRange(obsoleteBreaks);
        _context.ConsultantAvailabilities.RemoveRange(removedDays);

        foreach (var day in workingDays)
        {
            var availability = existing
                .FirstOrDefault(a => a.DayOfWeek == day.DayOfWeek);

            if (availability == null)
            {
                _context.ConsultantAvailabilities.Add(
                    new ConsultantAvailability
                    {
                        ConsultantId = consultant.Id,
                        DayOfWeek = day.DayOfWeek,
                        StartTime = day.StartTime,
                        EndTime = day.EndTime
                    });
            }
            else
            {
                availability.StartTime = day.StartTime;
                availability.EndTime = day.EndTime;
            }
        }

        consultant.SlotDurationMinutes = model.SlotDurationMinutes;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Your working schedule has been updated.";

        return RedirectToAction(nameof(Schedule));
    }

    private async Task<Consultant?> GetCurrentConsultantAsync()
    {
        var userId = _userManager.GetUserId(User);

        return await _context.Consultants
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }
}