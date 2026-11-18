using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AppointmentSchedulingBookingSystem.Models;

namespace AppointmentSchedulingBookingSystem.Areas.Identity.Data;

public class AppointmentSchedulingBookingSystemIdentityDbContext
    : IdentityDbContext<IdentityUser>
{
    public AppointmentSchedulingBookingSystemIdentityDbContext(
        DbContextOptions<AppointmentSchedulingBookingSystemIdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // One Identity user can have only one Customer profile
        builder.Entity<Customer>()
            .HasIndex(c => c.UserId)
            .IsUnique();
    }
}
