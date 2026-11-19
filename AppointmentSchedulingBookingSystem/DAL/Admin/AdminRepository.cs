using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using AppointmentSchedulingBookingSystem.DAL.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AppointmentSchedulingBookingSystem.DAL.Admin;
public class AdminRepository(AppointmentSchedulingBookingSystemIdentityDbContext context,
    UserManager<IdentityUser> users) : IAdminRepository
{
    public Task<List<IdentityUser>> GetUsersAsync() => users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
    public Task<Dictionary<string, Customer>> GetCustomersAsync() => context.Customers.AsNoTracking().ToDictionaryAsync(c => c.UserId);
    public Task<Dictionary<string, Consultant>> GetConsultantsAsync() => context.Consultants.AsNoTracking().ToDictionaryAsync(c => c.UserId);
    public Task<IdentityUser?> FindUserAsync(string id) => users.FindByIdAsync(id);
    public Task<IList<string>> GetRolesAsync(IdentityUser user) => users.GetRolesAsync(user);
    public Task<Customer?> FindCustomerAsync(string userId) => context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
    public Task<Consultant?> FindConsultantAsync(string userId) => context.Consultants.FirstOrDefaultAsync(c => c.UserId == userId);
    public void AddCustomer(Customer customer) => context.Customers.Add(customer);
    public void AddConsultant(Consultant consultant) => context.Consultants.Add(consultant);
    public async Task SaveAsync()
    {
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateException ex) { throw new DataWriteException("Database update failed.", ex); }
    }
    public async Task<string[]> AddRoleAsync(IdentityUser user, string role)
    {
        try
        {
            var result = await users.AddToRoleAsync(user, role);
            return result.Errors.Select(e => e.Description).ToArray();
        }
        catch (DbUpdateException ex) { throw new DataWriteException("Role update failed.", ex); }
    }
    public async Task<IDataTransaction> BeginTransactionAsync() =>
        new DataTransaction(await context.Database.BeginTransactionAsync());
}
