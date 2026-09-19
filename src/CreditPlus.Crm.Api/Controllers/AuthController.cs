using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Authorize]
public sealed class AuthController(IUserService users, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<LoginResponse> Login(LoginRequest request) => Ok(users.Login(request));

    /// <summary>Lets the client restore a session from a stored token.</summary>
    [HttpGet("me")]
    [ProducesResponseType<User>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<User> Me() =>
        currentUser.Id is { } id && users.FindUser(id) is { } user ? Ok(user) : Unauthorized();

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ChangePassword(ChangePasswordRequest request)
    {
        if (currentUser.Id is not { } id)
        {
            return Unauthorized();
        }

        users.ChangePassword(id, request);
        return NoContent();
    }
}

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(Policy = Roles.CanManageUsers)]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<User>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<User>> Get() => Ok(users.GetUsers());

    [HttpGet("roles")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetRoles() => Ok(Roles.All);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<User>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<User> GetById(Guid id) =>
        users.FindUser(id) is { } user ? Ok(user) : NotFound();

    [HttpPost]
    [ProducesResponseType<User>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<User> Create(CreateUserRequest request)
    {
        var user = users.CreateUser(request);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<User>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<User> Update(Guid id, UpdateUserRequest request) => Ok(users.UpdateUser(id, request));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => users.DeleteUser(id) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/audit")]
[Produces("application/json")]
[Authorize(Policy = Roles.CanReadAudit)]
public sealed class AuditController(IAuditLog audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuditEntry>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AuditEntry>> Get(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] int limit = 100) =>
        Ok(audit.Query(entityType, entityId, action, limit));
}

[ApiController]
[Route("api/emails")]
[Produces("application/json")]
[Authorize]
public sealed class EmailsController(IEmailService emails) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EmailMessage>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<EmailMessage>> Get(
        [FromQuery] string? relatedType,
        [FromQuery] Guid? relatedId,
        [FromQuery] int limit = 50) =>
        Ok(emails.GetMessages(relatedType, relatedId, limit));

    [HttpPost]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<EmailMessage>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailMessage>> Send(SendEmailRequest request, CancellationToken cancellationToken)
    {
        var message = await emails.SendAsync(request, cancellationToken);
        return Created($"api/emails/{message.Id}", message);
    }
}
