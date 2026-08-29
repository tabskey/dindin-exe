using Application.Abstractions;
using Application.Dtos;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Results;

namespace Application.Services;

public sealed class AccountService : IAccountService
{
    private const int MinPasswordLength = 6;
    private const int MaxAvatarBytes = 512 * 1024;
    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly IAccountRepository _accounts;

    public AccountService(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.WeakPassword, $"Password must have at least {MinPasswordLength} characters."));
        }

        var cpf = request.Cpf.Trim();
        if (await _accounts.GetByCpfAsync(cpf, cancellationToken) is not null)
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.CpfAlreadyRegistered, "This CPF is already registered."));
        }

        var account = Account.Create(request.Name.Trim(), cpf, request.AccountType, BC.HashPassword(request.Password));
        await _accounts.AddAsync(account, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);

        return Result<AccountDto>.Success(ToDto(account));
    }

    public async Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByCpfAsync(request.Cpf.Trim(), cancellationToken);
        if (account is null || !BC.Verify(request.Password, account.PasswordHash))
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidCredentials, "Invalid CPF or password."));
        }

        return Result<AccountDto>.Success(ToDto(account));
    }

    public async Task<Result<BalanceDto>> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result<BalanceDto>.Failure(
                new DomainError(DomainErrorCode.AccountNotFound, "Account not found."));
        }

        return Result<BalanceDto>.Success(new BalanceDto(account.Id, account.Balance));
    }

    public async Task<Result> UpdateAvatarAsync(long accountId, byte[] avatar, string contentType, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure(new DomainError(DomainErrorCode.AccountNotFound, "Account not found."));
        }

        if (avatar.Length == 0 || avatar.Length > MaxAvatarBytes || !AllowedAvatarContentTypes.Contains(contentType))
        {
            return Result.Failure(new DomainError(
                DomainErrorCode.InvalidAvatar,
                $"Avatar must be a JPEG, PNG or WebP image up to {MaxAvatarBytes / 1024} KB."));
        }

        account.SetAvatar(avatar, contentType);
        await _accounts.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AvatarDto>> GetAvatarAsync(long accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result<AvatarDto>.Failure(new DomainError(DomainErrorCode.AccountNotFound, "Account not found."));
        }

        if (account.Avatar is null || account.AvatarContentType is null)
        {
            return Result<AvatarDto>.Failure(new DomainError(DomainErrorCode.AvatarNotFound, "This account has no avatar."));
        }

        return Result<AvatarDto>.Success(new AvatarDto(account.Avatar, account.AvatarContentType));
    }

    private static AccountDto ToDto(Account account) =>
        new(account.Id, account.AccountNumber, account.Name, account.Cpf, account.AccountType, account.CreatedAt);
}
