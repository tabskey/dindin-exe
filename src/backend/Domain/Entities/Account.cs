using Domain.Movements;
using Domain.Results;

namespace Domain.Entities;

public class Account
{
    public long Id { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Cpf { get; private set; } = string.Empty;
    public AccountType AccountType { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Account() { } // EF Core

    public static Account Create(string name, string cpf, AccountType accountType, string passwordHash)
    {
        return new Account
        {
            AccountNumber = GenerateAccountNumber(),
            Name = name,
            Cpf = cpf,
            AccountType = accountType,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };
    }

    public Result ApplyMovement(IMovementStrategy strategy, decimal amount) => strategy.Apply(this, amount);

    internal void SetBalance(decimal balance) => Balance = balance;

    private static string GenerateAccountNumber() =>
        $"A{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}
