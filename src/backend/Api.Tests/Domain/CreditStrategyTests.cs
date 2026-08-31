using Domain.Entities;
using Domain.Movements;
using Domain.Results;
using Xunit;

namespace Api.Tests.Domain;

public class CreditStrategyTests
{
    private readonly CreditStrategy _strategy = new();

    [Fact]
    public void Type_ReturnsCredit()
    {
        Assert.Equal(MovementType.Credit, _strategy.Type);
    }

    [Fact]
    public void Apply_WithPositiveAmount_IncreasesBalance()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        var result = _strategy.Apply(account, 15000);

        Assert.True(result.IsSuccess);
        Assert.Equal(15000, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Apply_WithNonPositiveAmount_FailsAndKeepsBalance(long amount)
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        var result = _strategy.Apply(account, amount);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAmount, result.Error?.Code);
        Assert.Equal(0, account.Balance);
    }
}
