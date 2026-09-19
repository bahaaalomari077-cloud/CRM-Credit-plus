using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Assembles the single-customer view and scores leads. Scoring is deliberately
/// explainable: every point comes from a stored fact, and the reasons are returned
/// alongside the number so a salesperson can see why a lead ranks where it does.
/// </summary>
public sealed class CustomerService(CrmDbContext db, IKycService kyc) : ICustomerService
{
    public CustomerOverview? GetOverview(Guid leadId)
    {
        var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == leadId);
        if (lead is null)
        {
            return null;
        }

        var record = lead.ToRecord();
        var identifiers = new[] { lead.Company, lead.FullName };

        // Deals and activities reference the customer by name rather than id, which is
        // how the original schema linked them; match on either the person or the company.
        var deals = db.Deals.AsNoTracking()
            .Where(deal => identifiers.Contains(deal.Company) || identifiers.Contains(deal.Contact))
            .OrderByDescending(deal => deal.CreatedAt)
            .Select(deal => deal.ToRecord())
            .ToList();

        var activities = db.Activities.AsNoTracking()
            .Where(activity => identifiers.Contains(activity.RelatedTo))
            .OrderBy(activity => activity.IsCompleted)
            .ThenBy(activity => activity.DueAt)
            .Select(activity => activity.ToRecord())
            .ToList();

        var loans = (from loan in db.LoanApplications.AsNoTracking()
                     where loan.LeadId == leadId
                     select loan)
            .OrderByDescending(loan => loan.CreatedAt)
            .ToList()
            .Select(loan => loan.ToRecord(lead.FullName, lead.Company))
            .ToList();

        var tickets = db.Tickets.AsNoTracking()
            .Where(ticket => ticket.LeadId == leadId)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToList()
            .Select(ticket => ticket.ToRecord())
            .ToList();

        var emails = db.EmailMessages.AsNoTracking()
            .Where(email => email.RelatedType == "Lead" && email.RelatedId == leadId)
            .OrderByDescending(email => email.CreatedAt)
            .Take(20)
            .Select(email => email.ToRecord())
            .ToList();

        var key = leadId.ToString();
        var history = db.AuditEntries.AsNoTracking()
            .Where(entry => entry.EntityId == key)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(25)
            .Select(entry => entry.ToRecord())
            .ToList();

        var (score, band, reasons) = Score(leadId);

        return new CustomerOverview(
            record,
            score,
            band,
            reasons,
            kyc.GetSummary(leadId),
            deals,
            loans,
            tickets,
            activities,
            emails,
            history,
            deals.Where(deal => deal.Stage != DealStages.Lost).Sum(deal => deal.Value),
            loans.Where(loan => loan.Status is LoanStatuses.Approved or LoanStatuses.Disbursed)
                 .Sum(loan => loan.Amount));
    }

    public (int Score, string Band, IReadOnlyList<string> Reasons) Score(Guid leadId)
    {
        var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == leadId)
            ?? throw new KeyNotFoundException($"Lead {leadId} was not found.");

        var reasons = new List<string>();
        var score = 0;

        // Contactability: a lead nobody can reach cannot be worked.
        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            score += 10;
            reasons.Add("+10 email on file");
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            score += 10;
            reasons.Add("+10 phone on file");
        }

        // Source quality: referrals convert better than cold channels.
        var sourcePoints = lead.Source switch
        {
            "Referral" => 20,
            "Event" => 15,
            "Campaign" => 10,
            "Website" => 8,
            _ => 5,
        };
        score += sourcePoints;
        reasons.Add($"+{sourcePoints} source: {lead.Source}");

        // Progress through the sales stages.
        var statusPoints = lead.Status switch
        {
            LeadStatuses.Converted => 25,
            LeadStatuses.Qualified => 20,
            LeadStatuses.Contacted => 10,
            LeadStatuses.New => 5,
            _ => 0,
        };
        score += statusPoints;
        reasons.Add($"+{statusPoints} status: {lead.Status}");

        // Verified identity is worth real points in a regulated business.
        if (lead.KycStatus == KycStatuses.Verified)
        {
            score += 15;
            reasons.Add("+15 KYC verified");
        }
        else if (lead.KycStatus is KycStatuses.Rejected or KycStatuses.Expired)
        {
            score -= 10;
            reasons.Add($"-10 KYC {lead.KycStatus.ToLowerInvariant()}");
        }

        var identifiers = new[] { lead.Company, lead.FullName };

        var openDeals = db.Deals.AsNoTracking()
            .Count(deal => (identifiers.Contains(deal.Company) || identifiers.Contains(deal.Contact))
                           && deal.Stage != DealStages.Lost && deal.Stage != DealStages.Won);
        if (openDeals > 0)
        {
            var points = Math.Min(openDeals * 8, 16);
            score += points;
            reasons.Add($"+{points} {openDeals} open deal(s)");
        }

        var wonDeals = db.Deals.AsNoTracking()
            .Count(deal => (identifiers.Contains(deal.Company) || identifiers.Contains(deal.Contact))
                           && deal.Stage == DealStages.Won);
        if (wonDeals > 0)
        {
            score += 10;
            reasons.Add("+10 has closed business before");
        }

        // Engagement: replies are not tracked, but outbound contact still signals work done.
        var emails = db.EmailMessages.AsNoTracking()
            .Count(email => email.RelatedType == "Lead" && email.RelatedId == leadId);
        if (emails > 0)
        {
            var points = Math.Min(emails * 3, 9);
            score += points;
            reasons.Add($"+{points} {emails} email(s) sent");
        }

        // Unresolved complaints are a genuine negative signal.
        var openTickets = db.Tickets.AsNoTracking()
            .Count(ticket => ticket.LeadId == leadId
                             && ticket.Status != TicketStatuses.Resolved
                             && ticket.Status != TicketStatuses.Closed);
        if (openTickets > 0)
        {
            var penalty = Math.Min(openTickets * 5, 15);
            score -= penalty;
            reasons.Add($"-{penalty} {openTickets} open ticket(s)");
        }

        // High risk tempers an otherwise strong score.
        if (lead.RiskRating == RiskRatings.High)
        {
            score -= 10;
            reasons.Add("-10 high risk rating");
        }

        score = Math.Clamp(score, 0, 100);

        var band = score switch
        {
            >= 70 => "Hot",
            >= 40 => "Warm",
            _ => "Cold",
        };

        return (score, band, reasons);
    }
}
