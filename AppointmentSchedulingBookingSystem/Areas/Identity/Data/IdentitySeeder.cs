using Microsoft.AspNetCore.Identity;

namespace AppointmentSchedulingBookingSystem.Areas.Identity.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in new[] { "Admin", "Customer", "Consultant" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            }

            var userName = role.ToLowerInvariant();
            var email = configuration[$"IdentitySeed:{role}:Email"]
                ?? throw new InvalidOperationException($"IdentitySeed:{role}:Email is required.");
            var user = await userManager.FindByNameAsync(userName);

            if (user == null)
            {
                // Do not grant privileges to an unrelated account that already owns this email.
                if (await userManager.FindByEmailAsync(email) != null)
                {
                    throw new InvalidOperationException($"Cannot seed '{userName}': email '{email}' is already in use.");
                }

                var password = configuration[$"IdentitySeed:{role}:Password"]
                    ?? throw new InvalidOperationException($"IdentitySeed:{role}:Password is required.");
                user = new IdentityUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                EnsureSucceeded(await userManager.CreateAsync(user, password), $"create user '{userName}'");
            }
            else if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot seed '{userName}': the existing user's email does not match the configured email.");
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"assign '{role}' to '{userName}'");
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
