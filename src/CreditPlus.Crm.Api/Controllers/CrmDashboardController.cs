using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/dashboard")]
public sealed class CrmDashboardController(ICrmDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    public ActionResult<DashboardSummary> Get() => Ok(dashboardService.GetDashboard());
}
