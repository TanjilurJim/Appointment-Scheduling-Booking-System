using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSchedulingBookingSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppointmentSchedulingBookingSystemIdentityDbContext _context;

    public AdminController(
        UserManager<IdentityUser> userManager,
        AppointmentSchedulingBookingSystemIdentityDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync();

        var customers = await _context.Customers
            .AsNoTracking()
            .ToDictionaryAsync(c => c.UserId);

        var consultants = await _context.Consultants
            .AsNoTracking()
            .ToDictionaryAsync(c => c.UserId);

        var model = new List<AdminUserViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            customers.TryGetValue(user.Id, out var customer);
            consultants.TryGetValue(user.Id, out var consultant);

            model.Add(new AdminUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                UserName = user.UserName ?? "",
                Roles = roles.ToList(),
                Name = consultant?.Name ?? customer?.Name ?? "",
                Phone = customer?.Phone ?? "",
                Specialization = consultant?.Specialization ?? ""
            });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(UpdateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid name and role.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(model.UserId);

        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("Admin"))
        {
            TempData["Error"] = "Admin accounts cannot be modified here.";
            return RedirectToAction(nameof(Index));
        }


        var existingRole = roles.FirstOrDefault(
            r => r == "Customer" || r == "Consultant");

        var form = await Request.ReadFormAsync();
        var role = form.ContainsKey(nameof(model.Role)) ? model.Role : existingRole;
        var nameChanged = form.ContainsKey(nameof(model.Name));
        var phoneChanged = form.ContainsKey(nameof(model.Phone));
        var specializationChanged = form.ContainsKey(nameof(model.Specialization));

        if (role != "Customer" && role != "Consultant")
        {
            TempData["Error"] = "Invalid role selected.";
            return RedirectToAction(nameof(Index));
        }

        if (existingRole != null && existingRole != role)
        {
            TempData["Error"] =
                "Changing an existing Customer or Consultant role is not yet supported.";
            return RedirectToAction(nameof(Index));
        }

        // Does not assign a second role to a user with an unrelated role.
        if (roles.Any(r => r != role))
        {
            TempData["Error"] = "This user's existing role requires review.";
            return RedirectToAction(nameof(Index));
        }

        // Profile and role changes use the same database transaction.
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            if (role == "Customer")
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                var name = nameChanged ? model.Name?.Trim() : customer?.Name;
                var phone = phoneChanged ? model.Phone?.Trim() : customer?.Phone;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
                    string.IsNullOrWhiteSpace(phone) || phone.Length > 20)
                {
                    TempData["Error"] = "A name (up to 100 characters) and phone (up to 20 characters) are required.";
                    return RedirectToAction(nameof(Index));
                }

                if (customer == null)
                {
                    customer = new Customer
                    {
                        UserId = user.Id
                    };

                    _context.Customers.Add(customer);
                }

                customer.Name = name;
                customer.Phone = phone;
            }
            else
            {
                var consultant = await _context.Consultants
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                var name = nameChanged ? model.Name?.Trim() : consultant?.Name;
                var specialization = specializationChanged
                    ? model.Specialization?.Trim() : consultant?.Specialization;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
                    string.IsNullOrWhiteSpace(specialization) || specialization.Length > 200)
                {
                    TempData["Error"] = "A name (up to 100 characters) and specialization (up to 200 characters) are required.";
                    return RedirectToAction(nameof(Index));
                }

                if (consultant == null)
                {
                    consultant = new Consultant
                    {
                        UserId = user.Id,
                        SlotDurationMinutes = 30,
                        IsActive = true
                    };

                    _context.Consultants.Add(consultant);
                }

                consultant.Name = name;
                consultant.Specialization = specialization;
            }

            await _context.SaveChangesAsync();

            if (!roles.Contains(role))
            {
                var result = await _userManager.AddToRoleAsync(
                    user, role);

                if (!result.Succeeded)
                {
                    TempData["Error"] = string.Join(
                        "; ",
                        result.Errors.Select(e => e.Description));

                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Index));
                }
            }

            await transaction.CommitAsync();

            TempData["Success"] = "User updated successfully.";
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Database update failed. No changes were saved.";
        }

        return RedirectToAction(nameof(Index));
    }
}
