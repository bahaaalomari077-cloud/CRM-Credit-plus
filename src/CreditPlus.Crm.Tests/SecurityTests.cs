using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;

namespace CreditPlus.Crm.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void A_hash_never_contains_the_password()
    {
        var hash = PasswordHasher.Hash("Admin123!");

        Assert.DoesNotContain("Admin123", hash);
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time()
    {
        // Distinct salts, so two accounts sharing a password do not share a hash.
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }

    [Fact]
    public void The_correct_password_verifies()
    {
        Assert.True(PasswordHasher.Verify("Admin123!", PasswordHasher.Hash("Admin123!")));
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("admin123!")]
    [InlineData("Admin123")]
    [InlineData("")]
    public void A_wrong_password_does_not_verify(string attempt)
    {
        Assert.False(PasswordHasher.Verify(attempt, PasswordHasher.Hash("Admin123!")));
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("1.2")]
    [InlineData("abc.def.ghi")]
    public void A_malformed_stored_hash_fails_closed(string stored)
    {
        // A corrupt row must refuse the login, never throw or accidentally pass.
        Assert.False(PasswordHasher.Verify("anything", stored));
    }
}

public sealed class UserSecurityTests : IDisposable
{
    // Rules like "the last administrator" depend on the whole table, so each test
    // gets its own database instead of sharing one across the class.
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    private (UserService Users, CrmDbContext Context) Arrange(ICurrentUser? actor = null)
    {
        var context = database.CreateContext();
        return (database.CreateUsers(context, actor), context);
    }

    [Fact]
    public void A_created_password_is_stored_hashed()
    {
        var (users, context) = Arrange();
        var created = users.CreateUser(new CreateUserRequest("a@test.local", "A Person", "secret123", Roles.Agent));

        var stored = context.Users.Single(user => user.Id == created.Id).PasswordHash;

        Assert.DoesNotContain("secret123", stored);
        Assert.StartsWith("210000.", stored);
    }

    [Fact]
    public void An_unknown_account_and_a_wrong_password_give_the_same_message()
    {
        var (users, _) = Arrange();
        users.CreateUser(new CreateUserRequest("known@test.local", "Known", "rightpass", Roles.Agent));

        var unknown = Assert.Throws<CrmValidationException>(
            () => users.Login(new LoginRequest("nobody@test.local", "whatever")));
        var wrong = Assert.Throws<CrmValidationException>(
            () => users.Login(new LoginRequest("known@test.local", "wrongpass")));

        // Identical wording, so the response cannot be used to enumerate accounts.
        Assert.Equal(unknown.Message, wrong.Message);
    }

    [Fact]
    public void A_disabled_account_cannot_sign_in()
    {
        var (users, _) = Arrange();
        var created = users.CreateUser(new CreateUserRequest("x@test.local", "X", "password1", Roles.Agent));
        users.UpdateUser(created.Id, new UpdateUserRequest("X", Roles.Agent, false));

        Assert.Throws<CrmForbiddenException>(() => users.Login(new LoginRequest("x@test.local", "password1")));
    }

    [Fact]
    public void A_duplicate_email_is_refused()
    {
        var (users, _) = Arrange();
        users.CreateUser(new CreateUserRequest("dup@test.local", "First", "password1", Roles.Agent));

        Assert.Throws<CrmValidationException>(
            () => users.CreateUser(new CreateUserRequest("DUP@test.local", "Second", "password2", Roles.Agent)));
    }

    [Fact]
    public void The_last_administrator_cannot_be_demoted()
    {
        var (users, context) = Arrange();
        var admin = users.CreateUser(new CreateUserRequest("only@test.local", "Only Admin", "password1", Roles.Admin));

        // Act as somebody else, so the self-demotion rule is not what blocks it.
        var other = database.CreateUsers(context, new FakeCurrentUser("Someone Else"));

        Assert.Throws<CrmValidationException>(
            () => other.UpdateUser(admin.Id, new UpdateUserRequest("Only Admin", Roles.Agent, true)));
    }

    [Fact]
    public void The_last_administrator_cannot_be_deleted()
    {
        var (users, context) = Arrange();
        var admin = users.CreateUser(new CreateUserRequest("last@test.local", "Last Admin", "password1", Roles.Admin));
        var other = database.CreateUsers(context, new FakeCurrentUser("Someone Else"));

        Assert.Throws<CrmValidationException>(() => other.DeleteUser(admin.Id));
    }

