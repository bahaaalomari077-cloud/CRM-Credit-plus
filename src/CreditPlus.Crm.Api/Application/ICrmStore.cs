using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Application;

/// <summary>Raised when a request fails validation; surfaced as a 400 ProblemDetails.</summary>
public sealed class CrmValidationException(string message) : Exception(message);

public interface ICrmStore
{
    IReadOnlyList<Lead> GetLeads(string? search = null, string? status = null);
    Lead? FindLead(Guid id);
    Lead AddLead(CreateLeadRequest request);
    Lead UpdateLead(Guid id, UpdateLeadRequest request);
    bool DeleteLead(Guid id);

    IReadOnlyList<Deal> GetDeals(string? search = null, string? stage = null);
    Deal? FindDeal(Guid id);
    Deal AddDeal(CreateDealRequest request);
    Deal UpdateDeal(Guid id, UpdateDealRequest request);
    Deal MoveDeal(Guid id, string stage);
    bool DeleteDeal(Guid id);

    IReadOnlyList<Activity> GetActivities(bool? completed = null);
    Activity? FindActivity(Guid id);
    Activity AddActivity(CreateActivityRequest request);
    Activity UpdateActivity(Guid id, UpdateActivityRequest request);
    Activity SetActivityCompletion(Guid id, bool isCompleted);
    bool DeleteActivity(Guid id);
}

public interface ICrmDashboardService
{
    DashboardSummary GetDashboard();
}
