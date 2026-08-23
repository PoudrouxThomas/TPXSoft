using Microsoft.AspNetCore.Identity;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Infrastructure.Security;

/// <summary>Wraps ASP.NET Core Identity's PBKDF2-based PasswordHasher&lt;User&gt;.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // PasswordHasher<TUser>'s default algorithm doesn't read the user argument at all -- it
    // exists only so custom IPasswordHasher<TUser> implementations could vary behavior per user.
    // User.Create is a public factory even though User's ctor is private, so this is a legitimate
    // way to obtain a throwaway instance to satisfy the API shape.
    private static readonly User PlaceholderUser =
        User.Create("password-hasher@internal.invalid", string.Empty, Guid.Empty, Role.Member, TimeProvider.System);

    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(PlaceholderUser, password);

    public bool Verify(string hash, string password)
    {
        var result = _inner.VerifyHashedPassword(PlaceholderUser, hash, password);

        // SuccessRehashNeeded means the hash was produced with older/weaker hasher parameters
        // but the password is still correct. Treated as success without rehashing-and-storing a
        // fresh hash -- a deliberate deferral for v1, not a TODO.
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
