using Domain.Entities;
using Xunit;

namespace Api.Tests.Domain;

public class CounterpartyLabelTests
{
    [Fact]
    public void For_UsesUppercaseNameWithoutAccents_AndAccountNumberSuffixCc()
    {
        var account = Account.Create("João Teste", "123.456.789-09", AccountType.Checking, "hash");
        account.SetAccountNumber("00456-78");

        var label = CounterpartyLabel.For(account);

        Assert.Equal("JOAO TESTE 00456-78 CC", label);
    }

    [Fact]
    public void AutoDeposit_UsesOwnAccountNumber_AndAutoDepositoPrefix()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetAccountNumber("00319-78");

        var label = CounterpartyLabel.AutoDeposit(account);

        Assert.Equal("AUTO-DEPOSITO 00319-78 CC", label);
    }

    [Fact]
    public void AutoWithdrawal_UsesOwnAccountNumber_AndAutoSaquePrefix()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetAccountNumber("00319-78");

        var label = CounterpartyLabel.AutoWithdrawal(account);

        Assert.Equal("AUTO-SAQUE 00319-78 CC", label);
    }
}
