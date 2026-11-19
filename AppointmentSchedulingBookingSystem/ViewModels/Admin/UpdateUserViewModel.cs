using System.ComponentModel.DataAnnotations;

namespace AppointmentSchedulingBookingSystem.ViewModels;

public class UpdateUserViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? Role { get; set; }

    [MaxLength(100)]
    public string? Name { get; set; }

    public string? Phone { get; set; }

    public string? Specialization { get; set; }
}