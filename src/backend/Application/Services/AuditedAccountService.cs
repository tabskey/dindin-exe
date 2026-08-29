using System.Text.Json;
using Application.Abstractions;
using Application.Dtos;
using Domain.Entities;
using Domain.Results;

namespace Application.Services;

public sealed class AuditedAccountService : IAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAccountService _inner;
    private readonly IAuditLogRepository _audit;

    public AuditedAccountService(IAccountService inner, IAuditLogRepository audit)
    {
        _inner = inner;
        _audit = audit;
    }

    public async Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _inner.CreateAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            await _audit.AddAsync(
                AuditLog.Create("Account", result.Value!.Id.ToString(), "create", JsonSerializer.Serialize(request, JsonOptions)),
                cancellationToken);
            await _audit.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        _inner.LoginAsync(request, cancellationToken);

    public Task<Result<BalanceDto>> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default) =>
        _inner.GetBalanceAsync(accountId, cancellationToken);

    public Task<Result> UpdateAvatarAsync(long accountId, byte[] avatar, string contentType, CancellationToken cancellationToken = default) =>
        _inner.UpdateAvatarAsync(accountId, avatar, contentType, cancellationToken);

    public Task<Result<AvatarDto>> GetAvatarAsync(long accountId, CancellationToken cancellationToken = default) =>
        _inner.GetAvatarAsync(accountId, cancellationToken);
}
