using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<LeadEntity> Leads => Set<LeadEntity>();
    public DbSet<DealEntity> Deals => Set<DealEntity>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();

    /// <summary>
    /// Npgsql only accepts UTC for 'timestamp with time zone', but callers may send any
    /// offset (the client posts local time). Normalise on the way in; reads come back UTC.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcConverter =
        new(value => value.ToUniversalTime(), value => value.ToUniversalTime());

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
            entity.Property(lead => lead.CreatedAt).HasConversion(UtcConverter);
            entity.HasIndex(lead => lead.Status);
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

    public Lead ToRecord() => new(Id, FullName, Company, Email, Phone, Source, Status, Owner, CreatedAt);
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
