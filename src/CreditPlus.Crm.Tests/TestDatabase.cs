using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreditPlus.Crm.Tests;

/// <summary>
/// A throwaway PostgreSQL database per test class. Tests run against the real
/// provider rather than an in-memory substitute, so provider-specific behaviour
/// (ILike, numeric precision, cascade deletes) is actually exercised.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private const string Admin = "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=123456";

    private readonly string databaseName;
    private readonly string connectionString;

    public TestDatabase()
    {
        databaseName = $"crm_test_{Guid.NewGuid():N}";
        connectionString = $"Host=localhost;Port=5433;Database={databaseName};Username=postgres;Password=123456";

        Execute(Admin, $"CREATE DATABASE \"{databaseName}\"");

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public CrmDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CrmDbContext>().UseNpgsql(connectionString).Options);

    /// <summary>A store wired to a caller-supplied identity, so ownership and audit can be asserted.</summary>
    public PostgresCrmStore CreateStore(CrmDbContext context, ICurrentUser? user = null) =>
        new(context, user ?? new FakeCurrentUser(), new FakeAuditLog());

    public KycService CreateKyc(CrmDbContext context, ICurrentUser? user = null) =>
        new(context, user ?? new FakeCurrentUser(), new FakeAuditLog());

    public LoanService CreateLoans(CrmDbContext context, ICurrentUser? user = null) =>
        new(context, user ?? new FakeCurrentUser(), new FakeAuditLog());

    public TicketService CreateTickets(CrmDbContext context, ICurrentUser? user = null) =>
        new(context, user ?? new FakeCurrentUser(), new FakeAuditLog());

    public CustomerService CreateCustomers(CrmDbContext context) =>
        new(context, CreateKyc(context));

    public InsightService CreateInsights(CrmDbContext context) => new(context);

    public ExportService CreateExports(CrmDbContext context) => new(context, new FakeAuditLog());

    public UserService CreateUsers(CrmDbContext context, ICurrentUser? user = null) =>
        new(context, new FakeAuditLog(), user ?? new FakeCurrentUser(), new JwtOptions
        {
            SigningKey = "test-signing-key-at-least-32-characters-long",
        });

    /// <summary>Inserts a lead directly, bypassing validation, for arranging a test.</summary>
    public LeadEntity SeedLead(
        CrmDbContext context,
        string name = "Test Customer",
        string company = "Test Company",
        string status = LeadStatuses.New,
        string kyc = KycStatuses.NotStarted,
        string risk = RiskRatings.Medium,
        string source = "Website",
        string email = "test@example.com",
        string phone = "+962 7 0000 0000")
    {
        var lead = new LeadEntity
        {
            Id = Guid.NewGuid(),
            FullName = name,
            Company = company,
            Email = email,
            Phone = phone,
            Source = source,
            Status = status,
            KycStatus = kyc,
            RiskRating = risk,
            Owner = "Tester",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Leads.Add(lead);
        context.SaveChanges();
        return lead;
    }

    public void Dispose()
    {
        // Drop the whole database; terminate stragglers so the drop cannot block.
        Execute(Admin, $"""
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity
            WHERE datname = '{databaseName}' AND pid <> pg_backend_pid()
            """);
        Execute(Admin, $"DROP DATABASE IF EXISTS \"{databaseName}\"");
    }

    private static void Execute(string connection, string sql)
    {
        using var context = new CrmDbContext(
            new DbContextOptionsBuilder<CrmDbContext>().UseNpgsql(connection).Options);
        context.Database.ExecuteSqlRaw(sql);
    }
}

public sealed class FakeCurrentUser(
    string name = "Tester",
    string role = Roles.Admin,
    bool authenticated = true) : ICurrentUser
{
    public Guid? Id { get; } = Guid.NewGuid();
    public string Email => $"{name.Replace(" ", "").ToLowerInvariant()}@test.local";
    public string Name => name;
    public string Role => role;
    public bool IsAuthenticated => authenticated;
    public string? IpAddress => "127.0.0.1";
}

/// <summary>Records calls so a test can assert that auditing happened, without a database.</summary>
public sealed class FakeAuditLog : IAuditLog
{
    public List<(string Action, string EntityType, string EntityId, string? Summary)> Entries { get; } = [];

    public void Record(string action, string entityType, string entityId, string? summary = null, object? changes = null) =>
        Entries.Add((action, entityType, entityId, summary));

    public IReadOnlyList<AuditEntry> Query(string? entityType, string? entityId, string? action, int limit) => [];
}
