using System.ComponentModel.DataAnnotations;

namespace AppointmentSchedulingBookingSystem.ViewModels;

public class ConsultantScheduleViewModel
{
    [Range(1, 1440)]
    public int SlotDurationMinutes { get; set; } = 30;

    public List<WorkingDayViewModel> WorkingDays { get; set; } = new();
}

public class WorkingDayViewModel
{
    public DayOfWeek DayOfWeek { get; set; }

    public bool IsWorking { get; set; }

    public TimeOnly StartTime { get; set; } = new(9, 0);

    public TimeOnly EndTime { get; set; } = new(17, 0);
}