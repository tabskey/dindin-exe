using Domain.Entities;
using Domain.Movements;
using Xunit;

namespace Api.Tests.Domain;

public class MovementStrategiesTests
{
    [Fact]
    public void For_Credit_ReturnsCreditStrategy()
    {
        Assert.IsType<CreditStrategy>(MovementStrategies.For(MovementType.Credit));
    }

    [Fact]
    public void For_Debit_ReturnsDebitStrategy()
    {
        Assert.IsType<DebitStrategy>(MovementStrategies.For(MovementType.Debit));
    }

    [Fact]
    public void For_UnknownType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MovementStrategies.For((MovementType)999));
    }
}
