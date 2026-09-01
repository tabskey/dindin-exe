using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            account.Property(a => a.Id).ValueGeneratedOnAdd();
            account.Property(a => a.AccountNumber).HasMaxLength(20).IsRequired();
            account.Property(a => a.Name).HasMaxLength(100).IsRequired();
            account.Property(a => a.Cpf).HasMaxLength(14).IsRequired();
            account.HasIndex(a => a.AccountNumber).IsUnique();
            account.HasIndex(a => a.Cpf).IsUnique();
            account.Property(a => a.PasswordHash).IsRequired();
            account.Property(a => a.Balance);
            account.Property(a => a.AvatarContentType).HasMaxLength(50);
            // SQLite não gera rowversion nativamente; o valor é atribuído pelo RowVersionInterceptor.
            account.Property(a => a.RowVersion).IsConcurrencyToken();
            account.HasMany<Movement>().WithOne().HasForeignKey(m => m.AccountId);
        });

        modelBuilder.Entity<Movement>(movement =>
        {
            movement.HasKey(m => m.Id);
            movement.Property(m => m.Id).ValueGeneratedOnAdd();
            movement.Property(m => m.Amount);
            movement.Property(m => m.Counterparty).HasMaxLength(120);
            movement.HasIndex(m => m.AccountId);
        });

        modelBuilder.Entity<AuditLog>(log =>
        {
            log.HasKey(l => l.Id);
            log.Property(l => l.Id).ValueGeneratedOnAdd();
            log.Property(l => l.EntityType).HasMaxLength(50).IsRequired();
            log.Property(l => l.EntityId).HasMaxLength(50).IsRequired();
            log.Property(l => l.Action).HasMaxLength(50).IsRequired();
            log.Property(l => l.Payload).HasColumnType("TEXT").IsRequired();
        });

        modelBuilder.Entity<IdempotencyRecord>(record =>
        {
            record.HasKey(r => r.Key);
            record.Property(r => r.Key).HasMaxLength(100);
            record.Property(r => r.RequestPath).HasMaxLength(200).IsRequired();
            record.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
            record.Property(r => r.ResponseBody).HasColumnType("TEXT").IsRequired();
        });
    }
}
