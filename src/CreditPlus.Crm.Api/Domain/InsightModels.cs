namespace CreditPlus.Crm.Api.Domain;

public static class AlertSeverities
{
    public const string Critical = "Critical";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

public static class AlertKinds
{
    public const string KycExpiring = "KycExpiring";
    public const string KycExpired = "KycExpired";
    public const string KycMissing = "KycMissing";
    public const string SlaBreached = "SlaBreached";
    public const string SlaAtRisk = "SlaAtRisk";
    public const string LoanAwaitingDecision = "LoanAwaitingDecision";
    public const string DealClosingSoon = "DealClosingSoon";
    public const string TaskOverdue = "TaskOverdue";
}

/// <summary>
/// Something that needs a person's attention, derived from stored data rather than
/// stored as its own record, so an alert can never go stale.
/// </summary>
public sealed record Alert(
    string Kind,
    string Severity,
    string Title,
    string Detail,
    string EntityType,
    string EntityId,
    string? Link,
    DateTimeOffset? DueAt);

/// <summary>Lending and service numbers for the reports screen.</summary>
public sealed record OperationsReport(
    int TotalLoans,
    decimal TotalRequested,
    decimal TotalApproved,
    decimal TotalDisbursed,
    decimal ApprovalRate,
    decimal AverageLoanSize,
    IReadOnlyList<CountAndValue> LoansByStatus,
    IReadOnlyList<CountAndValue> LoansByProduct,
    int TotalTickets,
    int OpenTickets,
    int BreachedTickets,
    decimal SlaComplianceRate,
    double AverageResolutionHours,
    IReadOnlyList<CountAndValue> TicketsByCategory,
    IReadOnlyList<CountAndValue> TicketsByPriority,
    IReadOnlyList<CountAndValue> KycByStatus,
    int CustomersFullyVerified);

public sealed record CountAndValue(string Label, int Count, decimal Value);
