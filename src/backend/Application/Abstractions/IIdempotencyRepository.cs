using Domain.Entities;

namespace Application.Abstractions;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
