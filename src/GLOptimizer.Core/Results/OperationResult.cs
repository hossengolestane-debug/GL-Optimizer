namespace GLOptimizer.Core.Results;

public enum OperationStatus
{
    Success = 0,
    Failed = 1,
    NotImplemented = 2
}

public sealed class OperationResult
{
    private OperationResult(bool succeeded, OperationStatus status, string? error)
    {
        Succeeded = succeeded;
        Status = status;
        Error = error;
    }

    public bool Succeeded { get; }

    public OperationStatus Status { get; }

    public string? Error { get; }

    public static OperationResult Success() => new(true, OperationStatus.Success, null);

    public static OperationResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new OperationResult(false, OperationStatus.Failed, error);
    }

    public static OperationResult NotImplemented(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        return new OperationResult(false, OperationStatus.NotImplemented, $"{feature} is not implemented.");
    }
}

public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, OperationStatus status, T? value, string? error)
    {
        Succeeded = succeeded;
        Status = status;
        Value = value;
        Error = error;
    }

    public bool Succeeded { get; }

    public OperationStatus Status { get; }

    public T? Value { get; }

    public string? Error { get; }

    public static OperationResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OperationResult<T>(true, OperationStatus.Success, value, null);
    }

    public static OperationResult<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new OperationResult<T>(false, OperationStatus.Failed, default, error);
    }

    public static OperationResult<T> NotImplemented(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        return new OperationResult<T>(false, OperationStatus.NotImplemented, default, $"{feature} is not implemented.");
    }
}
