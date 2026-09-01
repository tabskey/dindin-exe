using Application.Dtos;
using Application.Services;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Results;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Application;

public class AccountServiceTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly AccountService _service;

    public AccountServiceTests() => _service = new AccountService(_accounts, NullLogger<AccountService>.Instance);

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
    [InlineData("1234567890")]      // 10 dígitos
    [InlineData("123456789012")]    // 12 dígitos
    [InlineData("111.111.111-11x")] // 11 dígitos + caractere extra
    public async Task CreateAsync_WithMalformedCpf_Fails(string cpf)
    {
        var result = await _service.CreateAsync(ValidRequest() with { Cpf = cpf });

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidRequest, result.Error?.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenAccountNumberCollides_RetriesWithNewNumber()
    {
        _accounts.Accounts.Add(Account.Create("Bruno Teste", "222.222.222-22", AccountType.Checking, "hash"));
        _accounts.AccountNumberCollisionsRemaining = 2;

        var result = await _service.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _accounts.Accounts.Count);
    }

    [Fact]
    public async Task CreateAsync_WhenAccountNumberAlwaysCollides_FailsWithCollision()
    {
        _accounts.Accounts.Add(Account.Create("Bruno Teste", "222.222.222-22", AccountType.Checking, "hash"));
        _accounts.AccountNumberCollisionsRemaining = 100;

        var result = await _service.CreateAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AccountNumberCollision, result.Error?.Code);
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

    [Fact]
    public async Task UpdateAvatarAsync_WithValidImage_SavesAvatar()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        _accounts.Accounts.Add(account);
        var avatar = new byte[100];

        var result = await _service.UpdateAvatarAsync(1, avatar, "image/png");

        Assert.True(result.IsSuccess);
        Assert.Equal(avatar, account.Avatar);
        Assert.Equal("image/png", account.AvatarContentType);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/gif")]
    public async Task UpdateAvatarAsync_WithUnsupportedContentType_Fails(string contentType)
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        _accounts.Accounts.Add(account);

        var result = await _service.UpdateAvatarAsync(1, new byte[10], contentType);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAvatar, result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAvatarAsync_WithOversizedImage_Fails()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        _accounts.Accounts.Add(account);

        var result = await _service.UpdateAvatarAsync(1, new byte[512 * 1024 + 1], "image/jpeg");

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAvatar, result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAvatarAsync_WithUnknownAccount_Fails()
    {
        var result = await _service.UpdateAvatarAsync(999, new byte[10], "image/png");

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AccountNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task GetAvatarAsync_ReturnsStoredAvatar()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        account.SetAvatar(new byte[] { 1, 2, 3 }, "image/webp");
        _accounts.Accounts.Add(account);

        var result = await _service.GetAvatarAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value!.Data);
        Assert.Equal("image/webp", result.Value.ContentType);
    }

    [Fact]
    public async Task GetAvatarAsync_WhenAccountHasNoAvatar_Fails()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        _accounts.Accounts.Add(account);

        var result = await _service.GetAvatarAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.AvatarNotFound, result.Error?.Code);
    }
}
