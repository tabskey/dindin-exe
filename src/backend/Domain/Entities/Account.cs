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
    public long Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public byte[]? Avatar { get; private set; }
    public string? AvatarContentType { get; private set; }

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

    public Result ApplyMovement(IMovementStrategy strategy, long amount) => strategy.Apply(this, amount);

    internal void SetId(long id) => Id = id;

    internal void SetAccountNumber(string accountNumber) => AccountNumber = accountNumber;

    internal void SetBalance(long balance) => Balance = balance;

    public void SetAvatar(byte[] avatar, string contentType)
    {
        Avatar = avatar;
        AvatarContentType = contentType;
    }

    // Formato serial: 00xxx-xx (ex.: 00123-45).
    private static string GenerateAccountNumber() =>
        $"00{Random.Shared.Next(0, 1000):000}-{Random.Shared.Next(0, 100):00}";
}
