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

        if (!db.Users.Any())
        {
            // First-run administrator. The README tells the operator to change this
            // password immediately; it exists only so a fresh database can be signed into.
            db.Users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "admin@creditplus.local",
                FullName = DefaultOwner,
                PasswordHash = PasswordHasher.Hash("Admin123!"),
                Role = Roles.Admin,
                IsActive = true,
                CreatedAt = now,
            });

            db.Users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "agent@creditplus.local",
                FullName = "Lina Saad",
                PasswordHash = PasswordHasher.Hash("Agent123!"),
                Role = Roles.Agent,
                IsActive = true,
                CreatedAt = now,
            });
        }

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

        // The banking tables need the seeded leads to exist first.
        db.SaveChanges();

        if (!db.KycDocuments.Any() && db.Leads.Any())
        {
            var sarah = db.Leads.First(lead => lead.Email.StartsWith("sarah"));
            var omar = db.Leads.First(lead => lead.Email.StartsWith("omar"));

            db.KycDocuments.AddRange(
                new KycDocumentEntity { Id = Guid.NewGuid(), LeadId = sarah.Id, DocumentType = "NationalId", Reference = "9881023456", Status = KycStatuses.Verified, IssuedOn = DateOnly.FromDateTime(today.AddYears(-3)), ExpiresOn = DateOnly.FromDateTime(today.AddYears(4)), VerifiedBy = DefaultOwner, VerifiedAt = now.AddDays(-3), CreatedAt = now.AddDays(-4) },
                new KycDocumentEntity { Id = Guid.NewGuid(), LeadId = sarah.Id, DocumentType = "ProofOfAddress", Reference = "UTIL-2026-114", Status = KycStatuses.Verified, IssuedOn = DateOnly.FromDateTime(today.AddMonths(-2)), ExpiresOn = DateOnly.FromDateTime(today.AddMonths(10)), VerifiedBy = DefaultOwner, VerifiedAt = now.AddDays(-3), CreatedAt = now.AddDays(-4) },
                new KycDocumentEntity { Id = Guid.NewGuid(), LeadId = omar.Id, DocumentType = "NationalId", Reference = "9770118822", Status = KycStatuses.Pending, IssuedOn = DateOnly.FromDateTime(today.AddYears(-1)), ExpiresOn = DateOnly.FromDateTime(today.AddYears(6)), CreatedAt = now.AddDays(-1) });

            // Keep the rolled-up status on the lead consistent with the documents above.
            sarah.KycStatus = KycStatuses.Verified;
            omar.KycStatus = KycStatuses.Pending;
        }

        if (!db.LoanApplications.Any() && db.Leads.Any())
        {
            var sarah = db.Leads.First(lead => lead.Email.StartsWith("sarah"));
            var omar = db.Leads.First(lead => lead.Email.StartsWith("omar"));
            var lina = db.Leads.First(lead => lead.Email.StartsWith("lina"));

            db.LoanApplications.AddRange(
                new LoanApplicationEntity { Id = Guid.NewGuid(), Reference = $"LN-{today.Year}-0001", LeadId = sarah.Id, Product = "WorkingCapital", Amount = 45_000m, TermMonths = 36, InterestRate = 7.5m, Status = LoanStatuses.UnderReview, Owner = DefaultOwner, CreatedAt = now.AddDays(-6) },
                new LoanApplicationEntity { Id = Guid.NewGuid(), Reference = $"LN-{today.Year}-0002", LeadId = omar.Id, Product = "EquipmentFinance", Amount = 120_000m, TermMonths = 60, InterestRate = 6.25m, Status = LoanStatuses.Submitted, Owner = DefaultOwner, CreatedAt = now.AddDays(-2) },
                new LoanApplicationEntity { Id = Guid.NewGuid(), Reference = $"LN-{today.Year}-0003", LeadId = lina.Id, Product = "TradeFinance", Amount = 78_000m, TermMonths = 24, InterestRate = 8.0m, Status = LoanStatuses.Draft, Owner = DefaultOwner, CreatedAt = now.AddDays(-1) });
        }

        if (!db.Tickets.Any() && db.Leads.Any())
        {
            var sarah = db.Leads.First(lead => lead.Email.StartsWith("sarah"));
            var lina = db.Leads.First(lead => lead.Email.StartsWith("lina"));

            db.Tickets.AddRange(
                new TicketEntity { Id = Guid.NewGuid(), Reference = $"TK-{today.Year}-0001", LeadId = sarah.Id, CustomerName = sarah.FullName, Subject = "Statement not received", Description = "The monthly statement for August has not arrived by email.", Category = "Inquiry", Priority = TicketPriorities.Normal, Status = TicketStatuses.Open, AssignedTo = DefaultOwner, CreatedAt = now.AddHours(-30), DueAt = now.AddHours(-6) },
                new TicketEntity { Id = Guid.NewGuid(), Reference = $"TK-{today.Year}-0002", LeadId = lina.Id, CustomerName = lina.FullName, Subject = "Rate on the trade facility", Description = "Customer is asking why the quoted rate changed between the offer and the contract.", Category = "LoanIssue", Priority = TicketPriorities.High, Status = TicketStatuses.InProgress, AssignedTo = DefaultOwner, CreatedAt = now.AddHours(-3), DueAt = now.AddHours(5) },
                new TicketEntity { Id = Guid.NewGuid(), Reference = $"TK-{today.Year}-0003", LeadId = null, CustomerName = "Walk-in enquiry", Subject = "Branch opening hours", Description = "Asked about Saturday opening hours.", Category = "Inquiry", Priority = TicketPriorities.Low, Status = TicketStatuses.Resolved, AssignedTo = DefaultOwner, Resolution = "Advised 9am to 1pm on Saturdays.", CreatedAt = now.AddDays(-2), ResolvedAt = now.AddDays(-2).AddHours(1), DueAt = now.AddDays(-2).AddHours(72) });
        }

        db.SaveChanges();
    }
}
