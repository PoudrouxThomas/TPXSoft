using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TPXSoft.Documents.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real Api host (via the `public partial class Program;` marker at the bottom of
/// Program.cs) against a real Postgres database. Relies on Documents:ApplyMigrationsAtStartup --
/// set true here -- to run the same migration path a real deployment does. The Jwt:* values here
/// must match what <see cref="TestTokens"/> signs with, since this module validates tokens rather
/// than issuing its own. Mirrors TPXSoft.Auth.IntegrationTests.Fixtures.AuthWebApplicationFactory.
/// </summary>
public sealed class DocumentsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public DocumentsWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DocumentsDb"] = _connectionString,
                ["Documents:Jwt:Issuer"] = TestTokens.Issuer,
                ["Documents:Jwt:Audience"] = TestTokens.Audience,
                ["Documents:Jwt:SigningKey"] = TestTokens.SigningKey,
                ["Documents:MaxUploadBytes"] = "26214400",
                ["Documents:ApplyMigrationsAtStartup"] = "true"
            });
        });
    }
}
