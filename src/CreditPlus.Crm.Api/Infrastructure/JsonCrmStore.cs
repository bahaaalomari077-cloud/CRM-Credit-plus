using System.Text.Json;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Keeps the whole CRM in one JSON document under App_Data so the demo survives restarts
/// without needing a database. Every read and write goes through the same gate.
/// </summary>
public sealed class JsonCrmStore : ICrmStore
{
    private const string DefaultOwner = "Ahmad Khalil";

    private readonly Lock gate = new();
    private readonly string path;
    private readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private CrmData data = new();

    public JsonCrmStore(IWebHostEnvironment environment)
    {
        path = Path.Combine(environment.ContentRootPath, "App_Data", "crm.json");
        data = Load();
    }

    public IReadOnlyList<Lead> GetLeads(string? search = null, string? status = null)
    {
        lock (gate)
        {
            IEnumerable<Lead> leads = data.Leads;

            if (!string.IsNullOrWhiteSpace(status))
            {
                leads = leads.Where(lead => lead.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                leads = leads.Where(lead =>
                    Contains(lead.FullName, term) || Contains(lead.Company, term) ||
                    Contains(lead.Email, term) || Contains(lead.Phone, term));
            }

            return leads.OrderByDescending(lead => lead.CreatedAt).ToList();
        }
    }

    public Lead? FindLead(Guid id)
    {
        lock (gate) return data.Leads.FirstOrDefault(lead => lead.Id == id);
    }

    public Lead AddLead(CreateLeadRequest request)
    {
        var lead = new Lead(
            Guid.NewGuid(),
            Required(request.FullName, "Full name"),
            Required(request.Company, "Company"),
            ValidEmail(request.Email),
            Text(request.Phone),
            Text(request.Source, "Website"),
            LeadStatuses.New,
            DefaultOwner,
            DateTimeOffset.UtcNow);

        lock (gate)
        {
            data.Leads.Add(lead);
            Save();
        }

        return lead;
    }

    public Lead UpdateLead(Guid id, UpdateLeadRequest request)
    {
        var fullName = Required(request.FullName, "Full name");
        var company = Required(request.Company, "Company");
        var email = ValidEmail(request.Email);

        lock (gate)
        {
            var index = IndexOfLead(id);
            var current = data.Leads[index];
            data.Leads[index] = current with
            {
                FullName = fullName,
                Company = company,
                Email = email,
                Phone = Text(request.Phone),
                Source = Text(request.Source, current.Source),
                Status = LeadStatuses.Normalize(Text(request.Status, current.Status))
            };
            Save();
            return data.Leads[index];
        }
    }

    public bool DeleteLead(Guid id)
    {
        lock (gate)
        {
            if (data.Leads.RemoveAll(lead => lead.Id == id) == 0)
            {
                return false;
            }

            Save();
            return true;
        }
    }

    public IReadOnlyList<Deal> GetDeals(string? search = null, string? stage = null)
    {
        lock (gate)
        {
            IEnumerable<Deal> deals = data.Deals;

            if (!string.IsNullOrWhiteSpace(stage))
            {
                deals = deals.Where(deal => deal.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                deals = deals.Where(deal =>
                    Contains(deal.Name, term) || Contains(deal.Company, term) || Contains(deal.Contact, term));
            }

            return deals.OrderBy(deal => deal.ExpectedCloseDate).ThenByDescending(deal => deal.Value).ToList();
        }
    }

    public Deal? FindDeal(Guid id)
    {
        lock (gate) return data.Deals.FirstOrDefault(deal => deal.Id == id);
    }

    public Deal AddDeal(CreateDealRequest request)
    {
        var deal = new Deal(
            Guid.NewGuid(),
            Required(request.Name, "Deal name"),
            Required(request.Company, "Company"),
            Text(request.Contact),
            PositiveValue(request.Value),
            ValidStage(request.Stage, DealStages.Qualified),
            Math.Clamp(request.Probability, 0, 100),
            request.ExpectedCloseDate == default
                ? DateOnly.FromDateTime(DateTime.Today.AddDays(14))
                : request.ExpectedCloseDate,
            DefaultOwner,
            DateTimeOffset.UtcNow);

        lock (gate)
        {
            data.Deals.Add(deal);
            Save();
        }

        return deal;
    }

    public Deal UpdateDeal(Guid id, UpdateDealRequest request)
    {
        var name = Required(request.Name, "Deal name");
        var company = Required(request.Company, "Company");
        var value = PositiveValue(request.Value);

        lock (gate)
        {
            var index = IndexOfDeal(id);
            var current = data.Deals[index];
            data.Deals[index] = current with
            {
                Name = name,
                Company = company,
                Contact = Text(request.Contact),
                Value = value,
                Stage = ValidStage(request.Stage, current.Stage),
                Probability = Math.Clamp(request.Probability, 0, 100),
                ExpectedCloseDate = request.ExpectedCloseDate == default
                    ? current.ExpectedCloseDate
                    : request.ExpectedCloseDate
            };
            Save();
            return data.Deals[index];
        }
    }

    public Deal MoveDeal(Guid id, string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || !DealStages.IsKnown(stage))
        {
            throw new CrmValidationException($"Stage must be one of: {string.Join(", ", DealStages.All)}.");
        }

        var target = DealStages.Normalize(stage);

        lock (gate)
        {
            var index = IndexOfDeal(id);
            var current = data.Deals[index];
            // Closing a deal settles its probability either way, so the pipeline maths stay honest.
            var probability = target switch
            {
                DealStages.Won => 100,
                DealStages.Lost => 0,
                _ => current.Probability
            };

            data.Deals[index] = current with { Stage = target, Probability = probability };
            Save();
            return data.Deals[index];
        }
    }

    public bool DeleteDeal(Guid id)
    {
        lock (gate)
        {
            if (data.Deals.RemoveAll(deal => deal.Id == id) == 0)
            {
                return false;
            }

            Save();
            return true;
        }
    }

    public IReadOnlyList<Activity> GetActivities(bool? completed = null)
    {
        lock (gate)
        {
            IEnumerable<Activity> activities = data.Activities;

            if (completed.HasValue)
            {
                activities = activities.Where(activity => activity.IsCompleted == completed.Value);
            }

            return activities.OrderBy(activity => activity.IsCompleted).ThenBy(activity => activity.DueAt).ToList();
        }
    }

    public Activity? FindActivity(Guid id)
    {
        lock (gate) return data.Activities.FirstOrDefault(activity => activity.Id == id);
    }

    public Activity AddActivity(CreateActivityRequest request)
    {
        var activity = new Activity(
            Guid.NewGuid(),
            Required(request.Title, "Activity title"),
            Text(request.Type, "Task"),
            Text(request.RelatedTo),
            request.DueAt == default ? DateTimeOffset.Now.AddHours(1) : request.DueAt,
            false,
            DefaultOwner,
            DateTimeOffset.UtcNow);

        lock (gate)
        {
            data.Activities.Add(activity);
            Save();
        }

        return activity;
    }

    public Activity UpdateActivity(Guid id, UpdateActivityRequest request)
    {
        var title = Required(request.Title, "Activity title");

        lock (gate)
        {
            var index = IndexOfActivity(id);
            var current = data.Activities[index];
            data.Activities[index] = current with
            {
                Title = title,
                Type = Text(request.Type, current.Type),
                RelatedTo = Text(request.RelatedTo),
                DueAt = request.DueAt == default ? current.DueAt : request.DueAt,
                IsCompleted = request.IsCompleted
            };
            Save();
            return data.Activities[index];
        }
    }

    public Activity SetActivityCompletion(Guid id, bool isCompleted)
    {
        lock (gate)
        {
            var index = IndexOfActivity(id);
            data.Activities[index] = data.Activities[index] with { IsCompleted = isCompleted };
            Save();
            return data.Activities[index];
        }
    }

    public bool DeleteActivity(Guid id)
    {
        lock (gate)
        {
            if (data.Activities.RemoveAll(activity => activity.Id == id) == 0)
            {
                return false;
            }

            Save();
            return true;
        }
    }

    private int IndexOfLead(Guid id)
    {
        var index = data.Leads.FindIndex(lead => lead.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Lead {id} was not found.");
    }

    private int IndexOfDeal(Guid id)
    {
        var index = data.Deals.FindIndex(deal => deal.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Deal {id} was not found.");
    }

    private int IndexOfActivity(Guid id)
    {
        var index = data.Activities.FindIndex(activity => activity.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Activity {id} was not found.");
    }

    private CrmData Load()
    {
        if (File.Exists(path))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<CrmData>(File.ReadAllText(path), options);
                if (stored is not null)
                {
                    return stored;
                }
            }
            catch (JsonException)
            {
                // A hand-edited or truncated file should not stop the API from booting; reseed instead.
            }
        }

        data = Seed();
        Save();
        return data;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write through a temp file so a crash mid-write cannot leave a half-written database.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, options));
        File.Move(temporary, path, overwrite: true);
    }

