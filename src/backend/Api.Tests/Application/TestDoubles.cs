using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Application;

internal sealed class FakeAccountRepository : IAccountRepository
{
    public List<Account> Accounts { get; } = new();
    public Action? OnReload { get; set; }
    public int AccountNumberCollisionsRemaining { get; set; }

    public Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<Account?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Cpf == cpf));

    public Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Collide() ? Accounts.FirstOrDefault() : Accounts.FirstOrDefault(a => a.AccountNumber == accountNumber));

    private bool Collide()
    {
        if (AccountNumberCollisionsRemaining <= 0)
        {
            return false;
        }

        AccountNumberCollisionsRemaining--;
        return true;
    }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        Accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task ReloadAsync(Account account, CancellationToken cancellationToken = default)
    {
        OnReload?.Invoke();
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeMovementRepository : IMovementRepository
{
    public List<Movement> Movements { get; } = new();
    public int SaveCallCount { get; private set; }
    public int ConcurrencyFailuresRemaining { get; set; }

    public Task AddAsync(Movement movement, CancellationToken cancellationToken = default)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Movement> Items, int Total)> GetByAccountIdAsync(
        long accountId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var matching = Movements.Where(m => m.AccountId == accountId)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Movement>, int)>((items, matching.Count));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        if (ConcurrencyFailuresRemaining > 0)
        {
            ConcurrencyFailuresRemaining--;
            throw new DbUpdateConcurrencyException("Simulated concurrent update.");
        }

        return 1;
    }
}

internal sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Logs { get; } = new();

    public Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        Logs.Add(log);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeIdempotencyRepository : IIdempotencyRepository
{
    public List<IdempotencyRecord> Records { get; } = new();

    public Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Key == key));

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}
