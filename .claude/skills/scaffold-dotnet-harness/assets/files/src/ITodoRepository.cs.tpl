namespace __NAME__.Domain;

/// <summary>
/// Persistence is an interface here and an implementation in Infrastructure, which is
/// what lets the architecture test assert that nothing outside Infrastructure knows EF
/// Core exists.
/// </summary>
public interface ITodoRepository
{
    Task<IReadOnlyList<Todo>> ListAsync(bool? completed, CancellationToken cancellationToken);

    Task<Todo?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Todo todo, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
