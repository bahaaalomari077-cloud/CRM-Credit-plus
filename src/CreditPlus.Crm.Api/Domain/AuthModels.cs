namespace CreditPlus.Crm.Api.Domain;

/// <summary>
/// Roles are ordered by privilege. Viewer reads, Agent owns records,
/// Manager sees everything, Admin also manages users and reads the audit trail.
/// </summary>
public static class Roles
{
    public const string Viewer = "Viewer";
    public const string Agent = "Agent";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Viewer, Agent, Manager, Admin];

    /// <summary>Policy names used by [Authorize(Policy = ...)].</summary>
    public const string CanWrite = "CanWrite";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanReadAudit = "CanReadAudit";

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string role) =>
        All.FirstOrDefault(known => known.Equals(role, StringComparison.OrdinalIgnoreCase)) ?? Viewer;
}

public sealed record User(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, User User);

public sealed record CreateUserRequest(string Email, string FullName, string Password, string Role);

public sealed record UpdateUserRequest(string FullName, string Role, bool IsActive);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>One immutable record of a change, written after the change commits.</summary>
public sealed record AuditEntry(
    long Id,
    DateTimeOffset OccurredAt,
    Guid? UserId,
    string UserEmail,
    string Action,
    string EntityType,
    string EntityId,
    string? Summary,
    string? Changes,
    string? IpAddress);

public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string StageChanged = "StageChanged";
    public const string LoggedIn = "LoggedIn";
    public const string LoginFailed = "LoginFailed";
    public const string EmailSent = "EmailSent";
    public const string PasswordChanged = "PasswordChanged";
}
