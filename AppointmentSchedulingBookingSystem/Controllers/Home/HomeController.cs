using System.Security.Claims;
using AppointmentSchedulingBookingSystem.BLL.Home;
using AppointmentSchedulingBookingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace AppointmentSchedulingBookingSystem.Controllers.Home
{
    public class HomeController(IHomeService service) : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            var target = service.GetDashboard(User.Identities.SelectMany(identity =>
                identity.FindAll(identity.RoleClaimType)).Select(claim => claim.Value));
            return target == null ? Forbid() : RedirectToAction("Index", target);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
