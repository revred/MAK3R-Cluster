namespace MAK3R.Core;

public record Result<T>
{
    public T? Value { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public Exception? Exception { get; init; }

    private Result(T? value, bool isSuccess, string? error, Exception? exception = null)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(value, true, null);
    
    public static Result<T> Failure(string error, Exception? exception = null) => 
        new(default, false, error, exception);

    public static implicit operator Result<T>(T value) => Success(value);
    
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess && Value is not null
            ? Result<TNew>.Success(mapper(Value))
            : Result<TNew>.Failure(Error ?? "Unknown error", Exception);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (!IsSuccess || Value is null)
            return Result<TNew>.Failure(Error ?? "Unknown error", Exception);

        try
        {
            var result = await mapper(Value);
            return Result<TNew>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(ex.Message, ex);
        }
    }
}

public record Result
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public Exception? Exception { get; init; }

    private Result(bool isSuccess, string? error, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Exception = exception;
    }

    public static Result Success() => new(true, null);
    
    public static Result Failure(string error, Exception? exception = null) => 
        new(false, error, exception);

    public static implicit operator bool(Result result) => result.IsSuccess;
}