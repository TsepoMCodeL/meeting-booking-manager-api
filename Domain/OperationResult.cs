public enum OperationErrorCode
{
    Validation,
    RoomNotFound,
    BookingNotFound,
    BookingConflict,
    MeetingAlreadyStarted
}

// Carries a use-case error and optional field validation details.
public sealed record OperationError(
    OperationErrorCode Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

// Carries either a successful value or the error that prevented the operation.
public sealed record OperationResult<T>(T? Value, OperationError? Error)
{
    public bool Succeeded => Error is null;

    public static OperationResult<T> Success(T value) => new(value, null);

    public static OperationResult<T> Failure(OperationError error) => new(default, error);
}
