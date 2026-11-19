namespace AppointmentSchedulingBookingSystem.Models;

// One working window per weekday. Times are local to the clinic.
public class ConsultantAvailability
{
    public int Id { get; set; }
    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
