using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AppointmentSchedulingBookingSystem.Models;

public class Consultant
{
    public int Id { get; set; }
    [Required]
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string Specialization { get; set; } = string.Empty;
    [Range(1, 1440)]
    public int SlotDurationMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public ICollection<ConsultantAvailability> Availabilities { get; set; } = new List<ConsultantAvailability>();
    public ICollection<ConsultantBreak> Breaks { get; set; } = new List<ConsultantBreak>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
