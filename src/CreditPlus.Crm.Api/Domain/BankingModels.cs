namespace CreditPlus.Crm.Api.Domain;

// ---------------------------------------------------------------- KYC

public static class KycStatuses
{
    public const string NotStarted = "NotStarted";
    public const string Pending = "Pending";
    public const string Verified = "Verified";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";

    public static readonly IReadOnlyList<string> All = [NotStarted, Pending, Verified, Rejected, Expired];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.FirstOrDefault(known => known.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? NotStarted;
}

public static class RiskRatings
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.FirstOrDefault(known => known.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? Medium;
}

public static class KycDocumentTypes
{
    public static readonly IReadOnlyList<string> All =
        ["NationalId", "Passport", "ProofOfAddress", "IncomeStatement", "CommercialRegister", "TaxNumber"];
}

/// <summary>
/// One KYC document held for a lead. Verification carries an expiry so a document
/// that lapses can be surfaced before it becomes a compliance problem.
/// </summary>
public sealed record KycDocument(
    Guid Id,
    Guid LeadId,
    string DocumentType,
    string Reference,
    string Status,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    string? Notes,
    string VerifiedBy,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt);

public sealed record CreateKycDocumentRequest(
    Guid LeadId,
    string DocumentType,
    string? Reference,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    string? Notes);

public sealed record VerifyKycDocumentRequest(string Status, string? Notes);

/// <summary>Rolled-up KYC position for one customer.</summary>
public sealed record KycSummary(
    Guid LeadId,
    string Status,
    string RiskRating,
    int Verified,
    int Pending,
    int Expired,
    int Missing,
    IReadOnlyList<KycDocument> Documents);

// ---------------------------------------------------------- Loan applications

public static class LoanStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Disbursed = "Disbursed";

    public static readonly IReadOnlyList<string> All =
        [Draft, Submitted, UnderReview, Approved, Rejected, Disbursed];

    /// <summary>Which statuses a given status may move to; anything else is refused.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Transitions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Draft] = [Submitted],
            [Submitted] = [UnderReview, Rejected],
            [UnderReview] = [Approved, Rejected],
            [Approved] = [Disbursed, Rejected],
            [Rejected] = [],
            [Disbursed] = [],
        };

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.FirstOrDefault(known => known.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? Draft;

    public static bool CanMove(string from, string to) =>
        Transitions.TryGetValue(from, out var allowed) &&
        allowed.Contains(Normalize(to), StringComparer.OrdinalIgnoreCase);
}

public static class LoanProducts
{
    public static readonly IReadOnlyList<string> All =
        ["WorkingCapital", "EquipmentFinance", "TradeFinance", "PersonalLoan", "MortgageLoan"];
}

public sealed record LoanApplication(
    Guid Id,
    string Reference,
    Guid LeadId,
    string CustomerName,
    string Company,
    string Product,
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    string Status,
    string? Decision,
    string? DecisionNotes,
    string Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    decimal MonthlyPayment);

public sealed record CreateLoanRequest(
    Guid LeadId,
    string Product,
    decimal Amount,
    int TermMonths,
    decimal InterestRate);

public sealed record MoveLoanRequest(string Status, string? Notes);

// ---------------------------------------------------------------- Tickets

public static class TicketStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Waiting = "Waiting";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = [Open, InProgress, Waiting, Resolved, Closed];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.FirstOrDefault(known => known.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? Open;
}

public static class TicketPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlyList<string> All = [Low, Normal, High, Urgent];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.FirstOrDefault(known => known.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? Normal;
}

public static class TicketCategories
{
    public static readonly IReadOnlyList<string> All =
        ["Complaint", "Inquiry", "LoanIssue", "AccountIssue", "DocumentRequest", "Other"];
}

public sealed record Ticket(
    Guid Id,
    string Reference,
    Guid? LeadId,
    string CustomerName,
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    string AssignedTo,
    string? Resolution,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset DueAt,
    bool IsBreached);

public sealed record CreateTicketRequest(
    Guid? LeadId,
    string CustomerName,
    string Subject,
    string Description,
    string? Category,
    string? Priority);

public sealed record UpdateTicketRequest(
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    string? AssignedTo,
    string? Resolution);

// ------------------------------------------------------ Customer 360 view

/// <summary>Everything the CRM holds about one customer, assembled for a single screen.</summary>
public sealed record CustomerOverview(
    Lead Lead,
    int Score,
    string ScoreBand,
    IReadOnlyList<string> ScoreReasons,
    KycSummary Kyc,
    IReadOnlyList<Deal> Deals,
    IReadOnlyList<LoanApplication> Loans,
    IReadOnlyList<Ticket> Tickets,
    IReadOnlyList<Activity> Activities,
    IReadOnlyList<EmailMessage> Emails,
    IReadOnlyList<AuditEntry> History,
    decimal TotalDealValue,
    decimal TotalLoanValue);
