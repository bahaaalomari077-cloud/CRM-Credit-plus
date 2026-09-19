using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>Derives every dashboard number from the stored deals, leads, and activities.</summary>
public sealed class CrmDashboardService(ICrmStore store) : ICrmDashboardService
{
    public DashboardSummary GetDashboard()
    {
        var leads = store.GetLeads();
        var deals = store.GetDeals();
        var activities = store.GetActivities();

        var openDeals = deals.Where(deal => DealStages.Open.Contains(deal.Stage)).ToList();
        var wonDeals = deals.Where(deal => deal.Stage == DealStages.Won).ToList();

        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var wonThisMonth = wonDeals
            .Where(deal => deal.ExpectedCloseDate >= monthStart)
            .Sum(deal => deal.Value);

        var closed = deals.Count(deal => deal.Stage is DealStages.Won or DealStages.Lost);
        var conversionRate = closed == 0
            ? 0m
            : Math.Round(wonDeals.Count * 100m / closed, 1);

        var pipeline = DealStages.Open
            .Append(DealStages.Won)
            .Select(stage =>
            {
                var stageDeals = deals.Where(deal => deal.Stage == stage).ToList();
                return new PipelineStage(
                    stage.ToLowerInvariant(),
                    stage,
                    stageDeals.Sum(deal => deal.Value),
                    stageDeals.Count,
                    DealStages.AccentFor(stage));
            })
            .ToList();

        return new DashboardSummary(
            leads.Count(lead => lead.Status == LeadStatuses.New),
            leads.Count,
            openDeals.Sum(deal => deal.Value),
            wonThisMonth,
            conversionRate,
            activities.Count(activity => !activity.IsCompleted),
            pipeline,
            openDeals,
            activities.Where(activity => !activity.IsCompleted).Take(6).ToList());
    }
}
