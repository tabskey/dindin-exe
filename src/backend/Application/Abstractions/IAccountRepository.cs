using Domain.Entities;

namespace Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Account?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    Task ReloadAsync(Account account, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
