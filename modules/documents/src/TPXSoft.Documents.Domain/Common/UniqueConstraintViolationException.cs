namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Thrown by Infrastructure when a database unique constraint blocks an insert that a
/// check-then-insert alone could not rule out -- e.g. two concurrent
/// POST /documents/{id}/shares requests for the same (document, user) pair racing
/// (documentation/04-sharing-and-visibility.md's "second grant... is 409" rule: must be backed by
/// the unique index, not just a check). Kept generic -- no Npgsql/EF Core types -- so Domain does
/// not need to reference them; Infrastructure catches the Postgres-specific exception (SqlState
/// 23505) and rethrows this instead. Mirrors ForeignKeyConstraintViolationException.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
