using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TPXSoft.Auth.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` tooling (migrations, etc.) construct an AuthDbContext outside of the Api
/// host. Reads the connection string from AUTH_DB_CONNECTION, falling back to a localhost
/// default matching .env.example -- never used at application runtime.
/// </summary>
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=tpxsoft;Username=tpxsoft;Password=tpxsoft";

    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTH_DB_CONNECTION") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AuthDbContext(optionsBuilder.Options);
    }
}
