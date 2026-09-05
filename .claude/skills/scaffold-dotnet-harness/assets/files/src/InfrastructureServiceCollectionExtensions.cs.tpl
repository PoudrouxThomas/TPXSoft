using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using __NAME__.Domain;

namespace __NAME__.Infrastructure;

/// <summary>
/// The single seam between the API and the database. Everything EF Core stops here, so
/// the API project never names a DbContext and the architecture test can say so.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITodoRepository, TodoRepository>();

        return services;
    }
}
