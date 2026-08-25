using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TPXSoft.Documents.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` tooling (migrations, etc.) construct a DocumentsDbContext outside of the Api
/// host. Reads the connection string from DOCUMENTS_DB_CONNECTION, falling back to a localhost
/// default matching .env.example -- never used at application runtime.
/// </summary>
public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=tpxsoft;Username=tpxsoft;Password=tpxsoft";

    public DocumentsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOCUMENTS_DB_CONNECTION") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<DocumentsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DocumentsDbContext(optionsBuilder.Options);
    }
}
