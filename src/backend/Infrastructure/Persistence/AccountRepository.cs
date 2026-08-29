using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db) => _db = db;

    public async Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Account?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default) =>
        await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Cpf == cpf, cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
        await _db.Accounts.AddAsync(account, cancellationToken);

    public async Task ReloadAsync(Account account, CancellationToken cancellationToken = default) =>
        await _db.Entry(account).ReloadAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
