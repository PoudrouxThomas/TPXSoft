using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public List<User> IssuedFor { get; } = new();

    public string Issue(User user)
    {
        IssuedFor.Add(user);
        return $"access-token-for-{user.Id}";
    }
}
