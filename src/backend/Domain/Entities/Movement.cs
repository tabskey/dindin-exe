using Domain.Results;

namespace Domain.Entities;

public class Movement
{
    public long Id { get; private set; }
    public long AccountId { get; private set; }
    public MovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? Counterparty { get; private set; }

    private Movement() { } // EF Core

    public static Result<Movement> Create(long accountId, MovementType type, decimal amount, string? counterparty = null)
    {
        if (amount <= 0)
        {
            return Result<Movement>.Failure(
                new DomainError(DomainErrorCode.InvalidAmount, "Amount must be greater than zero."));
        }

        return Result<Movement>.Success(new Movement
        {
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            Counterparty = counterparty
        });
    }
}
