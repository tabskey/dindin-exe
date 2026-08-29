using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class MovementRepository : IMovementRepository
{
    private readonly AppDbContext _db;

    public MovementRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Movement movement, CancellationToken cancellationToken = default) =>
        await _db.Movements.AddAsync(movement, cancellationToken);

    public async Task<(IReadOnlyList<Movement> Items, int Total)> GetByAccountIdAsync(
        long accountId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.Movements.AsNoTracking().Where(m => m.AccountId == accountId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
