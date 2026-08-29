using Application.Dtos;
using Domain.Results;

namespace Application.Services;

public interface IMovementService
{
    Task<Result<MovementDto>> CreateAsync(long accountId, CreateMovementRequest request, CancellationToken cancellationToken = default);
    Task<Result<MovementHistoryDto>> GetHistoryAsync(long accountId, int page, int pageSize, CancellationToken cancellationToken = default);
}
