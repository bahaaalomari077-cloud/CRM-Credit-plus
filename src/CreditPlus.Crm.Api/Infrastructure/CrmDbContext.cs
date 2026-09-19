using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<LeadEntity> Leads => Set<LeadEntity>();
    public DbSet<DealEntity> Deals => Set<DealEntity>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<EmailMessageEntity> EmailMessages => Set<EmailMessageEntity>();
    public DbSet<KycDocumentEntity> KycDocuments => Set<KycDocumentEntity>();
    public DbSet<LoanApplicationEntity> LoanApplications => Set<LoanApplicationEntity>();
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();

    /// <summary>
    /// Npgsql only accepts UTC for 'timestamp with time zone', but callers may send any
    /// offset (the client posts local time). Normalise on the way in; reads come back UTC.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcConverter =
        new(value => value.ToUniversalTime(), value => value.ToUniversalTime());

    private static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> NullableUtcConverter =
        new(value => value.HasValue ? value.Value.ToUniversalTime() : value,
            value => value.HasValue ? value.Value.ToUniversalTime() : value);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<LeadEntity>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(lead => lead.Id);
            entity.Property(lead => lead.FullName).HasMaxLength(200).IsRequired();
            entity.Property(lead => lead.Company).HasMaxLength(200).IsRequired();
            entity.Property(lead => lead.Email).HasMaxLength(200);
            entity.Property(lead => lead.Phone).HasMaxLength(60);
            entity.Property(lead => lead.Source).HasMaxLength(60);
            entity.Property(lead => lead.Status).HasMaxLength(30).IsRequired();
            entity.Property(lead => lead.Owner).HasMaxLength(120);
            entity.Property(lead => lead.KycStatus).HasMaxLength(20).IsRequired();
            entity.Property(lead => lead.RiskRating).HasMaxLength(20).IsRequired();
            entity.Property(lead => lead.CreatedAt).HasConversion(UtcConverter);
            entity.HasIndex(lead => lead.Status);
            entity.HasIndex(lead => lead.KycStatus);
            entity.HasIndex(lead => lead.CreatedAt);
        });

        builder.Entity<DealEntity>(entity =>
        {
            entity.ToTable("deals");
            entity.HasKey(deal => deal.Id);
            entity.Property(deal => deal.Name).HasMaxLength(200).IsRequired();
            entity.Property(deal => deal.Company).HasMaxLength(200).IsRequired();
            entity.Property(deal => deal.Contact).HasMaxLength(200);
            // Money keeps exact decimal semantics rather than float rounding.
            entity.Property(deal => deal.Value).HasColumnType("numeric(18,2)");
            entity.Property(deal => deal.Stage).HasMaxLength(30).IsRequired();
            entity.Property(deal => deal.Owner).HasMaxLength(120);
            entity.Property(deal => deal.CreatedAt).HasConversion(UtcConverter);
            entity.HasIndex(deal => deal.Stage);
            entity.HasIndex(deal => deal.ExpectedCloseDate);
        });

        builder.Entity<ActivityEntity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(activity => activity.Id);
            entity.Property(activity => activity.Title).HasMaxLength(250).IsRequired();
            entity.Property(activity => activity.Type).HasMaxLength(30).IsRequired();
            entity.Property(activity => activity.RelatedTo).HasMaxLength(200);
            entity.Property(activity => activity.Owner).HasMaxLength(120);
            entity.Property(activity => activity.DueAt).HasConversion(UtcConverter);
            entity.Property(activity => activity.CreatedAt).HasConversion(UtcConverter);
            entity.HasIndex(activity => activity.IsCompleted);
            entity.HasIndex(activity => activity.DueAt);
        });

        builder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(200).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(400).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(30).IsRequired();
            entity.Property(user => user.CreatedAt).HasConversion(UtcConverter);
            entity.Property(user => user.LastLoginAt).HasConversion(NullableUtcConverter);
            // Logins look up by email, and no two accounts may share one.
            entity.HasIndex(user => user.Email).IsUnique();
        });

        builder.Entity<AuditEntryEntity>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.UserEmail).HasMaxLength(200);
            entity.Property(audit => audit.Action).HasMaxLength(40).IsRequired();
            entity.Property(audit => audit.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(audit => audit.EntityId).HasMaxLength(64);
            entity.Property(audit => audit.Summary).HasMaxLength(400);
            entity.Property(audit => audit.IpAddress).HasMaxLength(64);
            entity.Property(audit => audit.OccurredAt).HasConversion(UtcConverter);
            entity.HasIndex(audit => audit.OccurredAt);
            entity.HasIndex(audit => new { audit.EntityType, audit.EntityId });
        });

        builder.Entity<EmailMessageEntity>(entity =>
        {
            entity.ToTable("email_messages");
            entity.HasKey(email => email.Id);
            entity.Property(email => email.ToAddress).HasMaxLength(200).IsRequired();
            entity.Property(email => email.ToName).HasMaxLength(200);
            entity.Property(email => email.Subject).HasMaxLength(300).IsRequired();
            entity.Property(email => email.Status).HasMaxLength(20).IsRequired();
            entity.Property(email => email.Error).HasMaxLength(1000);
            entity.Property(email => email.RelatedType).HasMaxLength(40);
            entity.Property(email => email.SentByName).HasMaxLength(200);
            entity.Property(email => email.CreatedAt).HasConversion(UtcConverter);
            entity.Property(email => email.SentAt).HasConversion(NullableUtcConverter);
            entity.HasIndex(email => email.CreatedAt);
            entity.HasIndex(email => new { email.RelatedType, email.RelatedId });
        });

        builder.Entity<KycDocumentEntity>(entity =>
        {
            entity.ToTable("kyc_documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.DocumentType).HasMaxLength(40).IsRequired();
            entity.Property(document => document.Reference).HasMaxLength(120);
            entity.Property(document => document.Status).HasMaxLength(20).IsRequired();
            entity.Property(document => document.Notes).HasMaxLength(600);
            entity.Property(document => document.VerifiedBy).HasMaxLength(120);
            entity.Property(document => document.CreatedAt).HasConversion(UtcConverter);
            entity.Property(document => document.VerifiedAt).HasConversion(NullableUtcConverter);
            // Deleting a customer takes their documents with them.
            entity.HasOne<LeadEntity>().WithMany()
                .HasForeignKey(document => document.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(document => document.LeadId);
        });

        builder.Entity<LoanApplicationEntity>(entity =>
        {
            entity.ToTable("loan_applications");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Reference).HasMaxLength(30).IsRequired();
            entity.Property(loan => loan.Product).HasMaxLength(40).IsRequired();
            entity.Property(loan => loan.Amount).HasColumnType("numeric(18,2)");
            entity.Property(loan => loan.InterestRate).HasColumnType("numeric(6,3)");
            entity.Property(loan => loan.Status).HasMaxLength(20).IsRequired();
            entity.Property(loan => loan.Decision).HasMaxLength(40);
            entity.Property(loan => loan.DecisionNotes).HasMaxLength(800);
            entity.Property(loan => loan.Owner).HasMaxLength(120);
            entity.Property(loan => loan.CreatedAt).HasConversion(UtcConverter);
            entity.Property(loan => loan.DecidedAt).HasConversion(NullableUtcConverter);
            entity.HasOne<LeadEntity>().WithMany()
                .HasForeignKey(loan => loan.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(loan => loan.Reference).IsUnique();
            entity.HasIndex(loan => loan.Status);
            entity.HasIndex(loan => loan.LeadId);
        });

        builder.Entity<TicketEntity>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Reference).HasMaxLength(30).IsRequired();
            entity.Property(ticket => ticket.CustomerName).HasMaxLength(200);
            entity.Property(ticket => ticket.Subject).HasMaxLength(250).IsRequired();
            entity.Property(ticket => ticket.Category).HasMaxLength(40).IsRequired();
            entity.Property(ticket => ticket.Priority).HasMaxLength(20).IsRequired();
            entity.Property(ticket => ticket.Status).HasMaxLength(20).IsRequired();
            entity.Property(ticket => ticket.AssignedTo).HasMaxLength(120);
            entity.Property(ticket => ticket.Resolution).HasMaxLength(1000);
            entity.Property(ticket => ticket.CreatedAt).HasConversion(UtcConverter);
            entity.Property(ticket => ticket.ResolvedAt).HasConversion(NullableUtcConverter);
            entity.Property(ticket => ticket.DueAt).HasConversion(UtcConverter);
            // A ticket may exist without a matching lead, so the link is optional.
            entity.HasOne<LeadEntity>().WithMany()
                .HasForeignKey(ticket => ticket.LeadId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(ticket => ticket.Reference).IsUnique();
            entity.HasIndex(ticket => ticket.Status);
            entity.HasIndex(ticket => ticket.LeadId);
        });
    }
}

