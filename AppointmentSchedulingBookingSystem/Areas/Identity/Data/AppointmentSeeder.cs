using AppointmentSchedulingBookingSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSchedulingBookingSystem.Areas.Identity.Data;

public static class AppointmentSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppointmentSchedulingBookingSystemIdentityDbContext>();
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var customerUser = await users.FindByNameAsync("customer")
            ?? throw new InvalidOperationException("Seed Identity accounts before appointment sample data.");
        var consultantUser = await users.FindByNameAsync("consultant")
            ?? throw new InvalidOperationException("Seed Identity accounts before appointment sample data.");

        if (!await db.Customers.AnyAsync(c => c.UserId == customerUser.Id))
        {
            db.Customers.Add(new Customer
            {
                UserId = customerUser.Id, Name = "Sample Customer",
                Phone = "01700000000", Address = "Dhaka"
            });
        }

        // Only initialize a new profile, preserving any later edits to its schedule.
        if (!await db.Consultants.AnyAsync(c => c.UserId == consultantUser.Id))
        {
            db.Consultants.Add(new Consultant
            {
                UserId = consultantUser.Id, Name = "Sample Consultant",
                Specialization = "General Consultation", SlotDurationMinutes = 30,
                Availabilities = new List<ConsultantAvailability>
                {
                    new() { DayOfWeek = DayOfWeek.Saturday, StartTime = new(9, 0), EndTime = new(17, 0) },
                    new() { DayOfWeek = DayOfWeek.Sunday, StartTime = new(10, 0), EndTime = new(14, 0) }
                },
                Breaks = new List<ConsultantBreak>
                {
                    new() { DayOfWeek = DayOfWeek.Saturday, StartTime = new(13, 0), EndTime = new(14, 0) }
                }
            });
        }

        await db.SaveChangesAsync();
    }
}
