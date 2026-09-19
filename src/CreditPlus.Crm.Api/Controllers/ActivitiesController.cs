using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/activities")]
[Produces("application/json")]
[Authorize]
public sealed class ActivitiesController(ICrmStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Activity>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Activity>> Get([FromQuery] bool? completed) => Ok(store.GetActivities(completed));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Activity>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Activity> GetById(Guid id) =>
        store.FindActivity(id) is { } activity ? Ok(activity) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Activity>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Activity> Create(CreateActivityRequest request)
    {
        var activity = store.AddActivity(request);
        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, activity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Activity>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Activity> Update(Guid id, UpdateActivityRequest request) =>
        Ok(store.UpdateActivity(id, request));

    [HttpPatch("{id:guid}/complete")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Activity>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Activity> Complete(Guid id) => Ok(store.SetActivityCompletion(id, true));

    [HttpPatch("{id:guid}/reopen")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Activity>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Activity> Reopen(Guid id) => Ok(store.SetActivityCompletion(id, false));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => store.DeleteActivity(id) ? NoContent() : NotFound();
}
