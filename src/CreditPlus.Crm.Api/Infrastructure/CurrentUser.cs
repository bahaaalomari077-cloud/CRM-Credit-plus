using System.Security.Claims;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? "anonymous";

    public string Name => Principal?.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";

    public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? Roles.Viewer;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>Prefers the proxy header so a deployment behind one still logs the real client.</summary>
    public string? IpAddress
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(forwarded)
                ? context.Connection.RemoteIpAddress?.ToString()
                : forwarded.Split(',')[0].Trim();
        }
    }
}
