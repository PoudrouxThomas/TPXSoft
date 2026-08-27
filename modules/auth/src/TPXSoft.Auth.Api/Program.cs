using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Auth.Api;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Api.Endpoints;
using TPXSoft.Auth.Infrastructure;
using TPXSoft.Auth.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddAuthApi();
builder.Services.AddAuthApiCors(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Auth:ApplyMigrationsAtStartup"))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.Migrate();
}

app.UseCors(ServiceCollectionExtensions.AuthCorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Minimal API JSON body binding fails silently with an empty-bodied 400 (e.g. malformed JSON,
// a required field missing). Rewrite that into the contract's Error shape instead of leaving
// callers with an empty body. Any handler that already wrote its own body (validation failures,
// the JwtBearer challenge, etc.) has started the response by the time next() returns, so it's
// left untouched.
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == StatusCodes.Status400BadRequest && !context.Response.HasStarted)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Malformed request body."));
    }
});

app.MapAuthEndpoints();

app.Run();

public partial class Program;
