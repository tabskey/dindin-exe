using Domain.Entities;
using Domain.Results;

namespace Domain.Movements;

public interface IMovementStrategy
{
    MovementType Type { get; }
    Result Apply(Account account, long amount);
}
