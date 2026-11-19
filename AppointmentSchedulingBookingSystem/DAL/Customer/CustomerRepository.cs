using System.Data;
using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.DAL.Common;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace AppointmentSchedulingBookingSystem.DAL.Customers;

public class CustomerRepository(AppointmentSchedulingBookingSystemIdentityDbContext context) : ICustomerRepository
{
    public Task<Customer?> FindCustomerAsync(string userId) => context.Customers.AsNoTracking()
        .FirstOrDefaultAsync(c => c.UserId == userId);

    public async Task<List<Consultant>> GetSchedulesAsync(DateOnly date, int? consultantId = null)
    {
        try
        {
            return await context.Consultants.AsNoTracking()
                .Where(c => c.IsActive && (!consultantId.HasValue || c.Id == consultantId))
                .Include(c => c.Availabilities)
                .Include(c => c.Breaks.Where(b => b.DayOfWeek == date.DayOfWeek || b.Date == date))
                .Include(c => c.Appointments.Where(a => a.Date == date && a.Status != AppointmentStatus.Cancelled))
                .AsSplitQuery().OrderBy(c => c.Name).ToListAsync();
        }
        catch (SqlException ex) when (IsConflict(ex)) { throw new BookingConflictException(ex); }
    }

    public async Task<List<Appointment>> GetCustomerAppointmentsAsync(int customerId, DateOnly from, DateOnly to)
    {
        try
        {
            return await context.Appointments.AsNoTracking().Include(a => a.Consultant)
                .Where(a => a.CustomerId == customerId && a.Date >= from && a.Date <= to)
                .OrderBy(a => a.Date).ThenBy(a => a.StartTime).ToListAsync();
        }
        catch (SqlException ex) when (IsConflict(ex)) { throw new BookingConflictException(ex); }
    }

    public async Task<IDataTransaction> BeginBookingAsync()
    {
        // Range locks cover the availability reads and INSERT, including empty appointment
        // ranges. Two overlapping bookings cannot both validate and commit successfully.
        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        return new BookingTransaction(transaction);
    }

    public async Task AddAppointmentAsync(Appointment appointment)
    {
        try
        {
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && IsConflict(sql))
        {
            throw new BookingConflictException(ex);
        }
        catch (SqlException ex) when (IsConflict(ex)) { throw new BookingConflictException(ex); }
    }

    private static bool IsConflict(SqlException ex) => ex.Number is 1205 or 2601 or 2627 or 1222;

    private sealed class BookingTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public async Task CommitAsync()
        {
            try { await transaction.CommitAsync(); }
            catch (SqlException ex) when (IsConflict(ex)) { throw new BookingConflictException(ex); }
        }
        public Task RollbackAsync() => transaction.RollbackAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
