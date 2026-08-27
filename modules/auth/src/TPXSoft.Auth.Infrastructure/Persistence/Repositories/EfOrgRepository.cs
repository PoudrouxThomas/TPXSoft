using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Infrastructure.Persistence.Repositories;

public sealed class EfOrgRepository : IOrgRepository
{
    private readonly AuthDbContext _dbContext;

    public EfOrgRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Org org) => _dbContext.Orgs.Add(org);

    public Task<Org?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Orgs.FindAsync([id], cancellationToken).AsTask();
}
