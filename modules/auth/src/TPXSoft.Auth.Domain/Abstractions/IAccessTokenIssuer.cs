using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Abstractions;

public interface IAccessTokenIssuer
{
    /// <summary>Issues a signed access token (JWT) encoding <paramref name="user"/>'s identity.</summary>
    string Issue(User user);
}
