using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy("crm-web", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddDbContext<CrmDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CrmDatabase")));

// Scoped: each request gets its own DbContext.
builder.Services.AddScoped<ICrmStore, PostgresCrmStore>();
builder.Services.AddScoped<ICrmDashboardService, CrmDashboardService>();

var app = builder.Build();

CrmDbSeeder.MigrateAndSeed(app.Services);

// Translate store-level failures into ProblemDetails so controllers stay free of try/catch.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        CrmValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
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
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

app.Run();
