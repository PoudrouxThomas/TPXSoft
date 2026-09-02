using TPXSoft.Auth.Domain.Common;

namespace TPXSoft.Auth.Api.Contracts;

// Request/response shapes mirror contracts/auth.v1.yaml exactly (property names, casing handled
// by System.Text.Json's default camelCase policy). UserResponse is named to avoid colliding with
// the Domain User entity, even though the contract's schema is called "User".

public sealed record RegisterRequest(string Email, string Password, string OrgName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record TokenPair(string AccessToken, string RefreshToken);

public sealed record UserResponse(Guid Id, string Email, Guid OrgId, string OrgName, Role Role, DateTimeOffset CreatedAt);

public sealed record ErrorResponse(string Message);
