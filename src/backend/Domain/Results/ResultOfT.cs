namespace Domain.Results;

public record Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };

    public static new Result<T> Failure(DomainError error) => new() { IsSuccess = false, Error = error };
}
