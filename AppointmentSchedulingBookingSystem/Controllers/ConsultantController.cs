using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSchedulingBookingSystem.Controllers
{
    [Authorize(Roles = "Admin,Consultant")]

    public class ConsultantController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
