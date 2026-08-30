using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.Dtos;
using Domain.Entities;
using Xunit;

namespace Api.Tests.Integration;

public partial class AccountFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountFlowTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateAccount_ReturnsCreatedWithAccountShape()
    {
        var response = await _factory.Client.PostAsJsonAsync("/accounts",
            new { name = "Nova Conta", cpf = ApiFactory.NewCpf(), accountType = 0, password = "senha123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);
        Assert.True(account!.Id > 0);
        Assert.Matches(new Regex(@"^00\d{3}-\d{2}$"), account.AccountNumber);
        Assert.Equal("Nova Conta", account.Name);
        Assert.Equal(AccountType.Checking, account.AccountType);
    }

    [Fact]
    public async Task CreateAccount_DuplicateCpf_ReturnsConflict()
    {
        var cpf = ApiFactory.NewCpf();
        await _factory.Client.PostAsJsonAsync("/accounts",
            new { name = "Um", cpf, accountType = 0, password = "senha123" });

        var duplicate = await _factory.Client.PostAsJsonAsync("/accounts",
            new { name = "Dois", cpf, accountType = 0, password = "senha123" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_WeakPassword_ReturnsBadRequest()
    {
        var response = await _factory.Client.PostAsJsonAsync("/accounts",
            new { name = "Fraca", cpf = ApiFactory.NewCpf(), accountType = 0, password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var response = await _factory.Client.PostAsJsonAsync("/auth/login",
            new { cpf = "111.111.111-11", password = "errada" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Fluxo completo do checklist: criar conta → login → movimentação → saldo → histórico.
    [Fact]
    public async Task FullFlow_CreateAccount_Login_Movement_Balance_History()
    {
        var (id, _, token) = await _factory.RegisterAsync("Fluxo Completo");

        var created = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 50 }, token, "flow-credit");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var movement = await created.Content.ReadFromJsonAsync<MovementDto>();
        Assert.NotNull(movement);
        Assert.Equal(50m, movement!.Amount);
        Assert.Equal(MovementType.Credit, movement.Type);

        var balance = await _factory.GetAsync($"/accounts/{id}/balance", token);
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);
        var balanceDto = await balance.Content.ReadFromJsonAsync<BalanceDto>();
        Assert.Equal(50m, balanceDto!.Balance);

        var history = await _factory.GetAsync($"/accounts/{id}/movements?page=1&pageSize=10", token);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var historyDto = await history.Content.ReadFromJsonAsync<MovementHistoryDto>();
        Assert.Equal(1, historyDto!.Total);
        Assert.Single(historyDto.Items);
        Assert.Equal(50m, historyDto.Items[0].Amount);
    }
}
