using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Xunit;

namespace Api.Tests.Integration;

public partial class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConcurrencyTests(ApiFactory factory) => _factory = factory;

    // Débitos concorrentes sobre o mesmo saldo: o lock otimista (RowVersion) com retry do
    // service garante que o saldo nunca fique negativo, mesmo com 5 requisições paralelas.
    [Fact]
    public async Task ConcurrentDebits_NeverProduceNegativeBalance()
    {
        var (id, _, token) = await _factory.RegisterAsync("Concorrente");
        var credit = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 100 }, token, "conc-credit");
        Assert.Equal(HttpStatusCode.Created, credit.StatusCode);

        var debits = Enumerable.Range(1, 5).Select(i =>
            _factory.PostAsync($"/accounts/{id}/movements",
                new { type = 1, amount = 80 }, token, $"conc-debit-{i}"));
        var results = await Task.WhenAll(debits);

        var successes = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = results.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(1, successes); // só um débito de 80 cabe no saldo de 100
        Assert.Equal(4, rejected); // os demais falham por saldo insuficiente (nenhum 500)

        var balance = await _factory.GetAsync($"/accounts/{id}/balance", token);
        var balanceDto = await balance.Content.ReadFromJsonAsync<BalanceDto>();
        Assert.Equal(20m, balanceDto!.Balance); // 100 - 80, nunca negativo
    }
}
