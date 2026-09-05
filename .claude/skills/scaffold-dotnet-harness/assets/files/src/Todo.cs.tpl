namespace __NAME__.Domain;

/// <summary>
/// Sample entity. Delete it once real endpoints exist -- it is here so the harness has
/// something to verify, not because the domain is about todos.
/// </summary>
public sealed class Todo
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Title { get; private set; } = string.Empty;

    public bool IsCompleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Todo() { }

    public Todo(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
    }

    public void Complete() => IsCompleted = true;
}
