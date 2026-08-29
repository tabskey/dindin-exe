namespace Domain.Results;

public sealed record DomainError(DomainErrorCode Code, string Message);
