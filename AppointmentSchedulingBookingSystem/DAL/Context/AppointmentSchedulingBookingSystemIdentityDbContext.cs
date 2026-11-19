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
    public DbSet<Consultant> Consultants { get; set; } = null!;
    public DbSet<ConsultantAvailability> ConsultantAvailabilities { get; set; } = null!;
    public DbSet<ConsultantBreak> ConsultantBreaks { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetAppointmentTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SetAppointmentTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SetAppointmentTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Appointment>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(a => a.CreatedAtUtc).IsModified = false;
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // One Identity user can have only one Customer profile
        builder.Entity<Customer>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        builder.Entity<Consultant>(entity =>
        {
            entity.HasIndex(c => c.UserId).IsUnique();
            entity.HasOne(c => c.User).WithOne().HasForeignKey<Consultant>(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Consultants", table => table.HasCheckConstraint(
                "CK_Consultants_SlotDuration", "[SlotDurationMinutes] BETWEEN 1 AND 1440"));
        });

        builder.Entity<ConsultantAvailability>(entity =>
        {
            entity.HasIndex(a => new { a.ConsultantId, a.DayOfWeek }).IsUnique();
            entity.HasOne(a => a.Consultant).WithMany(c => c.Availabilities)
                .HasForeignKey(a => a.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("ConsultantAvailabilities", table =>
            {
                table.HasCheckConstraint("CK_Availability_Day", "[DayOfWeek] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_Availability_Time", "[StartTime] < [EndTime]");
            });
        });

        builder.Entity<ConsultantBreak>(entity =>
        {
            entity.HasIndex(b => new { b.ConsultantId, b.DayOfWeek });
            entity.HasIndex(b => new { b.ConsultantId, b.Date });
            entity.HasOne(b => b.Consultant).WithMany(c => c.Breaks)
                .HasForeignKey(b => b.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("ConsultantBreaks", table =>
            {
                table.HasCheckConstraint("CK_Break_Schedule",
                    "([DayOfWeek] IS NOT NULL AND [Date] IS NULL) OR ([DayOfWeek] IS NULL AND [Date] IS NOT NULL)");
                table.HasCheckConstraint("CK_Break_Day", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_Break_Time", "[StartTime] < [EndTime]");
            });
        });

        builder.Entity<Appointment>(entity =>
        {
            entity.HasOne(a => a.Customer).WithMany(c => c.Appointments)
                .HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Consultant).WithMany(c => c.Appointments)
                .HasForeignKey(a => a.ConsultantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => new { a.CustomerId, a.Date });
            // Cancelled appointments release their start time. Overlap validation belongs in booking.
            entity.HasIndex(a => new { a.ConsultantId, a.Date, a.StartTime })
                .IsUnique().HasFilter("[Status] <> 1");
            entity.Property(a => a.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(a => a.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.ToTable("Appointments", table =>
            {
                table.HasCheckConstraint("CK_Appointment_Time", "[StartTime] < [EndTime]");
                table.HasCheckConstraint("CK_Appointment_Status", "[Status] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_Appointment_Timestamps", "[UpdatedAtUtc] >= [CreatedAtUtc]");
            });
        });
    }
}
