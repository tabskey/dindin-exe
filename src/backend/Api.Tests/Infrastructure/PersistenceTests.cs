using Application.Abstractions;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Infrastructure;

public class PersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public PersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateContext()
    {
        var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Seed_CreatesThreeAccountsWithMovements_AndBcryptHashes()
    {
        await using var db = CreateContext();
        DbInitializer.Seed(db);

        Assert.Equal(3, await db.Accounts.CountAsync());
        var ana = await db.Accounts.AsNoTracking().SingleAsync(a => a.Cpf == "111.111.111-11");
        var bruno = await db.Accounts.AsNoTracking().SingleAsync(a => a.Cpf == "222.222.222-22");
        var carlos = await db.Accounts.AsNoTracking().SingleAsync(a => a.Cpf == "333.333.333-33");
        Assert.True(BC.Verify("senha123", ana.PasswordHash));
        Assert.True(BC.Verify("senha123", bruno.PasswordHash));
        Assert.True(BC.Verify("senha123", carlos.PasswordHash));
        Assert.Equal(1050, ana.Balance);
        Assert.Equal(80, bruno.Balance);
        Assert.Equal(0, carlos.Balance);
        Assert.Equal(8, await db.Movements.CountAsync());
    }

    [Fact]
    public async Task Migrate_AppliesInitialCreateSchema_AndAllowsSeeding()
    {
        await using var db = new AppDbContext(_options);
        db.Database.Migrate();

        DbInitializer.Seed(db);

        Assert.Equal(3, await db.Accounts.CountAsync());
        Assert.Equal(8, await db.Movements.CountAsync());
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Equal(2, applied.Count());
        Assert.Contains(applied, m => m.EndsWith("_InitialCreate"));
        Assert.Contains(applied, m => m.EndsWith("_AddAvatar"));
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        await using var db = CreateContext();
        DbInitializer.Seed(db);
        DbInitializer.Seed(db);

        Assert.Equal(3, await db.Accounts.CountAsync());
        Assert.Equal(8, await db.Movements.CountAsync());
    }

    [Fact]
    public async Task Seed_BalancesMatchMovementSums()
    {
        await using var db = CreateContext();
        DbInitializer.Seed(db);

        foreach (var account in await db.Accounts.AsNoTracking().ToListAsync())
        {
            var sum = await db.Movements.AsNoTracking()
                .Where(m => m.AccountId == account.Id)
                .SumAsync(m => m.Type == MovementType.Credit ? m.Amount : -m.Amount);
            Assert.Equal(sum, account.Balance);
        }
    }

    [Fact]
    public async Task AccountRepository_GetByCpf_FindsSeededAccount()
    {
        await using var db = CreateContext();
        DbInitializer.Seed(db);
        var repository = new AccountRepository(db);

        var account = await repository.GetByCpfAsync("111.111.111-11");

        Assert.NotNull(account);
        Assert.Equal("Ana Teste", account!.Name);
    }

    [Fact]
    public async Task AccountRepository_GetById_ReturnsNullWhenMissing()
    {
        await using var db = CreateContext();
        var repository = new AccountRepository(db);

        var account = await repository.GetByIdAsync(999);

        Assert.Null(account);
    }

    [Fact]
    public void DuplicateCpf_ThrowsDbUpdateException()
    {
        using var db = CreateContext();
        db.Accounts.Add(Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash"));
        db.Accounts.Add(Account.Create("Outra Ana", "111.111.111-11", AccountType.Checking, "hash"));

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task MovementRepository_AddAndPaginate_OrdersByTimestampDescending()
    {
        await using var db = CreateContext();
        DbInitializer.Seed(db);
        var account = Account.Create("Nova Conta", "999.999.999-99", AccountType.Checking, "hash");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var repository = new MovementRepository(db);

        for (var i = 0; i < 5; i++)
        {
            var movement = Movement.Create(account.Id, MovementType.Credit, 10m + i).Value!;
            await repository.AddAsync(movement);
        }

        await repository.SaveChangesAsync();

        var (items, total) = await repository.GetByAccountIdAsync(account.Id, page: 1, pageSize: 2);

        Assert.Equal(5, total);
        Assert.Equal(2, items.Count);
        Assert.True(items[0].Timestamp >= items[1].Timestamp);
    }

    [Fact]
    public async Task IdempotencyRecord_IsStoredByKey()
    {
        await using var db = CreateContext();
        db.IdempotencyRecords.Add(IdempotencyRecord.Create("key-1", "/accounts", "hash", 201, "{}"));
        await db.SaveChangesAsync();

        var record = await db.IdempotencyRecords.FindAsync("key-1");

        Assert.NotNull(record);
        Assert.Equal("/accounts", record!.RequestPath);
        Assert.Equal(201, record.ResponseStatusCode);
    }

    [Fact]
    public async Task AuditLog_IsStored()
    {
        await using var db = CreateContext();
        db.AuditLogs.Add(AuditLog.Create("Account", "1", "create", "{}"));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task IdempotencyRepository_AddsAndFindsByKey()
    {
        await using var db = CreateContext();
        var repository = new IdempotencyRepository(db);

        await repository.AddAsync(IdempotencyRecord.Create("key-1", "/accounts/1/movements", "hash", 201, "{}"));
        await repository.SaveChangesAsync();

        var record = await repository.GetByKeyAsync("key-1");
        Assert.NotNull(record);
        Assert.Equal(201, record!.ResponseStatusCode);
    }

    [Fact]
    public async Task AuditLogRepository_AddsLog()
    {
        await using var db = CreateContext();
        var repository = new AuditLogRepository(db);

        await repository.AddAsync(AuditLog.Create("Account", "1", "create", "{}"));
        await repository.SaveChangesAsync();

        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Avatar_PersistsAndReloads()
    {
        await using var db = CreateContext();
        var account = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, "hash");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        account.SetAvatar(new byte[] { 1, 2, 3 }, "image/jpeg");
        await db.SaveChangesAsync();

        var reloaded = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, reloaded.Avatar);
        Assert.Equal("image/jpeg", reloaded.AvatarContentType);
    }
}
