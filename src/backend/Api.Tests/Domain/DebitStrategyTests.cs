using Domain.Entities;
using Domain.Movements;
using Domain.Results;
using Xunit;

namespace Api.Tests.Domain;

public class DebitStrategyTests
{
    private readonly DebitStrategy _strategy = new();

    [Fact]
    public void Type_ReturnsDebit()
    {
        Assert.Equal(MovementType.Debit, _strategy.Type);
    }

    [Fact]
    public void Apply_WithAvailableBalance_DecreasesBalance()
    {
        var account = FundedAccount(100);

        var result = _strategy.Apply(account, 40);

        Assert.True(result.IsSuccess);
        Assert.Equal(60, account.Balance);
    }

    [Fact]
    public void Apply_WithExactBalance_ReachesZero()
    {
        var account = FundedAccount(100);

        var result = _strategy.Apply(account, 100);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, account.Balance);
    }

    [Fact]
    public void Apply_ExceedingBalance_FailsAndKeepsBalance()
    {
        var account = FundedAccount(100);

        var result = _strategy.Apply(account, 100.01m);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientBalance, result.Error?.Code);
        Assert.Equal(100, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Apply_WithNonPositiveAmount_FailsWithInvalidAmount(decimal amount)
    {
        var account = FundedAccount(100);

        var result = _strategy.Apply(account, amount);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAmount, result.Error?.Code);
        Assert.Equal(100, account.Balance);
    }

    private static Account FundedAccount(decimal amount)
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        var credit = account.ApplyMovement(new CreditStrategy(), amount);
        Assert.True(credit.IsSuccess);
        return account;
    }
}
