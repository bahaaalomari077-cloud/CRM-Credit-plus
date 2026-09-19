using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Derives alerts and operational reporting from the stored data. Nothing here is
/// persisted: an alert exists only while the condition that raises it is true, so
/// resolving the underlying record clears it without any extra bookkeeping.
/// </summary>
public sealed class InsightService(CrmDbContext db) : IInsightService
{
    /// <summary>A document within this many days of expiry is worth warning about.</summary>
    private const int ExpiryWarningDays = 30;

    /// <summary>A ticket inside this fraction of its remaining time is at risk.</summary>
    private const double AtRiskFraction = 0.25;

    private static readonly string[] RequiredDocuments = ["NationalId", "ProofOfAddress"];

    public IReadOnlyList<Alert> GetAlerts(int limit = 50)
    {
        var alerts = new List<Alert>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var horizon = today.AddDays(ExpiryWarningDays);

        var leadNames = db.Leads.AsNoTracking()
            .Select(lead => new { lead.Id, lead.FullName, lead.Company, lead.KycStatus })
            .ToDictionary(lead => lead.Id);

        // --- KYC: expired, expiring, and never started ---
        foreach (var document in db.KycDocuments.AsNoTracking().ToList())
        {
            if (!leadNames.TryGetValue(document.LeadId, out var lead))
            {
                continue;
            }

            if (document.EffectiveStatus == KycStatuses.Expired)
            {
                alerts.Add(new Alert(
                    AlertKinds.KycExpired, AlertSeverities.Critical,
                    $"{document.DocumentType} expired",
                    $"{lead.FullName} ({lead.Company})",
                    "Lead", lead.Id.ToString(), $"/customers/{lead.Id}",
                    document.ExpiresOn?.ToDateTime(TimeOnly.MinValue)));
            }
            else if (document.Status == KycStatuses.Verified
                     && document.ExpiresOn is { } expiry
                     && expiry >= today && expiry <= horizon)
            {
                alerts.Add(new Alert(
                    AlertKinds.KycExpiring, AlertSeverities.Warning,
                    $"{document.DocumentType} expires soon",
                    $"{lead.FullName} — {(expiry.DayNumber - today.DayNumber)} day(s) left",
                    "Lead", lead.Id.ToString(), $"/customers/{lead.Id}",
                    expiry.ToDateTime(TimeOnly.MinValue)));
            }
        }

        // A customer with an open loan but no documents is a compliance risk worth raising.
        var leadsWithLoans = db.LoanApplications.AsNoTracking()
            .Where(loan => loan.Status != LoanStatuses.Rejected)
            .Select(loan => loan.LeadId)
            .Distinct()
            .ToList();

        var documentCounts = db.KycDocuments.AsNoTracking()
            .GroupBy(document => document.LeadId)
            .Select(group => new { LeadId = group.Key, Count = group.Count() })
            .ToDictionary(row => row.LeadId, row => row.Count);

        foreach (var leadId in leadsWithLoans)
        {
            if (documentCounts.GetValueOrDefault(leadId) >= RequiredDocuments.Length
                || !leadNames.TryGetValue(leadId, out var lead))
            {
                continue;
            }

            alerts.Add(new Alert(
                AlertKinds.KycMissing, AlertSeverities.Warning,
                "KYC incomplete with an open application",
                $"{lead.FullName} ({lead.Company})",
                "Lead", leadId.ToString(), $"/customers/{leadId}", null));
        }

        // --- Tickets: breached targets and those close to breaching ---
        var openTickets = db.Tickets.AsNoTracking()
            .Where(ticket => ticket.Status != TicketStatuses.Resolved && ticket.Status != TicketStatuses.Closed)
            .ToList();

        foreach (var ticket in openTickets)
        {
            if (ticket.DueAt < now)
            {
                alerts.Add(new Alert(
                    AlertKinds.SlaBreached, AlertSeverities.Critical,
                    $"{ticket.Reference} past its target",
                    $"{ticket.Subject} — {ticket.CustomerName}",
                    "Ticket", ticket.Id.ToString(), "/tickets", ticket.DueAt));
                continue;
            }

            var total = (ticket.DueAt - ticket.CreatedAt).TotalHours;
            var left = (ticket.DueAt - now).TotalHours;
            if (total > 0 && left / total <= AtRiskFraction)
            {
                alerts.Add(new Alert(
                    AlertKinds.SlaAtRisk, AlertSeverities.Warning,
                    $"{ticket.Reference} nearing its target",
                    $"{ticket.Subject} — {left:F1}h left",
                    "Ticket", ticket.Id.ToString(), "/tickets", ticket.DueAt));
            }
        }

        // --- Loans waiting on a human decision ---
        foreach (var loan in db.LoanApplications.AsNoTracking()
                     .Where(loan => loan.Status == LoanStatuses.Submitted || loan.Status == LoanStatuses.UnderReview)
                     .ToList())
        {
            var name = leadNames.TryGetValue(loan.LeadId, out var lead) ? lead.FullName : "Unknown";
            var waitingDays = (now - loan.CreatedAt).TotalDays;

            alerts.Add(new Alert(
                AlertKinds.LoanAwaitingDecision,
                waitingDays >= 3 ? AlertSeverities.Warning : AlertSeverities.Info,
                $"{loan.Reference} awaiting a decision",
                $"{name} — {loan.Amount:N0} JOD, {waitingDays:F0} day(s) waiting",
                "Loan", loan.Id.ToString(), "/loans", null));
        }

        // --- Deals closing within the week ---
        var weekOut = today.AddDays(7);
        foreach (var deal in db.Deals.AsNoTracking()
                     .Where(deal => deal.Stage != DealStages.Won && deal.Stage != DealStages.Lost
                                    && deal.ExpectedCloseDate <= weekOut)
                     .ToList())
        {
            alerts.Add(new Alert(
                AlertKinds.DealClosingSoon,
                deal.ExpectedCloseDate < today ? AlertSeverities.Warning : AlertSeverities.Info,
                deal.ExpectedCloseDate < today ? $"{deal.Name} is past its close date" : $"{deal.Name} closes soon",
                $"{deal.Company} — {deal.Value:N0} JOD",
                "Deal", deal.Id.ToString(), "/deals",
                deal.ExpectedCloseDate.ToDateTime(TimeOnly.MinValue)));
        }

        // --- Overdue tasks ---
        foreach (var activity in db.Activities.AsNoTracking()
                     .Where(activity => !activity.IsCompleted && activity.DueAt < now)
                     .ToList())
        {
            alerts.Add(new Alert(
                AlertKinds.TaskOverdue, AlertSeverities.Warning,
                $"Overdue: {activity.Title}",
                activity.RelatedTo,
                "Activity", activity.Id.ToString(), "/activities", activity.DueAt));
        }

        // Most urgent first, then soonest due.
        var order = new Dictionary<string, int>
        {
            [AlertSeverities.Critical] = 0,
            [AlertSeverities.Warning] = 1,
            [AlertSeverities.Info] = 2,
        };

        return alerts
            .OrderBy(alert => order[alert.Severity])
            .ThenBy(alert => alert.DueAt ?? DateTimeOffset.MaxValue)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
    }

