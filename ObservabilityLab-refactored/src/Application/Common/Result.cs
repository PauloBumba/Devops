namespace ObservabilityLab.Application.Common;


public class Result<T>
{
    public bool   IsSuccess { get; }
    public bool   IsFailure => !IsSuccess;
    public T?     Value     { get; }
    public string Error     { get; }

    private Result(bool isSuccess, T? value, string error)
    {
        IsSuccess = isSuccess;
        Value     = value;
        Error     = error;
    }

    public static Result<T> Success(T value) => new(true, value, string.Empty);
    public static Result<T> Failure(string error) => new(false, default, error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error);
}


public class Result
{
    public bool   IsSuccess { get; }
    public bool   IsFailure => !IsSuccess;
    public string Error     { get; }

    private Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error     = error;
    }

    public static Result Success()           => new(true, string.Empty);
    public static Result Failure(string err) => new(false, err);
}
