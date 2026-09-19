using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "CreditPlus.Crm";
    public string Audience { get; set; } = "CreditPlus.Crm.Web";
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;
}

public sealed class UserService(
    CrmDbContext db,
    IAuditLog audit,
    ICurrentUser currentUser,
    JwtOptions jwt) : IUserService
{
    public LoginResponse Login(LoginRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var user = db.Users.FirstOrDefault(candidate => candidate.Email == email);

        // The same message for a missing account and a wrong password, so the
        // response cannot be used to discover which addresses are registered.
        if (user is null || !PasswordHasher.Verify(request.Password ?? "", user.PasswordHash))
        {
            audit.Record(AuditActions.LoginFailed, "User", email, $"Failed sign-in for {email}");
            throw new CrmValidationException("Email or password is incorrect.");
        }

        if (!user.IsActive)
        {
            audit.Record(AuditActions.LoginFailed, "User", user.Id.ToString(), "Sign-in on a disabled account");
            throw new CrmForbiddenException("This account is disabled.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        db.SaveChanges();

        audit.Record(AuditActions.LoggedIn, "User", user.Id.ToString(), $"{user.Email} signed in");

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.ExpiryMinutes);
        return new LoginResponse(CreateToken(user, expiresAt), expiresAt, user.ToRecord());
    }

    public IReadOnlyList<User> GetUsers() =>
        db.Users.AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => user.ToRecord())
            .ToList();

    public User? FindUser(Guid id) =>
        db.Users.AsNoTracking().FirstOrDefault(user => user.Id == id)?.ToRecord();

    public User CreateUser(CreateUserRequest request)
    {
        var email = RequiredEmail(request.Email);

        if (db.Users.Any(user => user.Email == email))
        {
            throw new CrmValidationException("An account with that email already exists.");
        }

        var entity = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = Required(request.FullName, "Full name"),
            PasswordHash = PasswordHasher.Hash(ValidPassword(request.Password)),
            Role = ValidRole(request.Role),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Users.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "User", entity.Id.ToString(),
            $"Created {entity.Email}", new { entity.Email, entity.FullName, entity.Role });

        return entity.ToRecord();
    }

    public User UpdateUser(Guid id, UpdateUserRequest request)
    {
        var entity = db.Users.FirstOrDefault(user => user.Id == id)
            ?? throw new KeyNotFoundException($"User {id} was not found.");

        var role = ValidRole(request.Role);

        // Without this an admin could demote or disable themselves and lock
        // everyone out of user management.
        if (entity.Id == currentUser.Id && (role != Roles.Admin || !request.IsActive))
        {
            throw new CrmValidationException("You cannot remove your own administrator access.");
        }

        if (entity.Role == Roles.Admin && role != Roles.Admin && CountActiveAdmins() <= 1)
        {
            throw new CrmValidationException("At least one active administrator must remain.");
        }

        var before = new { entity.FullName, entity.Role, entity.IsActive };
        entity.FullName = Required(request.FullName, "Full name");
        entity.Role = role;
        entity.IsActive = request.IsActive;
        db.SaveChanges();

        audit.Record(AuditActions.Updated, "User", entity.Id.ToString(),
            $"Updated {entity.Email}",
            new { before, after = new { entity.FullName, entity.Role, entity.IsActive } });

        return entity.ToRecord();
    }

    public bool DeleteUser(Guid id)
    {
        var entity = db.Users.FirstOrDefault(user => user.Id == id);
        if (entity is null)
        {
            return false;
        }

        if (entity.Id == currentUser.Id)
        {
            throw new CrmValidationException("You cannot delete your own account.");
        }

        if (entity.Role == Roles.Admin && CountActiveAdmins() <= 1)
        {
            throw new CrmValidationException("At least one active administrator must remain.");
        }

        db.Users.Remove(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Deleted, "User", id.ToString(), $"Deleted {entity.Email}");
        return true;
    }

    public void ChangePassword(Guid userId, ChangePasswordRequest request)
    {
        var entity = db.Users.FirstOrDefault(user => user.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (!PasswordHasher.Verify(request.CurrentPassword ?? "", entity.PasswordHash))
        {
            throw new CrmValidationException("Current password is incorrect.");
        }

        entity.PasswordHash = PasswordHasher.Hash(ValidPassword(request.NewPassword));
        db.SaveChanges();

        audit.Record(AuditActions.PasswordChanged, "User", entity.Id.ToString(), $"{entity.Email} changed password");
    }

    private int CountActiveAdmins() =>
        db.Users.Count(user => user.Role == Roles.Admin && user.IsActive);

    private string CreateToken(UserEntity user, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new CrmValidationException($"{field} is required.")
            : value.Trim();

    private static string RequiredEmail(string? value)
    {
        var email = Required(value, "Email").ToLowerInvariant();
        if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            throw new CrmValidationException("Email address is not valid.");
        }

        return email;
    }

    private static string ValidPassword(string? password) =>
        string.IsNullOrWhiteSpace(password) || password.Length < 6
            ? throw new CrmValidationException("Password must be at least 6 characters.")
            : password;

    private static string ValidRole(string? role) =>
        string.IsNullOrWhiteSpace(role) || !Roles.IsKnown(role)
            ? throw new CrmValidationException($"Role must be one of: {string.Join(", ", Roles.All)}.")
            : Roles.Normalize(role);
}