    public OperationsReport GetOperationsReport()
    {
        var loans = db.LoanApplications.AsNoTracking().ToList();
        var tickets = db.Tickets.AsNoTracking().ToList();
        var leads = db.Leads.AsNoTracking().ToList();

        var approved = loans.Where(loan => loan.Status is LoanStatuses.Approved or LoanStatuses.Disbursed).ToList();
        var decided = loans.Where(loan => loan.Decision is not null).ToList();

        var resolved = tickets
            .Where(ticket => ticket.ResolvedAt is not null)
            .Select(ticket => (ticket.ResolvedAt!.Value - ticket.CreatedAt).TotalHours)
            .ToList();

        // Compliance is measured over tickets that have actually closed plus those
        // currently breached, so an untouched backlog cannot flatter the number.
        var closed = tickets.Where(ticket => ticket.ResolvedAt is not null).ToList();
        var metOnTime = closed.Count(ticket => ticket.ResolvedAt <= ticket.DueAt);
        var breached = tickets.Count(ticket => ticket.IsBreached);
        var slaDenominator = closed.Count + breached;

        return new OperationsReport(
            loans.Count,
            loans.Sum(loan => loan.Amount),
            approved.Sum(loan => loan.Amount),
            loans.Where(loan => loan.Status == LoanStatuses.Disbursed).Sum(loan => loan.Amount),
            decided.Count == 0 ? 0m : Math.Round(
                decided.Count(loan => loan.Decision == LoanStatuses.Approved) * 100m / decided.Count, 1),
            loans.Count == 0 ? 0m : Math.Round(loans.Sum(loan => loan.Amount) / loans.Count, 0),
            Group(loans, loan => loan.Status, loan => loan.Amount),
            Group(loans, loan => loan.Product, loan => loan.Amount),
            tickets.Count,
            tickets.Count(ticket => ticket.Status != TicketStatuses.Resolved && ticket.Status != TicketStatuses.Closed),
            breached,
            slaDenominator == 0 ? 100m : Math.Round(metOnTime * 100m / slaDenominator, 1),
            resolved.Count == 0 ? 0 : Math.Round(resolved.Average(), 1),
            Group(tickets, ticket => ticket.Category, _ => 0m),
            Group(tickets, ticket => ticket.Priority, _ => 0m),
            Group(leads, lead => lead.KycStatus, _ => 0m),
            leads.Count(lead => lead.KycStatus == KycStatuses.Verified));
    }

    private static IReadOnlyList<CountAndValue> Group<T>(
        IEnumerable<T> source,
        Func<T, string> key,
        Func<T, decimal> value) =>
        source
            .GroupBy(key)
            .Select(group => new CountAndValue(group.Key, group.Count(), group.Sum(value)))
            .OrderByDescending(row => row.Count)
            .ToList();
}
