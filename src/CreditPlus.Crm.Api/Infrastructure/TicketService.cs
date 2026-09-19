using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class TicketService(CrmDbContext db, ICurrentUser currentUser, IAuditLog audit) : ITicketService
{
    /// <summary>Hours allowed to resolve a ticket, by priority.</summary>
    private static readonly Dictionary<string, int> ResponseHours = new(StringComparer.OrdinalIgnoreCase)
    {
        [TicketPriorities.Urgent] = 4,
        [TicketPriorities.High] = 8,
        [TicketPriorities.Normal] = 24,
        [TicketPriorities.Low] = 72,
    };

    public IReadOnlyList<Ticket> GetTickets(string? status, string? priority, Guid? leadId, string? search)
    {
        var query = db.Tickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = TicketStatuses.Normalize(status);
            query = query.Where(ticket => ticket.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            var wanted = TicketPriorities.Normalize(priority);
            query = query.Where(ticket => ticket.Priority == wanted);
        }

        if (leadId.HasValue)
        {
            query = query.Where(ticket => ticket.LeadId == leadId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(ticket =>
                EF.Functions.ILike(ticket.Reference, term) ||
                EF.Functions.ILike(ticket.Subject, term) ||
                EF.Functions.ILike(ticket.CustomerName, term));
        }

        return query
            .OrderBy(ticket => ticket.Status == TicketStatuses.Closed || ticket.Status == TicketStatuses.Resolved)
            .ThenBy(ticket => ticket.DueAt)
            .ToList()
            .Select(ticket => ticket.ToRecord())
            .ToList();
    }

    public Ticket? FindTicket(Guid id) =>
        db.Tickets.AsNoTracking().FirstOrDefault(ticket => ticket.Id == id)?.ToRecord();

    public Ticket Create(CreateTicketRequest request)
    {
        var customerName = Text(request.CustomerName);

        if (request.LeadId is { } leadId)
        {
            var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == leadId)
                ?? throw new KeyNotFoundException($"Lead {leadId} was not found.");
            customerName = lead.FullName;
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new CrmValidationException("Customer name is required when no customer is linked.");
        }

        var priority = ValidPriority(request.Priority);
        var now = DateTimeOffset.UtcNow;

        var entity = new TicketEntity
        {
            Id = Guid.NewGuid(),
            Reference = NextReference("TK"),
            LeadId = request.LeadId,
            CustomerName = customerName,
            Subject = Required(request.Subject, "Subject"),
            Description = Required(request.Description, "Description"),
            Category = ValidCategory(request.Category),
            Priority = priority,
            Status = TicketStatuses.Open,
            AssignedTo = currentUser.IsAuthenticated ? currentUser.Name : "Unassigned",
            CreatedAt = now,
            DueAt = now.AddHours(ResponseHours[priority]),
        };

        db.Tickets.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "Ticket", entity.Id.ToString(),
            $"Opened {entity.Reference}: {entity.Subject}",
            new { entity.Reference, entity.Category, entity.Priority, entity.CustomerName });

        return entity.ToRecord();
    }

    public Ticket Update(Guid id, UpdateTicketRequest request)
    {
        var entity = db.Tickets.FirstOrDefault(ticket => ticket.Id == id)
            ?? throw new KeyNotFoundException($"Ticket {id} was not found.");

        var before = new { entity.Subject, entity.Priority, entity.Status, entity.AssignedTo };
        var status = ValidStatus(request.Status);
        var priority = ValidPriority(request.Priority);

        if (status is TicketStatuses.Resolved or TicketStatuses.Closed
            && string.IsNullOrWhiteSpace(request.Resolution)
            && string.IsNullOrWhiteSpace(entity.Resolution))
        {
            throw new CrmValidationException("A resolution note is required before resolving or closing a ticket.");
        }

        // Changing priority moves the target, measured from when the ticket was raised.
        if (priority != entity.Priority)
        {
            entity.DueAt = entity.CreatedAt.AddHours(ResponseHours[priority]);
        }

        entity.Subject = Required(request.Subject, "Subject");
        entity.Description = Required(request.Description, "Description");
        entity.Category = ValidCategory(request.Category);
        entity.Priority = priority;
        entity.Status = status;
        entity.AssignedTo = Text(request.AssignedTo, entity.AssignedTo);
        entity.Resolution = Text(request.Resolution, entity.Resolution ?? "");

        if (status is TicketStatuses.Resolved or TicketStatuses.Closed)
        {
            entity.ResolvedAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            // Reopening clears the resolution timestamp so SLA is measured again.
            entity.ResolvedAt = null;
        }

        db.SaveChanges();

        audit.Record(AuditActions.Updated, "Ticket", entity.Id.ToString(),
            $"Updated {entity.Reference}",
            new { before, after = new { entity.Subject, entity.Priority, entity.Status, entity.AssignedTo } });

        return entity.ToRecord();
    }

    public bool Delete(Guid id)
    {
        var entity = db.Tickets.FirstOrDefault(ticket => ticket.Id == id);
        if (entity is null)
        {
            return false;
        }

        db.Tickets.Remove(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Deleted, "Ticket", id.ToString(), $"Deleted {entity.Reference}");
        return true;
    }

    private string NextReference(string prefix)
    {
        var year = DateTime.UtcNow.Year;
        var stem = $"{prefix}-{year}-";
        var last = db.Tickets
            .Where(ticket => ticket.Reference.StartsWith(stem))
            .OrderByDescending(ticket => ticket.Reference)
            .Select(ticket => ticket.Reference)
            .FirstOrDefault();

        var next = 1;
        if (last is not null && int.TryParse(last[stem.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{stem}{next:D4}";
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new CrmValidationException($"{field} is required.")
            : value.Trim();

    private static string Text(string? value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string ValidStatus(string? value)
    {
        var status = Text(value, TicketStatuses.Open);
        return TicketStatuses.IsKnown(status)
            ? TicketStatuses.Normalize(status)
            : throw new CrmValidationException($"Status must be one of: {string.Join(", ", TicketStatuses.All)}.");
    }

    private static string ValidPriority(string? value)
    {
        var priority = Text(value, TicketPriorities.Normal);
        return TicketPriorities.IsKnown(priority)
            ? TicketPriorities.Normalize(priority)
            : throw new CrmValidationException(
                $"Priority must be one of: {string.Join(", ", TicketPriorities.All)}.");
    }

    private static string ValidCategory(string? value)
    {
        var category = Text(value, "Other");
        return TicketCategories.All.Contains(category, StringComparer.OrdinalIgnoreCase)
            ? TicketCategories.All.First(known => known.Equals(category, StringComparison.OrdinalIgnoreCase))
            : throw new CrmValidationException(
                $"Category must be one of: {string.Join(", ", TicketCategories.All)}.");
    }
}
