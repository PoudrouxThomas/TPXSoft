using TPXSoft.Auth.Domain.Common;

namespace TPXSoft.Auth.Domain.Entities;

public sealed class User
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Always stored normalized (see <see cref="Common.Email.Normalize"/>).</summary>
    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Guid OrgId { get; private set; }

    public Role Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <param name="normalizedEmail">Must already be normalized by the caller.</param>
    public static User Create(string normalizedEmail, string passwordHash, Guid orgId, Role role, TimeProvider timeProvider)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            OrgId = orgId,
            Role = role,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }
}
