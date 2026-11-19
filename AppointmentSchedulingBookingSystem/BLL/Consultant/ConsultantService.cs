using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.DAL.Consultants;
using AppointmentSchedulingBookingSystem.BLL.Common;
namespace AppointmentSchedulingBookingSystem.BLL.Consultants;

public class ConsultantService(IConsultantRepository repository) : IConsultantService
{
    private readonly IConsultantRepository _repository = repository;

    public async Task<ConsultantWeekViewModel> GetWeekAsync(string userId)
    {
        // Dashboard calendar uses Bangladesh time, independent of server timezone.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka")));
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var model = new ConsultantWeekViewModel { Today = today, WeekStart = weekStart };
        var consultant = await _repository.FindConsultantAsync(userId);
        if (consultant == null)
            return model;

        model.HasProfile = true;
        model.SlotDurationMinutes = consultant.SlotDurationMinutes;
        var workingDays = await _repository.ReadAvailabilityAsync(consultant.Id);
        var weekEnd = weekStart.AddDays(6);
        var breaks = await _repository.GetWeekBreaksAsync(consultant.Id, weekStart, weekEnd);

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
        return model;
    }

    public async Task<ConsultantScheduleViewModel?> GetScheduleAsync(string userId)
    {
        var consultant = await _repository.FindConsultantAsync(userId);

        if (consultant == null)
            return null;

        var availabilities = await _repository.ReadAvailabilityAsync(consultant.Id);

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

        return model;
    }

    public async Task<OperationResult> SaveScheduleAsync(string userId, ConsultantScheduleViewModel model)
    {
        var errors = new List<string>();
        var consultant = await _repository.FindConsultantAsync(userId);

        if (consultant == null)
            return OperationResult.Missing();

        if (model.SlotDurationMinutes is < 1 or > 1440)
            errors.Add("Appointment duration must be between 1 and 1440 minutes.");

        if (model.WorkingDays == null ||
            model.WorkingDays.Count != 7 ||
            model.WorkingDays.Select(d => d.DayOfWeek).Distinct().Count() != 7 ||
            model.WorkingDays.Any(d => !Enum.IsDefined(d.DayOfWeek)))
        {
            errors.Add("The schedule must contain each weekday exactly once.");
        }

        if (model.WorkingDays != null)
        {
            foreach (var day in model.WorkingDays.Where(d => d.IsWorking))
            {
                if (day.StartTime >= day.EndTime)
                {
                    errors.Add($"{day.DayOfWeek}: End time must be after start time.");
                }

                if (day.EndTime > day.StartTime &&
                    (day.EndTime - day.StartTime).TotalMinutes <
                    model.SlotDurationMinutes)
                {
                    errors.Add($"{day.DayOfWeek}: Working hours must fit at least one appointment.");
                }
            }
        }

        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        var existing = await _repository.GetAvailabilityAsync(consultant.Id);

        var workingDays = model.WorkingDays!
            .Where(d => d.IsWorking)
            .ToList();

        var removedDays = existing
            .Where(a => workingDays.All(d => d.DayOfWeek != a.DayOfWeek))
            .ToList();

        // Preserve recurring breaks on active days.
        // Remove recurring breaks when their working day is removed.
        var removedDayNames = removedDays.Select(a => a.DayOfWeek).ToList();

        var obsoleteBreaks = await _repository.GetRecurringBreaksAsync(consultant.Id, removedDayNames);

        _repository.RemoveBreaks(obsoleteBreaks);
        _repository.RemoveAvailability(removedDays);

        foreach (var day in workingDays)
        {
            var availability = existing
                .FirstOrDefault(a => a.DayOfWeek == day.DayOfWeek);

            if (availability == null)
            {
                _repository.AddAvailability(
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

        await _repository.SaveAsync();

        return OperationResult.Success("Your working schedule has been updated.");
    }

    public async Task<ConsultantBreakListViewModel?> GetBreaksAsync(string userId)
    {
        var consultant = await _repository.FindConsultantAsync(userId);

        if (consultant == null)
            return null;

        var model = await BuildBreaksViewModelAsync(consultant.Id);

        return model;
    }

    private async Task<ConsultantBreakListViewModel>
        BuildBreaksViewModelAsync(int consultantId)
    {
        var breaks = await _repository.GetBreaksAsync(consultantId);

        var availability = await _repository.ReadAvailabilityAsync(consultantId);
        var workingDays = availability.OrderBy(a => a.DayOfWeek).Select(a => new WorkingDayOptionViewModel
        {
            DayOfWeek = a.DayOfWeek, StartTime = a.StartTime, EndTime = a.EndTime
        }).ToList();

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
    public async Task<OperationResult> AddBreakAsync(string userId, ConsultantBreakViewModel model)
    {
        var errors = new List<string>();
        var consultant = await _repository.FindConsultantAsync(userId);

        if (consultant == null)
            return OperationResult.Missing();

        // No selection means no new break; existing breaks stay unchanged.
        if (string.IsNullOrWhiteSpace(model.BreakType))
            return OperationResult.Success();

        if (model.BreakType != "Recurring" &&
            model.BreakType != "Specific")
        {
            errors.Add("Invalid break type.");
        }

        if (model.StartTime >= model.EndTime)
        {
            errors.Add("End time must be after start time.");
        }

        DayOfWeek targetDay = default;

        if (model.BreakType == "Recurring")
        {
            if (!model.DayOfWeek.HasValue ||
                !Enum.IsDefined(model.DayOfWeek.Value))
            {
                errors.Add("Please select a valid weekday.");
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
                errors.Add("Please select a date.");
            }
            else
            {
                targetDay = model.Date.Value.DayOfWeek;
            }
        }

        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        // A break must be within the consultant's working hours.
        var availability = await _repository.FindAvailabilityAsync(consultant.Id, targetDay);

        if (availability == null)
        {
            return OperationResult.Fail("You are not scheduled to work on this day.");
        }

        if (model.StartTime < availability.StartTime ||
            model.EndTime > availability.EndTime)
        {
            return OperationResult.Fail("Break must be within your working hours.");
        }

        // Fetch existing breaks for this consultant.
        var existingBreaks = await _repository.GetBreaksAsync(consultant.Id);

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
            return OperationResult.Fail("This break overlaps an existing break.");
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

        _repository.AddBreak(newBreak);

        await _repository.SaveAsync();

        return OperationResult.Success("Break added successfully.");
    }
    public async Task<OperationResult> DeleteBreakAsync(string userId, int id)
    {
        var consultant = await _repository.FindConsultantAsync(userId);

        if (consultant == null)
            return OperationResult.Missing();

        var existingBreak = await _repository.FindBreakAsync(consultant.Id, id);

        if (existingBreak == null)
            return OperationResult.Missing();

        _repository.RemoveBreak(existingBreak);

        await _repository.SaveAsync();

        return OperationResult.Success("Break deleted successfully.");
    }
}