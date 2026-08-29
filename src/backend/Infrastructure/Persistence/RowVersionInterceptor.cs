using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence;

public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AssignRowVersions(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AssignRowVersions(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AssignRowVersions(DbContextEventData eventData)
    {
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<Account>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(a => a.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
