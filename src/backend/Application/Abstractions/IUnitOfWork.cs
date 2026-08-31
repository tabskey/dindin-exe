namespace Application.Abstractions;

// Permite delimitar uma unidade de trabalho transacional nos pontos de escrita da API
// (o filtro de idempotência abre/commita a transação; serviços e decorators apenas
// adicionam e chamam SaveChanges dentro dela).
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
