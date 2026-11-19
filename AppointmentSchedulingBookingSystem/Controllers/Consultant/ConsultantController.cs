using System.Security.Claims;
using AppointmentSchedulingBookingSystem.BLL.Consultants;
using AppointmentSchedulingBookingSystem.BLL.Common;
using AppointmentSchedulingBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AppointmentSchedulingBookingSystem.Controllers.Consultant;

[Authorize(Roles = "Consultant")]
public class ConsultantController(IConsultantService service) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await service.GetWeekAsync(UserId));

    [HttpGet]
    public async Task<IActionResult> Schedule()
    {
        var model = await service.GetScheduleAsync(UserId);
        return model == null ? NotFound("Consultant profile not found.") : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(ConsultantScheduleViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await service.SaveScheduleAsync(UserId, model);
        if (result.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error);
            return View(model);
        }
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Schedule));
    }

    [HttpGet]
    public async Task<IActionResult> Breaks()
    {
        var model = await service.GetBreaksAsync(UserId);
        return model == null ? NotFound("Consultant profile not found.") : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBreak([Bind(Prefix = "NewBreak")] ConsultantBreakViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid break details." : e.ErrorMessage));
            return RedirectToAction(nameof(Breaks));
        }
        return BreakResult(await service.AddBreakAsync(UserId, model));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBreak(int id) =>
        BreakResult(await service.DeleteBreakAsync(UserId, id));

    private IActionResult BreakResult(OperationResult result)
    {
        if (result.NotFound) return NotFound();
        if (!result.Succeeded) TempData["Error"] = string.Join(" ", result.Errors);
        else if (result.Message != null) TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Breaks));
    }
}