    private static bool Contains(string source, string term) =>
        source.Contains(term, StringComparison.OrdinalIgnoreCase);

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

    private static CrmData Seed()
    {
        var today = DateTime.Today;
        var now = DateTimeOffset.UtcNow;

        return new CrmData
        {
            Leads =
            [
                new(Guid.NewGuid(), "Sarah Haddad", "Al Noor Trading", "sarah@alnoor.com", "+962 7 9000 1122", "Website", LeadStatuses.Qualified, DefaultOwner, now.AddDays(-1)),
                new(Guid.NewGuid(), "Omar Nasser", "Amman Logistics", "omar@ammanlogistics.com", "+962 7 9000 2233", "Referral", LeadStatuses.New, DefaultOwner, now.AddDays(-2)),
                new(Guid.NewGuid(), "Lina Saad", "Petra Retail Group", "lina@petraretail.com", "+962 7 9000 3344", "Campaign", LeadStatuses.Contacted, DefaultOwner, now.AddDays(-4)),
                new(Guid.NewGuid(), "Maya Khalil", "Jordan Capital", "maya@jordancapital.com", "+962 7 9000 4455", "Social media", LeadStatuses.New, DefaultOwner, now.AddDays(-5))
            ],
            Deals =
            [
                new(Guid.NewGuid(), "Renewal program", "Al Noor Trading", "Sarah Haddad", 32_000m, DealStages.Proposal, 65, DateOnly.FromDateTime(today.AddDays(4)), DefaultOwner, now.AddDays(-9)),
                new(Guid.NewGuid(), "Working capital facility", "Amman Logistics", "Omar Nasser", 18_500m, DealStages.Negotiation, 80, DateOnly.FromDateTime(today.AddDays(2)), DefaultOwner, now.AddDays(-12)),
                new(Guid.NewGuid(), "Fleet expansion credit", "Petra Retail Group", "Lina Saad", 47_000m, DealStages.Qualified, 40, DateOnly.FromDateTime(today.AddDays(8)), DefaultOwner, now.AddDays(-6)),
                new(Guid.NewGuid(), "Merchant onboarding", "Jordan Capital", "Maya Khalil", 22_000m, DealStages.Proposal, 55, DateOnly.FromDateTime(today.AddDays(6)), DefaultOwner, now.AddDays(-3)),
                new(Guid.NewGuid(), "Payroll financing", "Zarqa Industrial", "Rami Odeh", 41_000m, DealStages.Won, 100, DateOnly.FromDateTime(today.AddDays(-2)), DefaultOwner, now.AddDays(-20))
            ],
            Activities =
            [
                new(Guid.NewGuid(), "Discovery call", "Call", "Al Noor Trading", DateTimeOffset.Now.AddHours(1), false, DefaultOwner, now.AddDays(-1)),
                new(Guid.NewGuid(), "Send revised proposal", "Email", "Amman Logistics", DateTimeOffset.Now.AddHours(3), false, DefaultOwner, now.AddDays(-1)),
                new(Guid.NewGuid(), "Quarterly review", "Meeting", "Petra Retail Group", DateTimeOffset.Now.AddDays(1), false, DefaultOwner, now.AddDays(-2)),
                new(Guid.NewGuid(), "Collect signed contract", "Task", "Zarqa Industrial", DateTimeOffset.Now.AddDays(-1), true, DefaultOwner, now.AddDays(-5))
            ]
        };
    }
}
