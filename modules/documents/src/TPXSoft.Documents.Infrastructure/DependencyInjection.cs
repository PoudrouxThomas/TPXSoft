using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Services;
using TPXSoft.Documents.Infrastructure.Options;
using TPXSoft.Documents.Infrastructure.Persistence;
using TPXSoft.Documents.Infrastructure.Persistence.Repositories;

namespace TPXSoft.Documents.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything this module's Infrastructure layer provides: the EF Core DbContext,
    /// repository/unit-of-work implementations, and the bound + validated Jwt options. Does not
    /// touch ASP.NET authentication/authorization -- that's wired by the Api project, which
    /// knows about HTTP.
    /// </summary>
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DocumentsDb")));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Documents:Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IFolderRepository, EfFolderRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<FolderService>();
        services.AddScoped<DocumentService>();

        return services;
    }
}
