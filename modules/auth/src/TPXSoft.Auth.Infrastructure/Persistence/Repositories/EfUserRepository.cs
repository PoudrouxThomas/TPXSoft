using Microsoft.EntityFrameworkCore;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Infrastructure.Persistence.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly AuthDbContext _dbContext;

    public EfUserRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        _dbContext.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public void Add(User user) => _dbContext.Users.Add(user);
}
