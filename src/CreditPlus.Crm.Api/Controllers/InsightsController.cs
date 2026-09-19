using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/alerts")]
[Produces("application/json")]
[Authorize]
public sealed class AlertsController(IInsightService insights) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Alert>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Alert>> Get([FromQuery] int limit = 50) => Ok(insights.GetAlerts(limit));
}

[ApiController]
[Route("api/crm/reports")]
[Produces("application/json")]
[Authorize]
public sealed class OperationsReportController(IInsightService insights) : ControllerBase
{
    [HttpGet("operations")]
    [ProducesResponseType<OperationsReport>(StatusCodes.Status200OK)]
    public ActionResult<OperationsReport> Get() => Ok(insights.GetOperationsReport());
}

[ApiController]
[Route("api/crm/export")]
[Authorize(Policy = Roles.CanReadAudit)]
public sealed class ExportController(IExportService exports) : ControllerBase
{
    /// <summary>Exporting customer data is a manager-level action and is audited.</summary>
    [HttpGet("{dataset}")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Get(string dataset)
    {
        var (fileName, content) = exports.Export(dataset);
        // The BOM makes Excel open UTF-8 correctly, which matters for Arabic names.
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(
            System.Text.Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv", fileName);
    }
}
