using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/leads")]
[Produces("application/json")]
[Authorize]
public sealed class LeadsController(ICrmStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Lead>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Lead>> Get([FromQuery] string? search, [FromQuery] string? status) =>
        Ok(store.GetLeads(search, status));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Lead>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Lead> GetById(Guid id) =>
        store.FindLead(id) is { } lead ? Ok(lead) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Lead>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Lead> Create(CreateLeadRequest request)
    {
        var lead = store.AddLead(request);
        return CreatedAtAction(nameof(GetById), new { id = lead.Id }, lead);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Lead>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Lead> Update(Guid id, UpdateLeadRequest request) => Ok(store.UpdateLead(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => store.DeleteLead(id) ? NoContent() : NotFound();
}
