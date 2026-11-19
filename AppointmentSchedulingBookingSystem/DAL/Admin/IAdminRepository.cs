using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.DAL.Common;
using Microsoft.AspNetCore.Identity;
namespace AppointmentSchedulingBookingSystem.DAL.Admin;
public interface IAdminRepository
{
    Task<List<IdentityUser>> GetUsersAsync();
    Task<Dictionary<string, Customer>> GetCustomersAsync();
    Task<Dictionary<string, Consultant>> GetConsultantsAsync();
    Task<IdentityUser?> FindUserAsync(string id);
    Task<IList<string>> GetRolesAsync(IdentityUser user);
    Task<Customer?> FindCustomerAsync(string userId);
    Task<Consultant?> FindConsultantAsync(string userId);
    void AddCustomer(Customer customer);
    void AddConsultant(Consultant consultant);
    Task SaveAsync();
    Task<string[]> AddRoleAsync(IdentityUser user, string role);
    Task<IDataTransaction> BeginTransactionAsync();
}
