using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TPXSoft.Auth.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real Api host (via the `public partial class Program;` marker at the bottom of
/// Program.cs) against a real Postgres database instead of appsettings' localhost default.
/// Relies on Auth:ApplyMigrationsAtStartup -- set true here -- to run the same migration path a
/// real deployment does.
/// </summary>
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public AuthWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AuthDb"] = _connectionString,
                ["Auth:Jwt:Issuer"] = "tpxsoft-auth-integration-tests",
                ["Auth:Jwt:Audience"] = "tpxsoft-auth-integration-tests",
                ["Auth:Jwt:SigningKey"] = "integration-test-signing-key-0123456789ab",
                ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Auth:RefreshTokenLifetimeDays"] = "7",
                ["Auth:ApplyMigrationsAtStartup"] = "true"
            });
        });
    }
}
