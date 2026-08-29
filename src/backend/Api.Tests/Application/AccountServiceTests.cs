using Application.Dtos;
using Application.Services;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Results;
using Xunit;

namespace Api.Tests.Application;

public class AccountServiceTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly AccountService _service;

    public AccountServiceTests() => _service = new AccountService(_accounts);

    private static CreateAccountRequest ValidRequest() =>
        new("Ana Teste", "111.111.111-11", AccountType.Checking, "senha123");

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesAccountWithHashedPassword()
    {
        var result = await _service.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal("Ana Teste", account.Name);
        Assert.Equal("111.111.111-11", account.Cpf);
        Assert.Equal(0, account.Balance);
        Assert.True(BC.Verify("senha123", account.PasswordHash));
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.AccountNumber));
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCpf_Fails()
    {
        _accounts.Accounts.Add(Account.Create("Bruno Teste", "111.111.111-11", AccountType.Checking, "hash"));

        var result = await _service.CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.CpfAlreadyRegistered, result.Error?.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public async Task CreateAsync_WithWeakPassword_Fails(string? password)
    {
        var result = await _service.CreateAsync(ValidRequest() with { Password = password! });

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.WeakPassword, result.Error?.Code);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAccount()
    {
        _accounts.Accounts.Add(Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, BC.HashPassword("senha123")));

        var result = await _service.LoginAsync(new LoginRequest("111.111.111-11", "senha123"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana Teste", result.Value!.Name);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_Fails()
    {
        _accounts.Accounts.Add(Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, BC.HashPassword("senha123")));

        var result = await _service.LoginAsync(new LoginRequest("111.111.111-11", "errada"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidCredentials, result.Error?.Code);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownCpf_Fails()
    {
        var result = await _service.LoginAsync(new LoginRequest("999.999.999-99", "senha123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidCredentials, result.Error?.Code);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsCurrentBalance()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        account.SetBalance(250);
        _accounts.Accounts.Add(account);

        var result = await _service.GetBalanceAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(250, result.Value!.Balance);
    }

    [Fact]
    public async Task GetBalanceAsync_WithUnknownAccount_Fails()
    {
        var result = await _service.GetBalanceAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AccountNotFound, result.Error?.Code);
    }
}
