using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Project.Test.Helpers;

/// <summary>
/// EF Core's InMemory provider does not support automatic RowVersion generation, so we need to do it manually.
/// </summary>
public class RowVersionInterceptor : ISaveChangesInterceptor
{
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        SetRowVersion(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void SetRowVersion(DbContext? context)
    {
        if (context == null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Modified)
            {
                var property = entry.Properties.FirstOrDefault(p => p.Metadata.IsConcurrencyToken);
                if (property != null)
                {
                    property.CurrentValue = Guid.NewGuid().ToByteArray();
                }
            }
        }
    }
}
