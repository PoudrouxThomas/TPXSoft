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
        catch (DbUpdateConcurrencyException ex)
        {
            // A concurrent request already changed or removed the same row -- e.g. two
            // simultaneous DELETEs on the same document racing (documentation
            // 03-rename-move-delete-document.md's "Concurrency" section). Must be caught before
            // the DbUpdateException clause below: DbUpdateConcurrencyException derives from it.
            throw new ConcurrencyConflictException(
                "A concurrent change already modified or removed this row.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" } pgException)
        {
            // documents.folder_id and folders.parent_folder_id are both ON DELETE RESTRICT --
            // a race that slips past a service-level emptiness check surfaces here instead of
            // becoming an unhandled 500 (documentation 07).
            throw new ForeignKeyConstraintViolationException(
                "A database foreign-key constraint blocked this change.", pgException);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pgException)
        {
            // document_shares' unique (document_id, granted_to_user_id) index -- two concurrent
            // POST /documents/{id}/shares requests for the same pair racing past the
            // service-level check-then-insert (documentation/04-sharing-and-visibility.md's
            // "second grant... is 409" rule).
            throw new UniqueConstraintViolationException(
                "A database unique constraint blocked this change.", pgException);
        }
    }
}
