using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

public partial class MovementEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MovementEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Credit_WithCounterpartyCpf_UsesSeedAccountLabel()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");

        // Bruno do seed: 222.222.222-22.
        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50, counterpartyCpf = "222.222.222-22" }, token, "cp-bruno");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        Assert.Equal("BRUNO TESTE 222-22 CC", movement!.Counterparty);
    }

    [Fact]
    public async Task Credit_WithoutCounterpartyCpf_UsesAutoDepositLabel()
    {
        var (id, cpf, token) = await _factory.RegisterAsync("Titular");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50 }, token, "cp-auto");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        Assert.Equal($"AUTO-DEPOSITO {ApiFactory.MaskCpf(cpf)} CC", movement!.Counterparty);
    }

    [Fact]
    public async Task Credit_WithUnknownCounterparty_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50, counterpartyCpf = "999.999.999-99" }, token, "cp-nope");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_WithCounterpartyAccountNumber_UsesRegisteredAccountLabel()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");
        var (_, counterpartyCpf, _) = await _factory.RegisterAsync("João Teste");

        string counterpartyAccountNumber;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            counterpartyAccountNumber = await db.Accounts
                .Where(a => a.Cpf == counterpartyCpf)
                .Select(a => a.AccountNumber)
                .SingleAsync();
        }

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50, counterpartyAccountNumber }, token, "cp-num-joao");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        Assert.Equal($"JOAO TESTE {ApiFactory.MaskCpf(counterpartyCpf)} CC", movement!.Counterparty);
    }

    [Fact]
    public async Task Debit_OverBalance_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Sem Saldo");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 1, amount = 10 }, token, "debit-over");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Movement_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 10 }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replay_SameIdempotencyKey_DoesNotDuplicate()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");
        var body = new { type = 0, amount = 50 };

        var first = await _factory.PostAsync($"/accounts/{id}/movements", body, token, "replay-key");
        var firstMovement = await first.Content.ReadFromJsonAsync<MovementDto>();

        var replay = await _factory.PostAsync($"/accounts/{id}/movements", body, token, "replay-key");
        var replayMovement = await replay.Content.ReadFromJsonAsync<MovementDto>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(firstMovement!.Id, replayMovement!.Id); // mesma movimentação, não duplicada

        var balance = await _factory.GetAsync($"/accounts/{id}/balance", token);
        var balanceDto = await balance.Content.ReadFromJsonAsync<BalanceDto>();
        Assert.Equal(50m, balanceDto!.Balance); // creditado uma única vez
    }

    [Fact]
    public async Task History_ClampsPageAndPageSize()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");

        var response = await _factory.GetAsync($"/accounts/{id}/movements?page=0&pageSize=0", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<MovementHistoryDto>();
        Assert.Equal(1, history!.Page); // page < 1 vira 1
        Assert.Equal(1, history.PageSize); // pageSize < 1 vira 1
        Assert.Equal(0, history.Total);
    }
}
