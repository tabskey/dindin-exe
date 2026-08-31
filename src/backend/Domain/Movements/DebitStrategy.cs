using Domain.Entities;
using Domain.Results;

namespace Domain.Movements;

public sealed class DebitStrategy : IMovementStrategy
{
    public MovementType Type => MovementType.Debit;

    public Result Apply(Account account, long amount)
    {
        if (amount <= 0)
        {
            return Result.Failure(
                new DomainError(DomainErrorCode.InvalidAmount, "Amount must be greater than zero."));
        }

        if (account.Balance - amount < 0)
        {
            return Result.Failure(
                new DomainError(DomainErrorCode.InsufficientBalance, "Insufficient balance for this debit."));
        }

        account.SetBalance(account.Balance - amount);
        return Result.Success();
    }
}
