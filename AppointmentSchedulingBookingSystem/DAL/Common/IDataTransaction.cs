namespace AppointmentSchedulingBookingSystem.DAL.Common;
public interface IDataTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}
public class DataWriteException(string message, Exception inner) : Exception(message, inner);
