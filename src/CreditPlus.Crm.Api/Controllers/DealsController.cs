using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/deals")]
[Produces("application/json")]
public sealed class DealsController(ICrmStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Deal>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Deal>> Get([FromQuery] string? search, [FromQuery] string? stage) =>
        Ok(store.GetDeals(search, stage));

    [HttpGet("stages")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> Stages() => Ok(DealStages.All);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Deal>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Deal> GetById(Guid id) =>
        store.FindDeal(id) is { } deal ? Ok(deal) : NotFound();

    [HttpPost]
    [ProducesResponseType<Deal>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Deal> Create(CreateDealRequest request)
    {
        var deal = store.AddDeal(request);
        return CreatedAtAction(nameof(GetById), new { id = deal.Id }, deal);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<Deal>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Deal> Update(Guid id, UpdateDealRequest request) => Ok(store.UpdateDeal(id, request));

    [HttpPatch("{id:guid}/stage")]
    [ProducesResponseType<Deal>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Deal> MoveStage(Guid id, MoveStageRequest request) => Ok(store.MoveDeal(id, request.Stage));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => store.DeleteDeal(id) ? NoContent() : NotFound();
}

public sealed record MoveStageRequest(string Stage);
