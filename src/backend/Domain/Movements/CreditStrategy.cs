using Domain.Entities;
using Domain.Results;

namespace Domain.Movements;

public sealed class CreditStrategy : IMovementStrategy
{
    public MovementType Type => MovementType.Credit;

    public Result Apply(Account account, long amount)
    {
        if (amount <= 0)
        {
            return Result.Failure(
                new DomainError(DomainErrorCode.InvalidAmount, "Amount must be greater than zero."));
        }

        account.SetBalance(account.Balance + amount);
        return Result.Success();
    }
}
