namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Thrown by Infrastructure when a database update or delete affects zero rows because another
/// request already changed or removed the same row first -- the classic "two simultaneous
/// deletes race" case (documentation/03-rename-move-delete-document.md's "Concurrency" section:
/// the loser's SaveChangesAsync must not surface as an unhandled 500). Kept generic -- no EF Core
/// types -- so Domain does not need to reference them; Infrastructure catches
/// DbUpdateConcurrencyException and rethrows this instead. Mirrors
/// ForeignKeyConstraintViolationException.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
