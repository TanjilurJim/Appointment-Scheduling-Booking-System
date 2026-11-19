namespace AppointmentSchedulingBookingSystem.BLL.Common;

public record OperationResult(bool NotFound = false, string? Message = null, params string[] Errors)
{
    public bool Succeeded => !NotFound && Errors.Length == 0;
    public static OperationResult Missing() => new(NotFound: true);
    public static OperationResult Fail(params string[] errors) => new(Errors: errors);
    public static OperationResult Success(string? message = null) => new(Message: message);
}
