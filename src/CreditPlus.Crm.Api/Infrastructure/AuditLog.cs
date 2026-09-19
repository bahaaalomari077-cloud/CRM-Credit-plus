using System.Text.Json;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Append-only trail. Writes go through their own DbContext so an audit row is never
/// lost to a rollback in the caller's context, and a failure to audit never breaks
/// the request it describes — it is logged instead.
/// </summary>
public sealed class AuditLog(
    CrmDbContext db,
    IDbContextFactory<CrmDbContext> contextFactory,
    ICurrentUser currentUser,
    ILogger<AuditLog> logger) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Record(string action, string entityType, string entityId, string? summary = null, object? changes = null)
    {
        try
        {
            using var auditDb = contextFactory.CreateDbContext();
            auditDb.AuditEntries.Add(new AuditEntryEntity
            {
                OccurredAt = DateTimeOffset.UtcNow,
                UserId = currentUser.Id,
                UserEmail = currentUser.Email,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = Truncate(summary, 400),
                Changes = changes is null ? null : JsonSerializer.Serialize(changes, JsonOptions),
                IpAddress = currentUser.IpAddress,
            });
            auditDb.SaveChanges();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to write audit entry for {Action} on {EntityType} {EntityId}",
                action, entityType, entityId);
        }
    }

    public IReadOnlyList<AuditEntry> Query(string? entityType, string? entityId, string? action, int limit)
    {
        var query = db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(audit => audit.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(audit => audit.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(audit => audit.Action == action);
        }

        return query
            .OrderByDescending(audit => audit.OccurredAt)
            .ThenByDescending(audit => audit.Id)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(audit => audit.ToRecord())
            .ToList();
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
