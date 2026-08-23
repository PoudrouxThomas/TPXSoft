using TPXSoft.Auth.Domain.Abstractions;

namespace TPXSoft.Auth.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AuthDbContext _dbContext;

    public EfUnitOfWork(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
