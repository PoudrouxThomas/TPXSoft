namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Thrown by Infrastructure when a database foreign-key constraint blocks an operation the
/// service layer already believed was safe (e.g. a race where a subfolder or document was
/// created under a folder between the service's own emptiness check and the delete). Kept
/// generic -- no Npgsql/EF Core types -- so Domain does not need to reference them; Infrastructure
/// catches the Postgres-specific exception (SqlState 23503) and rethrows this instead.
/// </summary>
public sealed class ForeignKeyConstraintViolationException : Exception
{
    public ForeignKeyConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
