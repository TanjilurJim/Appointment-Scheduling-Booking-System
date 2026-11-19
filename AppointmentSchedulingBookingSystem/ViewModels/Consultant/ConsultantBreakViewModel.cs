using System.ComponentModel.DataAnnotations;

namespace AppointmentSchedulingBookingSystem.ViewModels;

public class ConsultantBreakViewModel
{
    public string? BreakType { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }
}

public class ConsultantBreakListViewModel
{
    public List<WorkingDayOptionViewModel> WorkingDays { get; set; } = new();

    public List<ConsultantBreakItemViewModel> Breaks { get; set; } = new();

    public ConsultantBreakViewModel NewBreak { get; set; } = new();
}

public class ConsultantBreakItemViewModel
{
    public int Id { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    public DateOnly? Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}
public class WorkingDayOptionViewModel
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
