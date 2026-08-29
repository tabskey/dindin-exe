using Domain.Entities;
using Domain.Movements;
using Domain.Results;
using Xunit;

namespace Api.Tests.Domain;

public class AccountTests
{
    [Fact]
    public void Create_SetsFieldsAndStartsWithZeroBalance()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash123");

        Assert.False(string.IsNullOrWhiteSpace(account.AccountNumber));
        Assert.Equal("Ana Teste", account.Name);
        Assert.Equal("111.111.111-11", account.Cpf);
        Assert.Equal(AccountType.Checking, account.AccountType);
        Assert.Equal("hash123", account.PasswordHash);
        Assert.Equal(0, account.Balance);
        Assert.True(account.CreatedAt <= DateTime.UtcNow);
        Assert.NotNull(account.RowVersion);
    }

    [Fact]
    public void Create_GeneratesAccountNumberInSerialFormat()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        Assert.Matches(@"^00\d{3}-\d{2}$", account.AccountNumber);
    }

    [Fact]
    public void Create_GeneratesDistinctAccountNumbers()
    {
        var first = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        var second = Account.Create("Bruno Teste", "222.222.222-22", AccountType.Savings, "hash");

        Assert.NotEqual(first.AccountNumber, second.AccountNumber);
    }

    [Fact]
    public void ApplyMovement_CreditUpdatesBalance()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        var result = account.ApplyMovement(new CreditStrategy(), 50);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, account.Balance);
    }

    [Fact]
    public void ApplyMovement_DebitExceedingBalance_KeepsBalanceUnchanged()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        var result = account.ApplyMovement(new DebitStrategy(), 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientBalance, result.Error?.Code);
        Assert.Equal(0, account.Balance);
    }
}
