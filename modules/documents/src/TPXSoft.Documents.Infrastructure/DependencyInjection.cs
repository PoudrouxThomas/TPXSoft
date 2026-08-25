using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TPXSoft.Documents.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers this module's Infrastructure layer. Empty until the first endpoint lands
    /// (via the new-endpoint skill) and gives it something real to wire -- DbContext,
    /// repositories, storage -- against contracts/documents.v1.yaml.
    /// </summary>
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
