namespace TPXSoft.Documents.Domain.Entities;

public sealed class Folder
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private Folder()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    /// <summary>Self-referencing FK. Null means the folder sits at the owner's root -- root is
    /// not a row, there is no synthetic root folder per user.</summary>
    public Guid? ParentFolderId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <param name="name">Must already be validated/normalized by the caller (see
    /// Domain.Common.FolderName).</param>
    public static Folder Create(Guid ownerUserId, string name, Guid? parentFolderId, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();

        return new Folder
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ParentFolderId = parentFolderId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <param name="name">Must already be validated/normalized by the caller.</param>
    public void Rename(string name, TimeProvider timeProvider)
    {
        Name = name;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    /// <param name="parentFolderId">Null moves the folder to root. Caller is responsible for
    /// ownership and cycle checks before calling this.</param>
    public void MoveTo(Guid? parentFolderId, TimeProvider timeProvider)
    {
        ParentFolderId = parentFolderId;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
