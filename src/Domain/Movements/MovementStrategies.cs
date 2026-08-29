using Domain.Entities;

namespace Domain.Movements;

public static class MovementStrategies
{
    public static IMovementStrategy For(MovementType type) => type switch
    {
        MovementType.Credit => new CreditStrategy(),
        MovementType.Debit => new DebitStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown movement type.")
    };
}
