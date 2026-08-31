using BC = BCrypt.Net.BCrypt;
using Domain.Entities;
using Domain.Movements;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public static class DbInitializer
{
    public static void Initialize(AppDbContext db)
    {
        db.Database.Migrate();
        Seed(db);
    }

    // Dados iniciais de teste (idempotente): só popula se não houver contas.
    // Os saldos são mantidos consistentes aplicando as movimentações pelas
    // strategies do domínio (um débito que zeraria o saldo é um caso de borda
    // deliberado: saldo 0 é permitido, negativo nunca). Todas as contas são
    // correntes (CC); as movimentações trazem contrapartes de exemplo —
    // auto-depósito (boca do caixa) ou transferência entre as contas do seed.
    public static void Seed(AppDbContext db)
    {
        if (db.Accounts.Any())
        {
            return;
        }

        var ana = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, BC.HashPassword("senha123"));
        var bruno = Account.Create("Bruno Teste", "222.222.222-22", AccountType.Checking, BC.HashPassword("senha123"));
        var carlos = Account.Create("Carlos Teste", "333.333.333-33", AccountType.Checking, BC.HashPassword("senha123"));

        db.Accounts.AddRange(ana, bruno, carlos);
        db.SaveChanges();

        AddMovement(db, ana, MovementType.Credit, 100000, CounterpartyLabel.AutoDeposit(ana));
        AddMovement(db, ana, MovementType.Credit, 50000, CounterpartyLabel.For(carlos));
        AddMovement(db, ana, MovementType.Debit, 30000, CounterpartyLabel.For(bruno));
        AddMovement(db, ana, MovementType.Debit, 15000, CounterpartyLabel.For(carlos));

        AddMovement(db, bruno, MovementType.Credit, 20000, CounterpartyLabel.For(ana));
        AddMovement(db, bruno, MovementType.Debit, 12000, CounterpartyLabel.For(carlos));

        AddMovement(db, carlos, MovementType.Credit, 10000, CounterpartyLabel.For(ana));
        AddMovement(db, carlos, MovementType.Debit, 10000, CounterpartyLabel.For(bruno));

        db.SaveChanges();
    }

    private static void AddMovement(AppDbContext db, Account account, MovementType type, long amount, string? counterparty = null)
    {
        var movement = Movement.Create(account.Id, type, amount, counterparty).Value!;
        account.ApplyMovement(MovementStrategies.For(type), amount);
        db.Movements.Add(movement);
    }
}
