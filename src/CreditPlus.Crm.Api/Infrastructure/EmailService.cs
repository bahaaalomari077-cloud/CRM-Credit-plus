using System.Net;
using System.Net.Mail;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class EmailOptions
{
    /// <summary>When false, messages are written to PickupDirectory instead of being sent.</summary>
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "crm@creditplus.local";
    public string FromName { get; set; } = "Credit Plus CRM";
    public string PickupDirectory { get; set; } = "App_Data/outbox";
}

/// <summary>
/// Sends through SMTP when configured. With no SMTP credentials it drops .eml files
/// into a pickup directory instead, so the feature is usable in development and the
/// message history behaves identically either way.
/// </summary>
public sealed class EmailService(
    CrmDbContext db,
    EmailOptions options,
    ICurrentUser currentUser,
    IAuditLog audit,
    IWebHostEnvironment environment,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task<EmailMessage> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new EmailMessageEntity
        {
            Id = Guid.NewGuid(),
            ToAddress = ValidEmail(request.ToAddress),
            ToName = Text(request.ToName),
            Subject = Required(request.Subject, "Subject"),
            Body = Required(request.Body, "Message body"),
            Status = EmailStatuses.Queued,
            RelatedType = Text(request.RelatedType),
            RelatedId = request.RelatedId,
            SentByUserId = currentUser.Id ?? Guid.Empty,
            SentByName = currentUser.Name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.EmailMessages.Add(entity);
        db.SaveChanges();

        try
        {
            await DeliverAsync(entity, cancellationToken);
            entity.Status = EmailStatuses.Sent;
            entity.SentAt = DateTimeOffset.UtcNow;
            entity.Error = null;
        }
        catch (Exception exception)
        {
            // A delivery failure is recorded on the message rather than thrown away,
            // so the history shows what was attempted and why it failed.
            entity.Status = EmailStatuses.Failed;
            entity.Error = exception.Message;
            logger.LogError(exception, "Failed to send email {EmailId} to {Recipient}", entity.Id, entity.ToAddress);
        }

        db.SaveChanges();

        audit.Record(AuditActions.EmailSent, "Email", entity.Id.ToString(),
            $"{entity.Status}: \"{entity.Subject}\" to {entity.ToAddress}",
            new { entity.ToAddress, entity.Subject, entity.Status, entity.RelatedType, entity.RelatedId });

        return entity.ToRecord();
    }

    public IReadOnlyList<EmailMessage> GetMessages(string? relatedType, Guid? relatedId, int limit)
    {
        var query = db.EmailMessages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(relatedType))
        {
            query = query.Where(email => email.RelatedType == relatedType);
        }

        if (relatedId.HasValue)
        {
            query = query.Where(email => email.RelatedId == relatedId.Value);
        }

        return query
            .OrderByDescending(email => email.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(email => email.ToRecord())
            .ToList();
    }

    private async Task DeliverAsync(EmailMessageEntity entity, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = entity.Subject,
            Body = entity.Body,
            IsBodyHtml = false,
        };
        message.To.Add(string.IsNullOrWhiteSpace(entity.ToName)
            ? new MailAddress(entity.ToAddress)
            : new MailAddress(entity.ToAddress, entity.ToName));

        using var client = new SmtpClient();

        if (options.Enabled)
        {
            client.Host = options.Host;
            client.Port = options.Port;
            client.EnableSsl = options.UseSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                client.Credentials = new NetworkCredential(options.Username, options.Password);
            }
        }
        else
        {
            var pickup = Path.IsPathRooted(options.PickupDirectory)
                ? options.PickupDirectory
                : Path.Combine(environment.ContentRootPath, options.PickupDirectory);
            Directory.CreateDirectory(pickup);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = pickup;
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new CrmValidationException($"{field} is required.")
            : value.Trim();

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

    private static string ValidEmail(string? value)
    {
        var email = Required(value, "Recipient address");
        if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            throw new CrmValidationException("Recipient address is not valid.");
        }

        return email;
    }
}
