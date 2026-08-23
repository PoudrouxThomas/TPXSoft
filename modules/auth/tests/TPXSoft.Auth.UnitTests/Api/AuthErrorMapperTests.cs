using Microsoft.AspNetCore.Http;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Domain.Common;

namespace TPXSoft.Auth.UnitTests.Api;

public sealed class AuthErrorMapperTests
{
    [Theory]
    [InlineData(AuthError.EmailAlreadyRegistered, StatusCodes.Status409Conflict)]
    [InlineData(AuthError.InvalidCredentials, StatusCodes.Status401Unauthorized)]
    [InlineData(AuthError.InvalidRefreshToken, StatusCodes.Status401Unauthorized)]
    [InlineData(AuthError.ValidationFailed, StatusCodes.Status400BadRequest)]
    public void ToHttp_MapsEachAuthErrorToItsContractStatusCode(AuthError error, int expectedStatusCode)
    {
        var (statusCode, message) = error.ToHttp();

        Assert.Equal(expectedStatusCode, statusCode);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}