    [Fact]
    public void An_administrator_cannot_remove_their_own_access()
    {
        var context = database.CreateContext();
        var bootstrap = database.CreateUsers(context);
        var me = bootstrap.CreateUser(new CreateUserRequest("me@test.local", "Me", "password1", Roles.Admin));
        bootstrap.CreateUser(new CreateUserRequest("spare@test.local", "Spare", "password1", Roles.Admin));

        // Now act as that administrator.
        var asMe = database.CreateUsers(context, new SpecificUser(me.Id, Roles.Admin));

        var failure = Assert.Throws<CrmValidationException>(
            () => asMe.UpdateUser(me.Id, new UpdateUserRequest("Me", Roles.Viewer, true)));

        Assert.Contains("your own administrator access", failure.Message);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void A_weak_password_is_refused(string password)
    {
        var (users, _) = Arrange();

        Assert.Throws<CrmValidationException>(
            () => users.CreateUser(new CreateUserRequest("weak@test.local", "Weak", password, Roles.Agent)));
    }

    [Fact]
    public void An_invalid_email_is_refused()
    {
        var (users, _) = Arrange();

        Assert.Throws<CrmValidationException>(
            () => users.CreateUser(new CreateUserRequest("not-an-email", "X", "password1", Roles.Agent)));
    }

    [Fact]
    public void An_unknown_role_is_refused()
    {
        var (users, _) = Arrange();

        Assert.Throws<CrmValidationException>(
            () => users.CreateUser(new CreateUserRequest("r@test.local", "R", "password1", "Superuser")));
    }

    [Fact]
    public void Changing_a_password_requires_the_current_one()
    {
        var (users, _) = Arrange();
        var created = users.CreateUser(new CreateUserRequest("c@test.local", "C", "oldpass1", Roles.Agent));

        Assert.Throws<CrmValidationException>(
            () => users.ChangePassword(created.Id, new ChangePasswordRequest("guessed", "newpass1")));

        users.ChangePassword(created.Id, new ChangePasswordRequest("oldpass1", "newpass1"));
        Assert.NotNull(users.Login(new LoginRequest("c@test.local", "newpass1")));
    }

    /// <summary>A current user with a caller-chosen id, for self-action rules.</summary>
    private sealed class SpecificUser(Guid id, string role) : ICurrentUser
    {
        public Guid? Id => id;
        public string Email => "me@test.local";
        public string Name => "Me";
        public string Role => role;
        public bool IsAuthenticated => true;
        public string? IpAddress => "127.0.0.1";
    }
}

public sealed class ExportSafetyTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase database;

    public ExportSafetyTests(TestDatabase database) => this.database = database;

    [Fact]
    public void A_value_that_looks_like_a_formula_is_neutralised()
    {
        var context = database.CreateContext();
        // A spreadsheet would execute this on open if it were exported bare.
        database.SeedLead(context, name: "=cmd|'/c calc'!A1", company: "Test");

        var (_, content) = database.CreateExports(context).Export("leads");

        Assert.DoesNotContain("\n=cmd", content);
        Assert.Contains("'=cmd", content);
    }

    [Fact]
    public void A_value_containing_a_comma_is_quoted()
    {
        var context = database.CreateContext();
        database.SeedLead(context, name: "Odeh, Rania", company: "Irbid Foods");

        var (_, content) = database.CreateExports(context).Export("leads");

        Assert.Contains("\"Odeh, Rania\"", content);
    }

    [Fact]
    public void An_unknown_dataset_is_refused()
    {
        var context = database.CreateContext();

        Assert.Throws<CrmValidationException>(() => database.CreateExports(context).Export("passwords"));
    }

    [Theory]
    [InlineData("leads")]
    [InlineData("deals")]
    [InlineData("loans")]
    [InlineData("tickets")]
    [InlineData("kyc")]
    [InlineData("audit")]
    public void Every_advertised_dataset_exports(string dataset)
    {
        var context = database.CreateContext();

        var (fileName, content) = database.CreateExports(context).Export(dataset);

        Assert.StartsWith(dataset, fileName);
        Assert.EndsWith(".csv", fileName);
        Assert.NotEmpty(content);
    }
}
