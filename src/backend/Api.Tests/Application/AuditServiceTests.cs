using Application.Dtos;
using Application.Services;
using Domain.Entities;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Application;

public class AuditServiceTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeMovementRepository _movements = new();
    private readonly FakeAuditLogRepository _audit = new();

    [Fact]
    public async Task AuditedAccountService_CreateAsync_WritesAuditLog()
    {
        var service = new AuditedAccountService(new AccountService(_accounts, NullLogger<AccountService>.Instance), _audit);

        var result = await service.CreateAsync(new CreateAccountRequest("Ana Teste", "111.111.111-11", AccountType.Checking, "senha123"));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(_audit.Logs);
        Assert.Equal("Account", log.EntityType);
        Assert.Equal(result.Value!.Id.ToString(), log.EntityId);
        Assert.Equal("create", log.Action);
        Assert.False(string.IsNullOrWhiteSpace(log.Payload));
    }

    [Fact]
    public async Task AuditedAccountService_CreateAsync_WhenFails_DoesNotWriteAuditLog()
    {
        _accounts.Accounts.Add(Account.Create("Outra", "111.111.111-11", AccountType.Checking, "hash"));
        var service = new AuditedAccountService(new AccountService(_accounts, NullLogger<AccountService>.Instance), _audit);

        var result = await service.CreateAsync(new CreateAccountRequest("Ana Teste", "111.111.111-11", AccountType.Checking, "senha123"));

        Assert.False(result.IsSuccess);
        Assert.Empty(_audit.Logs);
    }

    [Fact]
    public async Task AuditedMovementService_CreateAsync_WritesAuditLog()
    {
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        account.SetId(1);
        _accounts.Accounts.Add(account);
        var service = new AuditedMovementService(new MovementService(_accounts, _movements, NullLogger<MovementService>.Instance), _audit);

        var result = await service.CreateAsync(1, new CreateMovementRequest(MovementType.Credit, 50));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(_audit.Logs);
        Assert.Equal("Movement", log.EntityType);
        Assert.Equal(result.Value!.Id.ToString(), log.EntityId);
        Assert.Equal("create", log.Action);
        Assert.False(string.IsNullOrWhiteSpace(log.Payload));
    }

    [Fact]
    public async Task AuditedMovementService_GetHistoryAsync_DoesNotWriteAuditLog()
    {
        var service = new AuditedMovementService(new MovementService(_accounts, _movements, NullLogger<MovementService>.Instance), _audit);

        await service.GetHistoryAsync(999, 1, 10);

        Assert.Empty(_audit.Logs);
    }
}
