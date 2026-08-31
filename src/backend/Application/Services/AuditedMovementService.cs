using System.Text.Json;
using Application.Abstractions;
using Application.Dtos;
using Domain.Entities;
using Domain.Results;

namespace Application.Services;

public sealed class AuditedMovementService : IMovementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMovementService _inner;
    private readonly IAuditLogRepository _audit;

    public AuditedMovementService(IMovementService inner, IAuditLogRepository audit)
    {
        _inner = inner;
        _audit = audit;
    }

    public async Task<Result<MovementDto>> CreateAsync(long accountId, CreateMovementRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _inner.CreateAsync(accountId, request, cancellationToken);
        if (result.IsSuccess)
        {
            var payload = JsonSerializer.Serialize(new
            {
                accountId,
                request.Type,
                request.Amount,
                counterparty = result.Value!.Counterparty
            }, JsonOptions);
            await _audit.AddAsync(
                AuditLog.Create("Movement", result.Value!.Id.ToString(), "create", payload),
                cancellationToken);
            await _audit.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public Task<Result<MovementHistoryDto>> GetHistoryAsync(long accountId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        _inner.GetHistoryAsync(accountId, page, pageSize, cancellationToken);
}
