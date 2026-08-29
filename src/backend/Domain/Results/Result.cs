namespace Domain.Results;

public record Result
{
    public bool IsSuccess { get; init; }
    public DomainError? Error { get; init; }

    public static Result Success() => new() { IsSuccess = true };

    public static Result Failure(DomainError error) => new() { IsSuccess = false, Error = error };
}
