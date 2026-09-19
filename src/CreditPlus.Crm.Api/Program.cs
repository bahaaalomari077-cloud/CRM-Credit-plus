using System.Text;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy("crm-web", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // Without this the browser hides the header and CSV downloads lose their filename.
    .WithExposedHeaders("Content-Disposition")));

var connectionString = builder.Configuration.GetConnectionString("CrmDatabase");
// The factory supplies short-lived contexts for the audit log, so its rows survive a
// rollback elsewhere; AddDbContext then reuses the factory for the per-request context.
builder.Services.AddPooledDbContextFactory<CrmDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<CrmDbContext>(provider =>
    provider.GetRequiredService<IDbContextFactory<CrmDbContext>>().CreateDbContext());

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be configured with at least 32 characters. " +
        "Set it in appsettings.json or as the Jwt__SigningKey environment variable.");
}

builder.Services.AddSingleton(jwt);

var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
builder.Services.AddSingleton(emailOptions);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            // Tokens expire exactly when they say they do, not five minutes later.
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.CanWrite, policy =>
        policy.RequireRole(Roles.Agent, Roles.Manager, Roles.Admin));
    options.AddPolicy(Roles.CanManageUsers, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(Roles.CanReadAudit, policy => policy.RequireRole(Roles.Manager, Roles.Admin));
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuditLog, AuditLog>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICrmStore, PostgresCrmStore>();
builder.Services.AddScoped<ICrmDashboardService, CrmDashboardService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IInsightService, InsightService>();
builder.Services.AddScoped<IExportService, ExportService>();

var app = builder.Build();

CrmDbSeeder.MigrateAndSeed(app.Services);

// Translate service-level failures into ProblemDetails so controllers stay free of try/catch.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        CrmValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        CrmForbiddenException => (StatusCodes.Status403Forbidden, "Not allowed"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
        _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
    };

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = status == StatusCodes.Status500InternalServerError ? null : exception?.Message
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("crm-web");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

app.Run();
