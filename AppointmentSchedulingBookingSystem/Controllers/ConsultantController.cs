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

    public async Task<IActionResult> Index()
    {
        // Dashboard calendar uses Bangladesh time, independent of server timezone.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka")));
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var model = new ConsultantWeekViewModel { Today = today, WeekStart = weekStart };
        var consultant = await GetCurrentConsultantAsync();
        if (consultant == null)
            return View(model);

        model.HasProfile = true;
        model.SlotDurationMinutes = consultant.SlotDurationMinutes;
        var workingDays = await _context.ConsultantAvailabilities.AsNoTracking()
            .Where(a => a.ConsultantId == consultant.Id).ToListAsync();
        var weekEnd = weekStart.AddDays(6);
        var breaks = await _context.ConsultantBreaks.AsNoTracking()
            .Where(b => b.ConsultantId == consultant.Id &&
                (b.DayOfWeek != null || (b.Date >= weekStart && b.Date <= weekEnd)))
            .OrderBy(b => b.StartTime).ToListAsync();

        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var hours = workingDays.FirstOrDefault(a => a.DayOfWeek == date.DayOfWeek);
            model.Days.Add(new ConsultantWeekDayViewModel
            {
                Date = date,
                StartTime = hours?.StartTime,
                EndTime = hours?.EndTime,
                Breaks = breaks.Where(b => b.DayOfWeek == date.DayOfWeek || b.Date == date)
                    .Select(b => new ConsultantBreakItemViewModel
                    {
                        Id = b.Id, DayOfWeek = b.DayOfWeek, Date = b.Date,
                        StartTime = b.StartTime, EndTime = b.EndTime
                    }).ToList()
            });
        }
        return View(model);
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
    [HttpGet]
    public async Task<IActionResult> Breaks()
    {
        var consultant = await GetCurrentConsultantAsync();

        if (consultant == null)
            return NotFound("Consultant profile not found.");

        var model = await BuildBreaksViewModelAsync(consultant.Id);

        return View(model);
    }

    private async Task<ConsultantBreakListViewModel>
        BuildBreaksViewModelAsync(int consultantId)
    {
        var breaks = await _context.ConsultantBreaks
            .AsNoTracking()
            .Where(b => b.ConsultantId == consultantId)
            .OrderBy(b => b.DayOfWeek)
            .ThenBy(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToListAsync();

        var workingDays = await _context.ConsultantAvailabilities
            .AsNoTracking()
            .Where(a => a.ConsultantId == consultantId)
            .OrderBy(a => a.DayOfWeek)
            .Select(a => new WorkingDayOptionViewModel
            {
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime
            })
            .ToListAsync();

        return new ConsultantBreakListViewModel
        {
            WorkingDays = workingDays,
            Breaks = breaks.Select(b => new ConsultantBreakItemViewModel
            {
                Id = b.Id,
                DayOfWeek = b.DayOfWeek,
                Date = b.Date,
                StartTime = b.StartTime,
                EndTime = b.EndTime
            }).ToList()
        };
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBreak(
    [Bind(Prefix = "NewBreak")] ConsultantBreakViewModel model)
    {
        var consultant = await GetCurrentConsultantAsync();

        if (consultant == null)
            return NotFound();

        // No selection means no new break; existing breaks stay unchanged.
        if (string.IsNullOrWhiteSpace(model.BreakType))
            return RedirectToAction(nameof(Breaks));

        if (model.BreakType != "Recurring" &&
            model.BreakType != "Specific")
        {
            ModelState.AddModelError(
                "", "Invalid break type.");
        }

        if (model.StartTime >= model.EndTime)
        {
            ModelState.AddModelError(
                "", "End time must be after start time.");
        }

        DayOfWeek targetDay = default;

        if (model.BreakType == "Recurring")
        {
            if (!model.DayOfWeek.HasValue ||
                !Enum.IsDefined(model.DayOfWeek.Value))
            {
                ModelState.AddModelError(
                    "", "Please select a valid weekday.");
            }
            else
            {
                targetDay = model.DayOfWeek.Value;
            }
        }
        else if (model.BreakType == "Specific")
        {
            if (!model.Date.HasValue)
            {
                ModelState.AddModelError(
                    "", "Please select a date.");
            }
            else
            {
                targetDay = model.Date.Value.DayOfWeek;
            }
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ",
                ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

            return RedirectToAction(nameof(Breaks));
        }

        // A break must be within the consultant's working hours.
        var availability = await _context.ConsultantAvailabilities
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.ConsultantId == consultant.Id &&
                a.DayOfWeek == targetDay);

        if (availability == null)
        {
            TempData["Error"] =
                "You are not scheduled to work on this day.";

            return RedirectToAction(nameof(Breaks));
        }

        if (model.StartTime < availability.StartTime ||
            model.EndTime > availability.EndTime)
        {
            TempData["Error"] =
                "Break must be within your working hours.";

            return RedirectToAction(nameof(Breaks));
        }

        // Fetch existing breaks for this consultant.
        var existingBreaks = await _context.ConsultantBreaks
            .AsNoTracking()
            .Where(b => b.ConsultantId == consultant.Id)
            .ToListAsync();

        var hasOverlap = existingBreaks.Any(b =>
        {
            bool appliesToSameDay;

            if (model.BreakType == "Recurring")
            {
                // Check other recurring breaks AND dated breaks
                // on the same weekday.
                appliesToSameDay =
                    b.DayOfWeek == targetDay ||
                    (b.Date.HasValue &&
                     b.Date.Value.DayOfWeek == targetDay);
            }
            else
            {
                // A specific date can overlap a recurring break
                // for its weekday or another break on that date.
                appliesToSameDay =
                    b.DayOfWeek == targetDay ||
                    b.Date == model.Date;
            }

            return appliesToSameDay &&
                model.StartTime < b.EndTime &&
                model.EndTime > b.StartTime;
        });

        if (hasOverlap)
        {
            TempData["Error"] =
                "This break overlaps an existing break.";

            return RedirectToAction(nameof(Breaks));
        }

        var newBreak = new ConsultantBreak
        {
            ConsultantId = consultant.Id,
            DayOfWeek = model.BreakType == "Recurring"
                ? targetDay
                : null,
            Date = model.BreakType == "Specific"
                ? model.Date
                : null,
            StartTime = model.StartTime,
            EndTime = model.EndTime
        };

        _context.ConsultantBreaks.Add(newBreak);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Break added successfully.";

        return RedirectToAction(nameof(Breaks));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBreak(int id)
    {
        var consultant = await GetCurrentConsultantAsync();

        if (consultant == null)
            return NotFound();

        var existingBreak = await _context.ConsultantBreaks
            .FirstOrDefaultAsync(b =>
                b.Id == id &&
                b.ConsultantId == consultant.Id);

        if (existingBreak == null)
            return NotFound();

        _context.ConsultantBreaks.Remove(existingBreak);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Break deleted successfully.";

        return RedirectToAction(nameof(Breaks));
    }
}