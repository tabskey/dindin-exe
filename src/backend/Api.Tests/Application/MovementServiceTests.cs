using Application.Dtos;
using Application.Services;
using Domain.Entities;
using Domain.Results;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Application;

public class MovementServiceTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeMovementRepository _movements = new();
    private readonly MovementService _service;

    public MovementServiceTests() => _service = new MovementService(_accounts, _movements, NullLogger<MovementService>.Instance);

    private Account AddAccount(long id, long balance)
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(id);
        account.SetBalance(balance);
        _accounts.Accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task CreateAsync_Credit_IncreasesBalanceAndSavesMovement()
    {
        var account = AddAccount(1, 0);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 100));

        Assert.True(result.IsSuccess);
        Assert.Equal(100, account.Balance);
        Assert.Equal(100, result.Value!.Amount);
        Assert.Equal(MovementType.Credit, result.Value.Type);
        var movement = Assert.Single(_movements.Movements);
        Assert.Equal(1, movement.AccountId);
    }

    [Fact]
    public async Task CreateAsync_Debit_WithSufficientBalance_DecreasesBalance()
    {
        var account = AddAccount(1, 100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 40));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, account.Balance);
    }

    [Fact]
    public async Task CreateAsync_Debit_WithInsufficientBalance_FailsAndKeepsBalance()
    {
        var account = AddAccount(1, 10);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 20));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientBalance, result.Error?.Code);
        Assert.Equal(10, account.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownAccount_Fails()
    {
        var result = await _service.CreateAsync(999, new CreateMovementRequest(MovementType.Credit, 10));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AccountNotFound, result.Error?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAsync_WithNonPositiveAmount_Fails(long amount)
    {
        AddAccount(1, 100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, amount));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAmount, result.Error?.Code);
        Assert.Equal(100, _accounts.Accounts[0].Balance);
    }

    [Fact]
    public async Task CreateAsync_TransferByCpf_DebitsOwnerAndCreditsRecipient()
    {
        var ana = AddAccount(1, 100);
        var joao = Account.Create("João Teste", "222.222.222-22", AccountType.Checking, "hash");
        joao.SetId(2);
        _accounts.Accounts.Add(joao);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 40, "222.222.222-22"));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, ana.Balance);
        Assert.Equal(40, joao.Balance);

        var debit = _movements.Movements.Single(m => m.AccountId == 1);
        var credit = _movements.Movements.Single(m => m.AccountId == 2);
        Assert.Equal(MovementType.Debit, debit.Type);
        Assert.Equal("JOAO TESTE 222-22 CC", debit.Counterparty);
        Assert.Equal(MovementType.Credit, credit.Type);
        Assert.Equal("ANA TESTE 111-11 CC", credit.Counterparty);

        Assert.Equal(1, result.Value!.AccountId);
        Assert.Equal(MovementType.Debit, result.Value.Type);
        Assert.Equal("JOAO TESTE 222-22 CC", result.Value.Counterparty);
    }

    [Fact]
    public async Task CreateAsync_TransferWithInsufficientOwnerBalance_Fails()
    {
        var ana = AddAccount(1, 10);
        var joao = Account.Create("João Teste", "222.222.222-22", AccountType.Checking, "hash");
        joao.SetId(2);
        _accounts.Accounts.Add(joao);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 20, "222.222.222-22"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientBalance, result.Error?.Code);
        Assert.Equal(10, ana.Balance);
        Assert.Equal(0, joao.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_TransferToSelf_Fails()
    {
        var ana = AddAccount(1, 100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 10, "111.111.111-11"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidRequest, result.Error?.Code);
        Assert.Equal(100, ana.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_DebitWithCounterparty_Fails()
    {
        var ana = AddAccount(1, 100);
        var joao = Account.Create("João Teste", "222.222.222-22", AccountType.Checking, "hash");
        joao.SetId(2);
        _accounts.Accounts.Add(joao);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 10, "222.222.222-22"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidRequest, result.Error?.Code);
        Assert.Equal(100, ana.Balance);
        Assert.Equal(0, joao.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_TransferOnConcurrencyConflict_ReloadsBothAndRetries()
    {
        var ana = AddAccount(1, 100);
        var joao = Account.Create("João Teste", "222.222.222-22", AccountType.Checking, "hash");
        joao.SetId(2);
        _accounts.Accounts.Add(joao);
        _movements.ConcurrencyFailuresRemaining = 1;
        _accounts.OnReload = () =>
        {
            ana.SetBalance(100);
            joao.SetBalance(0);
        };

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 30, "222.222.222-22"));

        Assert.True(result.IsSuccess);
        Assert.Equal(70, ana.Balance);
        Assert.Equal(30, joao.Balance);
        Assert.Equal(2, _movements.SaveCallCount);
        Assert.Equal(2, _movements.Movements.Count);
    }

    [Fact]
    public async Task CreateAsync_WithoutCounterpartyCpf_UsesAutoDepositLabel()
    {
        AddAccount(1, 0);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 100));

        Assert.True(result.IsSuccess);
        Assert.Equal("AUTO-DEPOSITO 111-11 CC", result.Value!.Counterparty);
    }

    [Fact]
    public async Task CreateAsync_Debit_WithoutCounterparty_UsesAutoWithdrawalLabel()
    {
        AddAccount(1, 100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 40));

        Assert.True(result.IsSuccess);
        Assert.Equal("AUTO-SAQUE 111-11 CC", result.Value!.Counterparty);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCounterpartyCpf_FailsAndKeepsBalance()
    {
        var account = AddAccount(1, 100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 50, "999.999.999-99"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.CounterpartyNotFound, result.Error?.Code);
        Assert.Equal(100, account.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_TransferByAccountNumber_DebitsOwnerAndCreditsRecipient()
    {
        var ana = AddAccount(1, 100);
        var joao = Account.Create("João Teste", "222.222.222-22", AccountType.Checking, "hash");
        joao.SetId(2);
        joao.SetAccountNumber("00456-78");
        _accounts.Accounts.Add(joao);

        var result = await _service.CreateAsync(1,
            new CreateMovementRequest(MovementType.Credit, 40, CounterpartyAccountNumber: "00456-78"));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, ana.Balance);
        Assert.Equal(40, joao.Balance);
        Assert.Equal(2, _movements.Movements.Count);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCounterpartyAccountNumber_FailsAndKeepsBalance()
    {
        var account = AddAccount(1, 100);

        var result = await _service.CreateAsync(1,
            new CreateMovementRequest(MovementType.Credit, 50, CounterpartyAccountNumber: "99999-99"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.CounterpartyNotFound, result.Error?.Code);
        Assert.Equal(100, account.Balance);
        Assert.Empty(_movements.Movements);
    }

    [Fact]
    public async Task CreateAsync_OnConcurrencyConflict_ReloadsAndRetries()
    {
        var account = AddAccount(1, 100);
        _movements.ConcurrencyFailuresRemaining = 1;
        _accounts.OnReload = () => account.SetBalance(100);

        var result = await _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(70, account.Balance);
        Assert.Equal(2, _movements.SaveCallCount);
    }

    [Fact]
    public async Task CreateAsync_OnConcurrencyConflict_RetryExhausted_Throws()
    {
        AddAccount(1, 100);
        _movements.ConcurrencyFailuresRemaining = 3;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => _service.CreateAsync(1, new CreateMovementRequest(MovementType.Debit, 30)));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsPaginatedMovements()
    {
        AddAccount(1, 100);
        for (var i = 0; i < 5; i++)
        {
            await _movements.AddAsync(Movement.Create(1, MovementType.Credit, 10L + i).Value!);
        }

        var result = await _service.GetHistoryAsync(1, page: 1, pageSize: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Total);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(1, result.Value.Page);
    }

    [Fact]
    public async Task GetHistoryAsync_WithUnknownAccount_Fails()
    {
        var result = await _service.GetHistoryAsync(999, 1, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AccountNotFound, result.Error?.Code);
    }
}
