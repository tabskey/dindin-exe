using Application.Abstractions;
using Application.Dtos;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Results;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class AccountService : IAccountService
{
    private const int MinPasswordLength = 6;
    private const int MaxAccountNumberAttempts = 5;
    public const int MaxAvatarBytes = 512 * 1024;
    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly IAccountRepository _accounts;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IAccountRepository accounts, ILogger<AccountService> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Cpf))
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "Name and CPF are required."));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.WeakPassword, $"Password must have at least {MinPasswordLength} characters."));
        }

        if (!Enum.IsDefined(typeof(AccountType), request.AccountType))
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "Invalid account type."));
        }

        var cpf = request.Cpf.Trim();
        if (cpf.Count(char.IsDigit) != 11)
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "CPF must have exactly 11 digits."));
        }

        // Validação de formato consistente com o que é armazenado: só dígitos (e a máscara
        // 000.000.000-00). Um CPF com 11 dígitos e caracteres extras (ex.: 111.111.111-11x)
        // não pode ser gravado cru.
        if (cpf.Any(c => !char.IsDigit(c) && c is not '.' and not '-'))
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "CPF must contain only digits (000.000.000-00)."));
        }

        if (await _accounts.GetByCpfAsync(cpf, cancellationToken) is not null)
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.CpfAlreadyRegistered, "This CPF is already registered."));
        }

        var name = request.Name.Trim();
        var passwordHash = BC.HashPassword(request.Password);
        var account = Account.Create(name, cpf, request.AccountType, passwordHash);
        // O número é aleatório (00xxx-xx): pré-valida a unicidade com retry antes do INSERT.
        // O índice único no banco é o backstop — a corrida residual vira 503 tratado no filtro.
        for (var attempt = 1; await _accounts.GetByAccountNumberAsync(account.AccountNumber, cancellationToken) is not null; attempt++)
        {
            if (attempt >= MaxAccountNumberAttempts)
            {
                return Result<AccountDto>.Failure(
                    new DomainError(DomainErrorCode.AccountNumberCollision, "Could not allocate a unique account number. Please retry."));
            }

            account = Account.Create(name, cpf, request.AccountType, passwordHash);
        }

        await _accounts.AddAsync(account, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account created: id={Id}, cpf={Cpf}", account.Id, account.Cpf);
        return Result<AccountDto>.Success(ToDto(account));
    }

    public async Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Cpf) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AccountDto>.Failure(
                new DomainError(DomainErrorCode.InvalidCredentials, "Invalid CPF or password."));
        }

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
