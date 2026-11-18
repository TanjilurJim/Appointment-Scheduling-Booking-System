namespace AppointmentSchedulingBookingSystem.Models;

public class ConsultantBreak
{
    public int Id { get; set; }
    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;
    // Set exactly one: a recurring weekday or a one-off calendar date.
    public DayOfWeek? DayOfWeek { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
