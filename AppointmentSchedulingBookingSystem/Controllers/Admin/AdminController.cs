using AppointmentSchedulingBookingSystem.BLL.Admin;
using AppointmentSchedulingBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AppointmentSchedulingBookingSystem.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminController(IAdminService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await service.GetUsersAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(UpdateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid name and role.";
            return RedirectToAction(nameof(Index));
        }
        var form = await Request.ReadFormAsync();
        var result = await service.UpdateUserAsync(model, form.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
        if (result.NotFound) return NotFound();
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? result.Message : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }
}
