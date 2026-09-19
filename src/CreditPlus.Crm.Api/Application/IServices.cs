using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Application;

/// <summary>Raised when a signed-in user lacks the rights for an operation.</summary>
public sealed class CrmForbiddenException(string message) : Exception(message);

/// <summary>Who is making the current request, resolved from the bearer token.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    string Email { get; }
    string Name { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
}

public interface IAuditLog
{
    /// <summary>Records a change. Never throws: auditing must not fail the operation it describes.</summary>
    void Record(string action, string entityType, string entityId, string? summary = null, object? changes = null);

    IReadOnlyList<AuditEntry> Query(string? entityType, string? entityId, string? action, int limit);
}

public interface IUserService
{
    LoginResponse Login(LoginRequest request);
    IReadOnlyList<User> GetUsers();
    User? FindUser(Guid id);
    User CreateUser(CreateUserRequest request);
    User UpdateUser(Guid id, UpdateUserRequest request);
    bool DeleteUser(Guid id);
    void ChangePassword(Guid userId, ChangePasswordRequest request);
}

public interface IEmailService
{
    Task<EmailMessage> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<EmailMessage> GetMessages(string? relatedType, Guid? relatedId, int limit);
}
