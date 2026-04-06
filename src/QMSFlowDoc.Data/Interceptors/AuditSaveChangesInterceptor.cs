using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QMSFlowDoc.Shared.Models;
using System.Text.Json;

namespace QMSFlowDoc.Data.Interceptors;

/// <summary>
/// EF Core interceptor that automatically logs audit entries to AuditLogs table
/// on every SaveChanges call. Captures entity changes, old/new values, and user info.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly string _currentUser;
    private readonly string _machineName;

    public AuditSaveChangesInterceptor(string currentUser)
    {
        _currentUser = currentUser;
        _machineName = Environment.MachineName;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = eventData.Context;
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog) // Don't audit the audit log itself
            .ToList();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString();

            string action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => "UNKNOWN"
            };

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                var changedProps = entry.Properties
                    .Where(p => p.IsModified && p.Metadata.Name != "RowVersion")
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString());
                oldValues = JsonSerializer.Serialize(changedProps);

                var newProps = entry.Properties
                    .Where(p => p.IsModified && p.Metadata.Name != "RowVersion")
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString());
                newValues = JsonSerializer.Serialize(newProps);
            }
            else if (entry.State == EntityState.Added)
            {
                var allProps = entry.Properties
                    .Where(p => p.Metadata.Name != "RowVersion")
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString());
                newValues = JsonSerializer.Serialize(allProps);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var allProps = entry.Properties
                    .Where(p => p.Metadata.Name != "RowVersion")
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString());
                oldValues = JsonSerializer.Serialize(allProps);
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = null, // Will be set by caller if needed
                UserName = _currentUser,
                Action = action,
                EntityType = entityName,
                EntityId = entityId != null ? Guid.Parse(entityId) : null,
                Details = $"{action} on {entityName}",
                BeforeSnapshot = oldValues,
                AfterSnapshot = newValues,
                MachineName = _machineName,
                Result = "OK"
            };

            context.Set<AuditLog>().Add(auditLog);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
