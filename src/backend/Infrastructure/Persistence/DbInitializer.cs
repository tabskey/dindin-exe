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
    // deliberado: saldo 0 é permitido, negativo nunca).
    public static void Seed(AppDbContext db)
    {
        if (db.Accounts.Any())
        {
            return;
        }

        var ana = Account.Create("Ana Teste", "111.111.111-11", AccountType.Checking, BC.HashPassword("senha123"));
        var bruno = Account.Create("Bruno Teste", "222.222.222-22", AccountType.Savings, BC.HashPassword("senha123"));
        var carlos = Account.Create("Carlos Teste", "333.333.333-33", AccountType.Checking, BC.HashPassword("senha123"));

        db.Accounts.AddRange(ana, bruno, carlos);
        db.SaveChanges();

        AddMovement(db, ana, MovementType.Credit, 1000m);
        AddMovement(db, ana, MovementType.Credit, 500m);
        AddMovement(db, ana, MovementType.Debit, 300m);
        AddMovement(db, ana, MovementType.Debit, 150m);

        AddMovement(db, bruno, MovementType.Credit, 200m);
        AddMovement(db, bruno, MovementType.Debit, 120m);

        AddMovement(db, carlos, MovementType.Credit, 100m);
        AddMovement(db, carlos, MovementType.Debit, 100m);

        db.SaveChanges();
    }

    private static void AddMovement(AppDbContext db, Account account, MovementType type, decimal amount)
    {
        var movement = Movement.Create(account.Id, type, amount).Value!;
        account.ApplyMovement(MovementStrategies.For(type), amount);
        db.Movements.Add(movement);
    }
}
