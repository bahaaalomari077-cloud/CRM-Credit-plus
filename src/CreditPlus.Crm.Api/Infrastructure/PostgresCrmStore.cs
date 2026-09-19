using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>Stores the CRM in PostgreSQL through EF Core.</summary>
public sealed class PostgresCrmStore(CrmDbContext db, ICurrentUser currentUser, IAuditLog audit) : ICrmStore
{
    /// <summary>New records belong to whoever created them.</summary>
    private string Owner => currentUser.IsAuthenticated ? currentUser.Name : "System";

    public IReadOnlyList<Lead> GetLeads(string? search = null, string? status = null)
    {
        var query = db.Leads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(lead => lead.Status.ToLower() == status.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(lead =>
                EF.Functions.ILike(lead.FullName, term) ||
                EF.Functions.ILike(lead.Company, term) ||
                EF.Functions.ILike(lead.Email, term) ||
                EF.Functions.ILike(lead.Phone, term));
        }

        return query
            .OrderByDescending(lead => lead.CreatedAt)
            .Select(lead => lead.ToRecord())
            .ToList();
    }

    public Lead? FindLead(Guid id) =>
        db.Leads.AsNoTracking().FirstOrDefault(lead => lead.Id == id)?.ToRecord();

    public Lead AddLead(CreateLeadRequest request)
    {
        var entity = new LeadEntity
        {
            Id = Guid.NewGuid(),
            FullName = Required(request.FullName, "Full name"),
            Company = Required(request.Company, "Company"),
            Email = ValidEmail(request.Email),
            Phone = Text(request.Phone),
            Source = Text(request.Source, "Website"),
            Status = LeadStatuses.New,
            Owner = Owner,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Leads.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "Lead", entity.Id.ToString(),
            $"Created lead {entity.FullName} ({entity.Company})",
            new { entity.FullName, entity.Company, entity.Email, entity.Source });

        return entity.ToRecord();
    }

    public Lead UpdateLead(Guid id, UpdateLeadRequest request)
    {
        var entity = db.Leads.FirstOrDefault(lead => lead.Id == id)
            ?? throw new KeyNotFoundException($"Lead {id} was not found.");

        var before = new { entity.FullName, entity.Company, entity.Email, entity.Phone, entity.Source, entity.Status };
        entity.FullName = Required(request.FullName, "Full name");
        entity.Company = Required(request.Company, "Company");
        entity.Email = ValidEmail(request.Email);
        entity.Phone = Text(request.Phone);
        entity.Source = Text(request.Source, entity.Source);
        entity.Status = LeadStatuses.Normalize(Text(request.Status, entity.Status));

        db.SaveChanges();

        audit.Record(AuditActions.Updated, "Lead", entity.Id.ToString(),
            $"Updated lead {entity.FullName}",
            new { before, after = new { entity.FullName, entity.Company, entity.Email, entity.Phone, entity.Source, entity.Status } });

        return entity.ToRecord();
    }

    public bool DeleteLead(Guid id)
    {
        var entity = db.Leads.AsNoTracking().FirstOrDefault(lead => lead.Id == id);
        if (entity is null || db.Leads.Where(lead => lead.Id == id).ExecuteDelete() == 0)
        {
            return false;
        }

        audit.Record(AuditActions.Deleted, "Lead", id.ToString(),
            $"Deleted lead {entity.FullName} ({entity.Company})");
        return true;
    }

    public IReadOnlyList<Deal> GetDeals(string? search = null, string? stage = null)
    {
        var query = db.Deals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(deal => deal.Stage.ToLower() == stage.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(deal =>
                EF.Functions.ILike(deal.Name, term) ||
                EF.Functions.ILike(deal.Company, term) ||
                EF.Functions.ILike(deal.Contact, term));
        }

        return query
            .OrderBy(deal => deal.ExpectedCloseDate)
            .ThenByDescending(deal => deal.Value)
            .Select(deal => deal.ToRecord())
            .ToList();
    }

    public Deal? FindDeal(Guid id) =>
        db.Deals.AsNoTracking().FirstOrDefault(deal => deal.Id == id)?.ToRecord();

    public Deal AddDeal(CreateDealRequest request)
    {
        var entity = new DealEntity
        {
            Id = Guid.NewGuid(),
            Name = Required(request.Name, "Deal name"),
            Company = Required(request.Company, "Company"),
            Contact = Text(request.Contact),
            Value = PositiveValue(request.Value),
            Stage = ValidStage(request.Stage, DealStages.Qualified),
            Probability = Math.Clamp(request.Probability, 0, 100),
            ExpectedCloseDate = request.ExpectedCloseDate == default
                ? DateOnly.FromDateTime(DateTime.Today.AddDays(14))
                : request.ExpectedCloseDate,
            Owner = Owner,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Deals.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "Deal", entity.Id.ToString(),
            $"Created deal {entity.Name} ({entity.Company})",
            new { entity.Name, entity.Company, entity.Value, entity.Stage });

        return entity.ToRecord();
    }

    public Deal UpdateDeal(Guid id, UpdateDealRequest request)
    {
        var entity = db.Deals.FirstOrDefault(deal => deal.Id == id)
            ?? throw new KeyNotFoundException($"Deal {id} was not found.");

        var before = new { entity.Name, entity.Company, entity.Value, entity.Stage, entity.Probability };
        entity.Name = Required(request.Name, "Deal name");
        entity.Company = Required(request.Company, "Company");
        entity.Contact = Text(request.Contact);
        entity.Value = PositiveValue(request.Value);
        entity.Stage = ValidStage(request.Stage, entity.Stage);
        entity.Probability = Math.Clamp(request.Probability, 0, 100);
        if (request.ExpectedCloseDate != default)
        {
            entity.ExpectedCloseDate = request.ExpectedCloseDate;
        }

        db.SaveChanges();

        audit.Record(AuditActions.Updated, "Deal", entity.Id.ToString(),
            $"Updated deal {entity.Name}",
            new { before, after = new { entity.Name, entity.Company, entity.Value, entity.Stage, entity.Probability } });

        return entity.ToRecord();
    }

