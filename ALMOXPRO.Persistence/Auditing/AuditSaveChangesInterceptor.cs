using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace ALMOXPRO.Persistence.Auditing;

/// <summary>
/// Gera registros de auditoria para toda inserção, alteração e exclusão,
/// e preenche os campos CreatedBy/UpdatedBy das entidades.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentSession _session;
    private readonly IMachineInfoProvider _machine;

    public AuditSaveChangesInterceptor(ICurrentSession session, IMachineInfoProvider machine)
    {
        _session = session;
        _machine = machine;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null)
            ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void ApplyAudit(DbContext context)
    {
        var now = DateTime.UtcNow;
        var userName = _session.IsAuthenticated ? _session.UserName : "sistema";
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    baseEntity.CreatedAt = now;
                    baseEntity.CreatedBy = userName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAt = now;
                    baseEntity.UpdatedBy = userName;
                }
            }

            logs.Add(BuildLog(entry, userName, now));
        }

        if (logs.Count > 0)
            context.Set<AuditLog>().AddRange(logs);
    }

    private AuditLog BuildLog(EntityEntry entry, string userName, DateTime now)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            // Fotos/assinaturas não vão para o log.
            if (property.Metadata.ClrType == typeof(byte[]))
                continue;

            var name = property.Metadata.Name;
            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = property.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[name] = property.OriginalValue;
                    break;
                case EntityState.Modified when property.IsModified:
                    oldValues[name] = property.OriginalValue;
                    newValues[name] = property.CurrentValue;
                    break;
            }
        }

        var keyValues = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "?");

        return new AuditLog
        {
            UserId = _session.UserId,
            UserName = userName,
            Computer = _machine.ComputerName,
            IpAddress = _machine.IpAddress,
            OccurredAt = now,
            Screen = string.Empty,
            Action = entry.State switch
            {
                EntityState.Added => "Inserção",
                EntityState.Deleted => "Exclusão",
                _ => "Alteração"
            },
            TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            RecordId = string.Join(",", keyValues),
            OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null
        };
    }
}
