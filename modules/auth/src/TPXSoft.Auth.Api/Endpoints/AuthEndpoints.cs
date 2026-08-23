using System.Security.Claims;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Services;

namespace TPXSoft.Auth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, request.OrgName, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        var tokens = result.Value;
        return Results.Json(new TokenPair(tokens.AccessToken, tokens.RefreshToken), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        var tokens = result.Value;
        return Results.Ok(new TokenPair(tokens.AccessToken, tokens.RefreshToken));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        var tokens = result.Value;
        return Results.Ok(new TokenPair(tokens.AccessToken, tokens.RefreshToken));
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        // Idempotent by contract: always 204, even for an unknown/garbage token.
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal user, AuthService authService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Json(new ErrorResponse("Missing or invalid access token."), statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await authService.GetUserAsync(userId.Value, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        var domainUser = result.Value;
        return Results.Ok(new UserResponse(domainUser.Id, domainUser.Email, domainUser.OrgId, domainUser.Role));
    }

    private static IResult ErrorResult(AuthError error)
    {
        var (statusCode, message) = error.ToHttp();
        return Results.Json(new ErrorResponse(message), statusCode: statusCode);
    }
}