/// <summary>
/// Mutable persistence entities. The API still exposes the immutable records in
/// <see cref="Domain"/>; these exist so EF can track changes.
/// </summary>
public sealed class LeadEntity
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Company { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Source { get; set; } = "";
    public string Status { get; set; } = LeadStatuses.New;
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string KycStatus { get; set; } = KycStatuses.NotStarted;
    public string RiskRating { get; set; } = RiskRatings.Medium;

    public Lead ToRecord() =>
        new(Id, FullName, Company, Email, Phone, Source, Status, Owner, CreatedAt, KycStatus, RiskRating);
}

public sealed class DealEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Contact { get; set; } = "";
    public decimal Value { get; set; }
    public string Stage { get; set; } = DealStages.Qualified;
    public int Probability { get; set; }
    public DateOnly ExpectedCloseDate { get; set; }
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public Deal ToRecord() =>
        new(Id, Name, Company, Contact, Value, Stage, Probability, ExpectedCloseDate, Owner, CreatedAt);
}

public sealed class ActivityEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "Task";
    public string RelatedTo { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public Activity ToRecord() => new(Id, Title, Type, RelatedTo, DueAt, IsCompleted, Owner, CreatedAt);
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public User ToRecord() => new(Id, Email, FullName, Role, IsActive, CreatedAt, LastLoginAt);
}

