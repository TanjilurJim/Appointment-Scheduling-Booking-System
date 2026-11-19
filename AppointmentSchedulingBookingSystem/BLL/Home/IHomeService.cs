namespace AppointmentSchedulingBookingSystem.BLL.Home;
public interface IHomeService
{
    string? GetDashboard(IEnumerable<string> roles);
}
