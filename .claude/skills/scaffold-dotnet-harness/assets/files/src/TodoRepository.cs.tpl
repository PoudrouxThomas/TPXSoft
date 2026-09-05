using Microsoft.EntityFrameworkCore;
using __NAME__.Domain;

namespace __NAME__.Infrastructure;

internal sealed class TodoRepository(AppDbContext db) : ITodoRepository
{
    public async Task<IReadOnlyList<Todo>> ListAsync(bool? completed, CancellationToken cancellationToken)
    {
        var query = db.Todos.AsNoTracking();
        if (completed is not null)
        {
            query = query.Where(t => t.IsCompleted == completed);
        }

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<Todo?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Todos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(Todo todo, CancellationToken cancellationToken) =>
        await db.Todos.AddAsync(todo, cancellationToken);

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (todo is null)
        {
            return false;
        }

        db.Todos.Remove(todo);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
