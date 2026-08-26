using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Infrastructure.Options;

namespace TPXSoft.Documents.Api;

public static class ServiceCollectionExtensions
{
    /// <summary>Wires ASP.NET Core JWT bearer authentication against JwtOptions (bound and
    /// validated by AddDocumentsInfrastructure). This module issues no tokens of its own -- it
    /// validates the same access tokens TPXSoft.Auth issues. HTTP-specific, so it lives in Api
    /// rather than Infrastructure.</summary>
    public static IServiceCollection AddDocumentsApi(this IServiceCollection services)
    {
        // Reading claims off a validated token (e.g. the "sub" claim) shouldn't get remapped to
        // long legacy URI claim types.
        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;

                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.Zero
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    // Replace the default empty-bodied 401 with the contract's Error shape.
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        return context.Response.WriteAsJsonAsync(new ErrorResponse("Missing or invalid access token."));
                    }
                };
            });

        services.AddAuthorization();

        // Reject oversized multipart bodies while ASP.NET reads the form, before the handler
        // ever allocates a buffer for the file bytes (documentation/01-upload-document.md's
        // "Streaming vs buffering" section).
        services.AddOptions<FormOptions>()
            .Configure<IOptions<DocumentsOptions>>((formOptions, documentsOptionsAccessor) =>
            {
                formOptions.MultipartBodyLengthLimit = documentsOptionsAccessor.Value.MaxUploadBytes;
            });

        return services;
    }
}
