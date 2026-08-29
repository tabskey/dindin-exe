using Domain.Entities;

namespace Application.Abstractions;

public interface IMovementRepository
{
    Task AddAsync(Movement movement, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Movement> Items, int Total)> GetByAccountIdAsync(
        long accountId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
