using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AppointmentSchedulingBookingSystem.Models

{
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser User { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();



    }
}
