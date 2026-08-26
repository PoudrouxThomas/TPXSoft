using Microsoft.EntityFrameworkCore;
using Npgsql;
using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly DocumentsDbContext _dbContext;

    public EfUnitOfWork(DocumentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" } pgException)
        {
            // documents.folder_id and folders.parent_folder_id are both ON DELETE RESTRICT --
            // a race that slips past a service-level emptiness check surfaces here instead of
            // becoming an unhandled 500 (documentation 07).
            throw new ForeignKeyConstraintViolationException(
                "A database foreign-key constraint blocked this change.", pgException);
        }
    }
}
