using Application.Abstractions;
using Application.Dtos;
using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Results;

namespace Application.Services;

public sealed class AccountService : IAccountService
{
    private const int MinPasswordLength = 6;
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

    private static AccountDto ToDto(Account account) =>
        new(account.Id, account.AccountNumber, account.Name, account.Cpf, account.AccountType, account.CreatedAt);
}
