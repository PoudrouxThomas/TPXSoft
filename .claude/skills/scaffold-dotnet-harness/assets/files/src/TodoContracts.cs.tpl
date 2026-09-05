using __NAME__.Domain;

namespace __NAME__.Api.Contracts;

/// <summary>
/// Wire types live here, separate from the domain entity. The entity is free to change
/// shape; this record is what the generated frontend client is compiled against, so a
/// change to it is a contract change and the verify step will say so.
/// </summary>
public sealed record TodoResponse(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAt)
{
    public static TodoResponse From(Todo todo)
    {
        ArgumentNullException.ThrowIfNull(todo);
        return new TodoResponse(todo.Id, todo.Title, todo.IsCompleted, todo.CreatedAt);
    }
}

public sealed record CreateTodoRequest(string Title);
