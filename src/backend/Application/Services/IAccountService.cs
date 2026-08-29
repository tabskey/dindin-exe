using Application.Dtos;
using Domain.Results;

namespace Application.Services;

public interface IAccountService
{
    Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<BalanceDto>> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default);
}
