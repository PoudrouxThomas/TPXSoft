using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    /// <summary>Only the users added via <see cref="Add"/> during the test -- excludes seeded ones.</summary>
    public List<User> Added { get; } = new();

    /// <summary>Pre-populates the repository as if this user already existed before the test ran.</summary>
    public void Seed(User user) => _users.Add(user);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Email == normalizedEmail));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Id == id));

    public void Add(User user)
    {
        Added.Add(user);
        _users.Add(user);
    }
}
