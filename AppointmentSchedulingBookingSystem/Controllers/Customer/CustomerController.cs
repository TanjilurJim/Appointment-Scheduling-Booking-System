using System.Security.Claims;
using AppointmentSchedulingBookingSystem.BLL.Customers;
using AppointmentSchedulingBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AppointmentSchedulingBookingSystem.Controllers.Customer;

[Authorize(Roles = "Admin,Customer")]
public class CustomerController(ICustomerService service) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please select a valid date.";
            return RedirectToAction(nameof(Index));
        }
        return View(await service.GetBookingPageAsync(UserId, date));
    }

    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Choose a consultant, date and available slot.";
            return RedirectToAction(nameof(Index), new { date = model.Date?.ToString("yyyy-MM-dd") });
        }
        var result = await service.BookAsync(UserId, model);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? result.Message : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index), new { date = model.Date?.ToString("yyyy-MM-dd") });
    }
}
