using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

public static class CrmDbSeeder
{
    private const string DefaultOwner = "Ahmad Khalil";

    /// <summary>Creates the schema if needed and fills demo rows into empty tables.</summary>
    public static void MigrateAndSeed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        db.Database.Migrate();

        var today = DateTime.Today;
        var now = DateTimeOffset.UtcNow;

        if (!db.Leads.Any())
        {
            db.Leads.AddRange(
                new LeadEntity { Id = Guid.NewGuid(), FullName = "Sarah Haddad", Company = "Al Noor Trading", Email = "sarah@alnoor.com", Phone = "+962 7 9000 1122", Source = "Website", Status = LeadStatuses.Qualified, Owner = DefaultOwner, CreatedAt = now.AddDays(-1) },
                new LeadEntity { Id = Guid.NewGuid(), FullName = "Omar Nasser", Company = "Amman Logistics", Email = "omar@ammanlogistics.com", Phone = "+962 7 9000 2233", Source = "Referral", Status = LeadStatuses.New, Owner = DefaultOwner, CreatedAt = now.AddDays(-2) },
                new LeadEntity { Id = Guid.NewGuid(), FullName = "Lina Saad", Company = "Petra Retail Group", Email = "lina@petraretail.com", Phone = "+962 7 9000 3344", Source = "Campaign", Status = LeadStatuses.Contacted, Owner = DefaultOwner, CreatedAt = now.AddDays(-4) },
                new LeadEntity { Id = Guid.NewGuid(), FullName = "Maya Khalil", Company = "Jordan Capital", Email = "maya@jordancapital.com", Phone = "+962 7 9000 4455", Source = "Social media", Status = LeadStatuses.New, Owner = DefaultOwner, CreatedAt = now.AddDays(-5) });
        }

        if (!db.Deals.Any())
        {
            db.Deals.AddRange(
                new DealEntity { Id = Guid.NewGuid(), Name = "Renewal program", Company = "Al Noor Trading", Contact = "Sarah Haddad", Value = 32_000m, Stage = DealStages.Proposal, Probability = 65, ExpectedCloseDate = DateOnly.FromDateTime(today.AddDays(4)), Owner = DefaultOwner, CreatedAt = now.AddDays(-9) },
                new DealEntity { Id = Guid.NewGuid(), Name = "Working capital facility", Company = "Amman Logistics", Contact = "Omar Nasser", Value = 18_500m, Stage = DealStages.Negotiation, Probability = 80, ExpectedCloseDate = DateOnly.FromDateTime(today.AddDays(2)), Owner = DefaultOwner, CreatedAt = now.AddDays(-12) },
                new DealEntity { Id = Guid.NewGuid(), Name = "Fleet expansion credit", Company = "Petra Retail Group", Contact = "Lina Saad", Value = 47_000m, Stage = DealStages.Qualified, Probability = 40, ExpectedCloseDate = DateOnly.FromDateTime(today.AddDays(8)), Owner = DefaultOwner, CreatedAt = now.AddDays(-6) },
                new DealEntity { Id = Guid.NewGuid(), Name = "Merchant onboarding", Company = "Jordan Capital", Contact = "Maya Khalil", Value = 22_000m, Stage = DealStages.Proposal, Probability = 55, ExpectedCloseDate = DateOnly.FromDateTime(today.AddDays(6)), Owner = DefaultOwner, CreatedAt = now.AddDays(-3) },
                new DealEntity { Id = Guid.NewGuid(), Name = "Payroll financing", Company = "Zarqa Industrial", Contact = "Rami Odeh", Value = 41_000m, Stage = DealStages.Won, Probability = 100, ExpectedCloseDate = DateOnly.FromDateTime(today.AddDays(-2)), Owner = DefaultOwner, CreatedAt = now.AddDays(-20) });
        }

        if (!db.Activities.Any())
        {
            db.Activities.AddRange(
                new ActivityEntity { Id = Guid.NewGuid(), Title = "Discovery call", Type = "Call", RelatedTo = "Al Noor Trading", DueAt = DateTimeOffset.Now.AddHours(1), IsCompleted = false, Owner = DefaultOwner, CreatedAt = now.AddDays(-1) },
                new ActivityEntity { Id = Guid.NewGuid(), Title = "Send revised proposal", Type = "Email", RelatedTo = "Amman Logistics", DueAt = DateTimeOffset.Now.AddHours(3), IsCompleted = false, Owner = DefaultOwner, CreatedAt = now.AddDays(-1) },
                new ActivityEntity { Id = Guid.NewGuid(), Title = "Quarterly review", Type = "Meeting", RelatedTo = "Petra Retail Group", DueAt = DateTimeOffset.Now.AddDays(1), IsCompleted = false, Owner = DefaultOwner, CreatedAt = now.AddDays(-2) },
                new ActivityEntity { Id = Guid.NewGuid(), Title = "Collect signed contract", Type = "Task", RelatedTo = "Zarqa Industrial", DueAt = DateTimeOffset.Now.AddDays(-1), IsCompleted = true, Owner = DefaultOwner, CreatedAt = now.AddDays(-5) });
        }

        db.SaveChanges();
    }
}
