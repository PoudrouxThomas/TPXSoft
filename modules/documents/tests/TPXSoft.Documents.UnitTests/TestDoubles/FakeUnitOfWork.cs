using TPXSoft.Documents.Domain.Abstractions;

namespace TPXSoft.Documents.UnitTests.TestDoubles;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    /// <summary>When set, SaveChangesAsync throws this instead of succeeding -- used to simulate
    /// the ON DELETE RESTRICT race FolderService.DeleteAsync catches
    /// (ForeignKeyConstraintViolationException) without needing a real database.</summary>
    public Exception? ThrowOnSaveChanges { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;

        if (ThrowOnSaveChanges is { } exception)
        {
            throw exception;
        }

        return Task.CompletedTask;
    }
}