    public Deal MoveDeal(Guid id, string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || !DealStages.IsKnown(stage))
        {
            throw new CrmValidationException($"Stage must be one of: {string.Join(", ", DealStages.All)}.");
        }

        var entity = db.Deals.FirstOrDefault(deal => deal.Id == id)
            ?? throw new KeyNotFoundException($"Deal {id} was not found.");

        var target = DealStages.Normalize(stage);
        var previousStage = entity.Stage;
        entity.Stage = target;
        // Closing a deal settles its probability either way, so the pipeline maths stay honest.
        entity.Probability = target switch
        {
            DealStages.Won => 100,
            DealStages.Lost => 0,
            _ => entity.Probability
        };

        db.SaveChanges();

        audit.Record(AuditActions.StageChanged, "Deal", entity.Id.ToString(),
            $"Moved {entity.Name} from {previousStage} to {target}",
            new { from = previousStage, to = target, entity.Value });

        return entity.ToRecord();
    }

    public bool DeleteDeal(Guid id)
    {
        var entity = db.Deals.AsNoTracking().FirstOrDefault(deal => deal.Id == id);
        if (entity is null || db.Deals.Where(deal => deal.Id == id).ExecuteDelete() == 0)
        {
            return false;
        }

        audit.Record(AuditActions.Deleted, "Deal", id.ToString(),
            $"Deleted deal {entity.Name} ({entity.Company})", new { entity.Value, entity.Stage });
        return true;
    }

    public IReadOnlyList<Activity> GetActivities(bool? completed = null)
    {
        var query = db.Activities.AsNoTracking();

        if (completed.HasValue)
        {
            query = query.Where(activity => activity.IsCompleted == completed.Value);
        }

        return query
            .OrderBy(activity => activity.IsCompleted)
            .ThenBy(activity => activity.DueAt)
            .Select(activity => activity.ToRecord())
            .ToList();
    }

    public Activity? FindActivity(Guid id) =>
        db.Activities.AsNoTracking().FirstOrDefault(activity => activity.Id == id)?.ToRecord();

    public Activity AddActivity(CreateActivityRequest request)
    {
        var entity = new ActivityEntity
        {
            Id = Guid.NewGuid(),
            Title = Required(request.Title, "Activity title"),
            Type = Text(request.Type, "Task"),
            RelatedTo = Text(request.RelatedTo),
            DueAt = request.DueAt == default ? DateTimeOffset.Now.AddHours(1) : request.DueAt,
            IsCompleted = false,
            Owner = Owner,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Activities.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "Activity", entity.Id.ToString(),
            $"Created task {entity.Title}", new { entity.Title, entity.Type, entity.RelatedTo, entity.DueAt });

        return entity.ToRecord();
    }

    public Activity UpdateActivity(Guid id, UpdateActivityRequest request)
    {
        var entity = db.Activities.FirstOrDefault(activity => activity.Id == id)
            ?? throw new KeyNotFoundException($"Activity {id} was not found.");

        var before = new { entity.Title, entity.Type, entity.RelatedTo, entity.DueAt, entity.IsCompleted };
        entity.Title = Required(request.Title, "Activity title");
        entity.Type = Text(request.Type, entity.Type);
        entity.RelatedTo = Text(request.RelatedTo);
        if (request.DueAt != default)
        {
            entity.DueAt = request.DueAt;
        }
        entity.IsCompleted = request.IsCompleted;

        db.SaveChanges();

        audit.Record(AuditActions.Updated, "Activity", entity.Id.ToString(),
            $"Updated task {entity.Title}",
            new { before, after = new { entity.Title, entity.Type, entity.RelatedTo, entity.DueAt, entity.IsCompleted } });

        return entity.ToRecord();
    }

    public Activity SetActivityCompletion(Guid id, bool isCompleted)
    {
        var entity = db.Activities.FirstOrDefault(activity => activity.Id == id)
            ?? throw new KeyNotFoundException($"Activity {id} was not found.");

        entity.IsCompleted = isCompleted;
        db.SaveChanges();

        audit.Record(AuditActions.Updated, "Activity", entity.Id.ToString(),
            isCompleted ? $"Completed task {entity.Title}" : $"Reopened task {entity.Title}");

        return entity.ToRecord();
    }

    public bool DeleteActivity(Guid id)
    {
        var entity = db.Activities.AsNoTracking().FirstOrDefault(activity => activity.Id == id);
        if (entity is null || db.Activities.Where(activity => activity.Id == id).ExecuteDelete() == 0)
        {
            return false;
        }

        audit.Record(AuditActions.Deleted, "Activity", id.ToString(), $"Deleted task {entity.Title}");
        return true;
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new CrmValidationException($"{field} is required.")
            : value.Trim();

    private static string Text(string? value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string ValidEmail(string? value)
    {
        var email = Text(value);
        if (email.Length > 0 && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
        {
            throw new CrmValidationException("Email address is not valid.");
        }

        return email;
    }

    private static decimal PositiveValue(decimal value) =>
        value > 0 ? value : throw new CrmValidationException("Deal value must be greater than zero.");

    private static string ValidStage(string? stage, string fallback)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return fallback;
        }

        return DealStages.IsKnown(stage)
            ? DealStages.Normalize(stage)
            : throw new CrmValidationException($"Stage must be one of: {string.Join(", ", DealStages.All)}.");
    }
}
