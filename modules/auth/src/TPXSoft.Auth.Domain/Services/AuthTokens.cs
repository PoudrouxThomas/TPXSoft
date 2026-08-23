namespace TPXSoft.Auth.Domain.Services;

/// <summary>The pair handed back to a caller after register/login/refresh.</summary>
public sealed record AuthTokens(string AccessToken, string RefreshToken);
