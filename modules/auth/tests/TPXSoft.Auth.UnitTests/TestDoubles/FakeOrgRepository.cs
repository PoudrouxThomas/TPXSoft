using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

/// <summary>In-memory stand-in for <see cref="IOrgRepository"/>: <see cref="Add"/> is visible
/// immediately, with no separate "SaveChanges" step -- good enough for AuthService's needs.</summary>
internal sealed class FakeOrgRepository : IOrgRepository
{
    public List<Org> Added { get; } = new();

    public void Add(Org org) => Added.Add(org);

    public Task<Org?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Added.FirstOrDefault(o => o.Id == id));
}
