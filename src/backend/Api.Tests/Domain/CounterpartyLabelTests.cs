using Domain.Entities;
using Xunit;

namespace Api.Tests.Domain;

public class CounterpartyLabelTests
{
    [Theory]
    [InlineData("123.456.789-09", "789-09")]
    [InlineData("111.111.111-11", "111-11")]
    [InlineData("22222222222", "222-22")]
    public void MaskCpf_ReturnsLastFiveDigitsFormatted(string cpf, string expected)
    {
        Assert.Equal(expected, CounterpartyLabel.MaskCpf(cpf));
    }

    [Fact]
    public void For_UsesUppercaseNameWithoutAccents_AndSuffixCc()
    {
        var account = Account.Create("João Teste", "123.456.789-09", AccountType.Checking, "hash");

        var label = CounterpartyLabel.For(account);

        Assert.Equal("JOAO TESTE 789-09 CC", label);
    }

    [Fact]
    public void AutoDeposit_UsesOwnCpf_AndAutoDepositoPrefix()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");

        var label = CounterpartyLabel.AutoDeposit(account);

        Assert.Equal("AUTO-DEPOSITO 111-11 CC", label);
    }
}
