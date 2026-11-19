namespace AppointmentSchedulingBookingSystem.BLL.Home;
public class HomeService : IHomeService
{
    public string? GetDashboard(IEnumerable<string> roles)
    {
        var assigned = roles.ToHashSet(StringComparer.Ordinal);
        return new[] { "Admin", "Consultant", "Customer" }.FirstOrDefault(assigned.Contains);
    }
}
