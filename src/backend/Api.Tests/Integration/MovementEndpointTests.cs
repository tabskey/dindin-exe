using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain.Entities;
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
    public async Task Transfer_WithCounterpartyCpf_MovesMoneyBetweenAccounts()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");
        var (targetId, targetCpf, targetToken) = await _factory.RegisterAsync("Maria Teste");

        await _factory.PostAsync($"/accounts/{id}/movements", new { type = 0, amount = 100 }, token, "fund-1");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 40, counterpartyCpf = targetCpf }, token, "tf-cpf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        Assert.Equal(MovementType.Debit, movement!.Type);
        string targetAccountNumber;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            targetAccountNumber = await db.Accounts
                .Where(a => a.Cpf == targetCpf)
                .Select(a => a.AccountNumber)
                .SingleAsync();
        }
        Assert.Equal($"MARIA TESTE {targetAccountNumber} CC", movement.Counterparty);

        var balance = await _factory.GetAsync($"/accounts/{id}/balance", token);
        Assert.Equal(60m, (await balance.Content.ReadFromJsonAsync<BalanceDto>())!.Balance);

        var targetBalance = await _factory.GetAsync($"/accounts/{targetId}/balance", targetToken);
        Assert.Equal(40m, (await targetBalance.Content.ReadFromJsonAsync<BalanceDto>())!.Balance);
    }

    [Fact]
    public async Task Credit_WithoutCounterpartyCpf_UsesAutoDepositLabel()
    {
        var (id, cpf, token) = await _factory.RegisterAsync("Titular");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50 }, token, "cp-auto");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        string accountNumber;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            accountNumber = await db.Accounts
                .Where(a => a.Cpf == cpf)
                .Select(a => a.AccountNumber)
                .SingleAsync();
        }
        Assert.Equal($"AUTO-DEPOSITO {accountNumber} CC", movement!.Counterparty);
    }

    [Fact]
    public async Task Debit_WithoutCounterparty_UsesAutoWithdrawalLabel()
    {
        var (id, cpf, token) = await _factory.RegisterAsync("Titular");
        await _factory.PostAsync($"/accounts/{id}/movements", new { type = 0, amount = 50 }, token, "dp-prime");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 1, amount = 10 }, token, "saque-auto");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        string accountNumber;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            accountNumber = await db.Accounts
                .Where(a => a.Cpf == cpf)
                .Select(a => a.AccountNumber)
                .SingleAsync();
        }
        Assert.Equal($"AUTO-SAQUE {accountNumber} CC", movement!.Counterparty);
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
    public async Task Transfer_WithCounterpartyAccountNumber_MovesMoneyBetweenAccounts()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");
        var (targetId, targetCpf, targetToken) = await _factory.RegisterAsync("Maria Teste");

        await _factory.PostAsync($"/accounts/{id}/movements", new { type = 0, amount = 100 }, token, "fund-2");

        string targetAccountNumber;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            targetAccountNumber = await db.Accounts
                .Where(a => a.Cpf == targetCpf)
                .Select(a => a.AccountNumber)
                .SingleAsync();
        }

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 40, counterpartyAccountNumber = targetAccountNumber }, token, "tf-num");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDto>();
        Assert.Equal(MovementType.Debit, movement!.Type);
        Assert.Equal($"MARIA TESTE {targetAccountNumber} CC", movement.Counterparty);

        var targetBalance = await _factory.GetAsync($"/accounts/{targetId}/balance", targetToken);
        Assert.Equal(40m, (await targetBalance.Content.ReadFromJsonAsync<BalanceDto>())!.Balance);
    }

    [Fact]
    public async Task Debit_WithCounterparty_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Titular");
        await _factory.PostAsync($"/accounts/{id}/movements", new { type = 0, amount = 100 }, token, "fund-3");

        var response = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 1, amount = 10, counterpartyCpf = "222.222.222-22" }, token, "debit-cp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
