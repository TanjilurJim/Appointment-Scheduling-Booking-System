namespace AppointmentSchedulingBookingSystem.ViewModels;

public class ConsultantWeekViewModel
{
    public DateOnly Today { get; set; }
    public DateOnly WeekStart { get; set; }
    public bool HasProfile { get; set; }
    public int SlotDurationMinutes { get; set; }
    public List<ConsultantWeekDayViewModel> Days { get; set; } = new();
}

public class ConsultantWeekDayViewModel
{
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public List<ConsultantBreakItemViewModel> Breaks { get; set; } = new();
}
