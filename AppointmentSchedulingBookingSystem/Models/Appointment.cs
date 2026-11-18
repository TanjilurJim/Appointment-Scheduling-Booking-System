namespace AppointmentSchedulingBookingSystem.Models;

public enum AppointmentStatus
{
    Confirmed = 0,
    Cancelled = 1,
    Completed = 2,
    NoShow = 3
}

public class Appointment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
