using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSchedulingBookingSystem.Controllers
{
    public class CustomerController : Controller
    {
        [Authorize(Roles = "Admin,Customer")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
