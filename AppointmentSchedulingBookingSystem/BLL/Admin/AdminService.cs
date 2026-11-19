using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.BLL.Common;
using AppointmentSchedulingBookingSystem.DAL.Admin;
using AppointmentSchedulingBookingSystem.DAL.Common;
namespace AppointmentSchedulingBookingSystem.BLL.Admin;
public class AdminService(IAdminRepository repository) : IAdminService
{
    private readonly IAdminRepository _repository = repository;
    public async Task<List<AdminUserViewModel>> GetUsersAsync()
    {
        var users = await _repository.GetUsersAsync();

        var customers = await _repository.GetCustomersAsync();

        var consultants = await _repository.GetConsultantsAsync();

        var model = new List<AdminUserViewModel>();

        foreach (var user in users)
        {
            var roles = await _repository.GetRolesAsync(user);

            customers.TryGetValue(user.Id, out var customer);
            consultants.TryGetValue(user.Id, out var consultant);

            model.Add(new AdminUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                UserName = user.UserName ?? "",
                Roles = roles.ToList(),
                Name = consultant?.Name ?? customer?.Name ?? "",
                Phone = customer?.Phone ?? "",
                Specialization = consultant?.Specialization ?? ""
            });
        }

        return model;
    }

    public async Task<OperationResult> UpdateUserAsync(UpdateUserViewModel model, IReadOnlySet<string> suppliedFields)
    {
        var user = await _repository.FindUserAsync(model.UserId);

        if (user == null)
            return OperationResult.Missing();

        var roles = await _repository.GetRolesAsync(user);

        if (roles.Contains("Admin"))
        {
            return OperationResult.Fail("Admin accounts cannot be modified here.");
        }


        var existingRole = roles.FirstOrDefault(
            r => r == "Customer" || r == "Consultant");

        var role = suppliedFields.Contains(nameof(model.Role)) ? model.Role : existingRole;
        var nameChanged = suppliedFields.Contains(nameof(model.Name));
        var phoneChanged = suppliedFields.Contains(nameof(model.Phone));
        var specializationChanged = suppliedFields.Contains(nameof(model.Specialization));

        if (role != "Customer" && role != "Consultant")
        {
            return OperationResult.Fail("Invalid role selected.");
        }

        if (existingRole != null && existingRole != role)
        {
            return OperationResult.Fail("Changing an existing Customer or Consultant role is not yet supported.");
        }

        // Does not assign a second role to a user with an unrelated role.
        if (roles.Any(r => r != role))
        {
            return OperationResult.Fail("This user's existing role requires review.");
        }

        // Profile and role changes use the same database transaction.
        await using var transaction =
            await _repository.BeginTransactionAsync();

        try
        {
            if (role == "Customer")
            {
                var customer = await _repository.FindCustomerAsync(user.Id);

                var name = nameChanged ? model.Name?.Trim() : customer?.Name;
                var phone = phoneChanged ? model.Phone?.Trim() : customer?.Phone;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
                    string.IsNullOrWhiteSpace(phone) || phone.Length > 20)
                {
                    return OperationResult.Fail("A name (up to 100 characters) and phone (up to 20 characters) are required.");
                }

                if (customer == null)
                {
                    customer = new Customer
                    {
                        UserId = user.Id
                    };

                    _repository.AddCustomer(customer);
                }

                customer.Name = name;
                customer.Phone = phone;
            }
            else
            {
                var consultant = await _repository.FindConsultantAsync(user.Id);

                var name = nameChanged ? model.Name?.Trim() : consultant?.Name;
                var specialization = specializationChanged
                    ? model.Specialization?.Trim() : consultant?.Specialization;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
                    string.IsNullOrWhiteSpace(specialization) || specialization.Length > 200)
                {
                    return OperationResult.Fail("A name (up to 100 characters) and specialization (up to 200 characters) are required.");
                }

                if (consultant == null)
                {
                    consultant = new Consultant
                    {
                        UserId = user.Id,
                        SlotDurationMinutes = 30,
                        IsActive = true
                    };

                    _repository.AddConsultant(consultant);
                }

                consultant.Name = name;
                consultant.Specialization = specialization;
            }

            await _repository.SaveAsync();

            if (!roles.Contains(role))
            {
                var result = await _repository.AddRoleAsync(
                    user, role);

                if (result.Length > 0)
                {
                    await transaction.RollbackAsync();
                    return OperationResult.Fail(result);
                }
            }

            await transaction.CommitAsync();

            return OperationResult.Success("User updated successfully.");
        }
        catch (DataWriteException)
        {
            await transaction.RollbackAsync();
            return OperationResult.Fail("Database update failed. No changes were saved.");
        }

    }
}
