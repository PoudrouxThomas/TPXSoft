namespace TPXSoft.Auth.Domain.Entities;

public sealed class Org
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private Org()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Org Create(string name, TimeProvider timeProvider)
    {
        return new Org
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }
}
