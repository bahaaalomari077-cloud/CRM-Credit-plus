namespace CreditPlus.Crm.Api.Domain;

public static class EmailStatuses
{
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

/// <summary>A message sent from the CRM, kept so it shows on the record's history.</summary>
public sealed record EmailMessage(
    Guid Id,
    string ToAddress,
    string ToName,
    string Subject,
    string Body,
    string Status,
    string? Error,
    string RelatedType,
    Guid? RelatedId,
    Guid SentByUserId,
    string SentByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

public sealed record SendEmailRequest(
    string ToAddress,
    string? ToName,
    string Subject,
    string Body,
    string? RelatedType,
    Guid? RelatedId);
