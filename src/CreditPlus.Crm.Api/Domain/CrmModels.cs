namespace CreditPlus.Crm.Api.Domain;

public static class DealStages
{
    public const string Qualified = "Qualified";
    public const string Proposal = "Proposal";
    public const string Negotiation = "Negotiation";
    public const string Won = "Won";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> Open = [Qualified, Proposal, Negotiation];
    public static readonly IReadOnlyList<string> All = [Qualified, Proposal, Negotiation, Won, Lost];

    public static bool IsKnown(string stage) => All.Contains(stage, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string stage) =>
        All.FirstOrDefault(known => known.Equals(stage, StringComparison.OrdinalIgnoreCase)) ?? Qualified;

    public static string AccentFor(string stage) => Normalize(stage) switch
    {
        Qualified => "blue",
        Proposal => "violet",
        Negotiation => "amber",
        Won => "emerald",
        _ => "slate"
    };
}

public static class LeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Converted = "Converted";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> All = [New, Contacted, Qualified, Converted, Lost];

    public static bool IsKnown(string status) => All.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string status) =>
        All.FirstOrDefault(known => known.Equals(status, StringComparison.OrdinalIgnoreCase)) ?? New;
}

public sealed record Lead(
    Guid Id,
    string FullName,
    string Company,
    string Email,
    string Phone,
    string Source,
    string Status,
    string Owner,
    DateTimeOffset CreatedAt,
    string KycStatus,
    string RiskRating);

public sealed record CreateLeadRequest(string FullName, string Company, string? Email, string? Phone, string? Source);

public sealed record UpdateLeadRequest(
    string FullName,
    string Company,
    string? Email,
    string? Phone,
    string? Source,
    string? Status,
    string? RiskRating);

public sealed record Deal(
    Guid Id,
    string Name,
    string Company,
    string Contact,
    decimal Value,
    string Stage,
    int Probability,
    DateOnly ExpectedCloseDate,
    string Owner,
    DateTimeOffset CreatedAt);

public sealed record CreateDealRequest(
    string Name,
    string Company,
    string? Contact,
    decimal Value,
    string? Stage,
    int Probability,
    DateOnly ExpectedCloseDate);

public sealed record UpdateDealRequest(
    string Name,
    string Company,
    string? Contact,
    decimal Value,
    string? Stage,
    int Probability,
    DateOnly ExpectedCloseDate);

public sealed record Activity(
    Guid Id,
    string Title,
    string Type,
    string RelatedTo,
    DateTimeOffset DueAt,
    bool IsCompleted,
    string Owner,
    DateTimeOffset CreatedAt);

public sealed record CreateActivityRequest(string Title, string? Type, string? RelatedTo, DateTimeOffset DueAt);

public sealed record UpdateActivityRequest(string Title, string? Type, string? RelatedTo, DateTimeOffset DueAt, bool IsCompleted);

public sealed record PipelineStage(string Id, string Name, decimal Value, int DealCount, string Accent);

public sealed record DashboardSummary(
    int NewLeads,
    int TotalLeads,
    decimal PipelineValue,
    decimal WonThisMonth,
    decimal ConversionRate,
    int OpenActivities,
    IReadOnlyList<PipelineStage> Pipeline,
    IReadOnlyList<Deal> Deals,
    IReadOnlyList<Activity> Activities);

/// <summary>The whole CRM database as persisted to a single JSON document.</summary>
public sealed class CrmData
{
    public List<Lead> Leads { get; set; } = [];
    public List<Deal> Deals { get; set; } = [];
    public List<Activity> Activities { get; set; } = [];
}
