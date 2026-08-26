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
    private readonly long _maxUploadBytes;

    public DocumentsWebApplicationFactory(string connectionString, long maxUploadBytes = 26_214_400)
    {
        _connectionString = connectionString;
        _maxUploadBytes = maxUploadBytes;
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
                ["Documents:MaxUploadBytes"] = _maxUploadBytes.ToString(),
                ["Documents:ApplyMigrationsAtStartup"] = "true"
            });
        });
    }
}
