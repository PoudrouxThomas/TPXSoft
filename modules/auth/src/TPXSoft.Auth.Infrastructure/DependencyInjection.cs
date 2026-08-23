using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Services;
using TPXSoft.Auth.Infrastructure.Options;
using TPXSoft.Auth.Infrastructure.Persistence;
using TPXSoft.Auth.Infrastructure.Persistence.Repositories;
using TPXSoft.Auth.Infrastructure.Security;

namespace TPXSoft.Auth.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything this module's Infrastructure layer provides: the EF Core DbContext,
    /// repository/unit-of-work implementations, password hashing, token issuance, and the bound
    /// + validated Jwt/AuthToken options. Does not touch ASP.NET authentication/authorization --
    /// that's wired by the Api project, which knows about HTTP.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AuthDb")));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Auth:Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthTokenOptions>()
            .Bind(configuration.GetSection("Auth"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IOrgRepository, EfOrgRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IRefreshTokenFactory, RefreshTokenFactory>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddScoped<AuthService>();

        return services;
    }
}
