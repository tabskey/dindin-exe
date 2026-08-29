using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly AppDbContext _db;

    public IdempotencyRepository(AppDbContext db) => _db = db;

    public async Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        await _db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default) =>
        await _db.IdempotencyRecords.AddAsync(record, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