public sealed class AuditEntryEntity
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? Summary { get; set; }
    public string? Changes { get; set; }
    public string? IpAddress { get; set; }

    public AuditEntry ToRecord() =>
        new(Id, OccurredAt, UserId, UserEmail, Action, EntityType, EntityId, Summary, Changes, IpAddress);
}

public sealed class EmailMessageEntity
{
    public Guid Id { get; set; }
    public string ToAddress { get; set; } = "";
    public string ToName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = EmailStatuses.Queued;
    public string? Error { get; set; }
    public string RelatedType { get; set; } = "";
    public Guid? RelatedId { get; set; }
    public Guid SentByUserId { get; set; }
    public string SentByName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public EmailMessage ToRecord() =>
        new(Id, ToAddress, ToName, Subject, Body, Status, Error, RelatedType, RelatedId,
            SentByUserId, SentByName, CreatedAt, SentAt);
}

public sealed class KycDocumentEntity
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public string DocumentType { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Status { get; set; } = KycStatuses.Pending;
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? Notes { get; set; }
    public string VerifiedBy { get; set; } = "";
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>A verified document past its expiry reports as expired, not verified.</summary>
    public string EffectiveStatus =>
        Status == KycStatuses.Verified && ExpiresOn is { } expiry && expiry < DateOnly.FromDateTime(DateTime.Today)
            ? KycStatuses.Expired
            : Status;

    public KycDocument ToRecord() =>
        new(Id, LeadId, DocumentType, Reference, EffectiveStatus, IssuedOn, ExpiresOn, Notes, VerifiedBy, VerifiedAt, CreatedAt);
}

public sealed class LoanApplicationEntity
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public Guid LeadId { get; set; }
    public string Product { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public decimal InterestRate { get; set; }
    public string Status { get; set; } = LoanStatuses.Draft;
    public string? Decision { get; set; }
    public string? DecisionNotes { get; set; }
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>Standard amortising payment; a zero rate falls back to simple division.</summary>
    public decimal MonthlyPayment
    {
        get
        {
            if (TermMonths <= 0)
            {
                return 0m;
            }

            var monthlyRate = (double)InterestRate / 100 / 12;
            if (monthlyRate <= 0)
            {
                return Math.Round(Amount / TermMonths, 2);
            }

            var factor = Math.Pow(1 + monthlyRate, TermMonths);
            var payment = (double)Amount * monthlyRate * factor / (factor - 1);
            return Math.Round((decimal)payment, 2);
        }
    }

    public LoanApplication ToRecord(string customerName, string company) =>
        new(Id, Reference, LeadId, customerName, company, Product, Amount, TermMonths, InterestRate,
            Status, Decision, DecisionNotes, Owner, CreatedAt, DecidedAt, MonthlyPayment);
}

public sealed class TicketEntity
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public Guid? LeadId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string Priority { get; set; } = TicketPriorities.Normal;
    public string Status { get; set; } = TicketStatuses.Open;
    public string AssignedTo { get; set; } = "";
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset DueAt { get; set; }

    /// <summary>An unresolved ticket past its response target has breached it.</summary>
    public bool IsBreached =>
        Status is not (TicketStatuses.Resolved or TicketStatuses.Closed) && DueAt < DateTimeOffset.UtcNow;

    public Ticket ToRecord() =>
        new(Id, Reference, LeadId, CustomerName, Subject, Description, Category, Priority, Status,
            AssignedTo, Resolution, CreatedAt, ResolvedAt, DueAt, IsBreached);
}
