using Microsoft.EntityFrameworkCore.Storage;
namespace AppointmentSchedulingBookingSystem.DAL.Common;
internal sealed class DataTransaction(IDbContextTransaction transaction) : IDataTransaction
{
    public Task CommitAsync() => transaction.CommitAsync();
    public Task RollbackAsync() => transaction.RollbackAsync();
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
