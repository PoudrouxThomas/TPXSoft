namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Cycle detection for PATCH /folders/{id} (move). Moving a folder into its own subtree would
/// orphan that subtree into a detached ring -- it would disappear from every listing (nothing
/// links back to root) while still existing. Walks upward from the proposed new parent toward
/// root and reports a cycle if the folder being moved is encountered along the way. Upward is
/// O(depth) with a bounded loop; a downward walk would be O(subtree).
///
/// getParentId is injected (rather than this type querying a DbContext directly) so the whole
/// algorithm -- including the depth-cap termination case -- is unit-testable with an in-memory
/// fake and no database.
/// </summary>
public static class FolderCycleCheck
{
    /// <summary>
    /// Hard cap on how many ancestors this walks before giving up and reporting "no cycle
    /// found". Guards against looping forever if the data somehow already is cyclic.
    /// </summary>
    public const int MaxDepth = 256;

    public static async Task<bool> WouldCreateCycleAsync(
        Guid folderBeingMovedId,
        Guid newParentId,
        Func<Guid, CancellationToken, Task<Guid?>> getParentId,
        CancellationToken cancellationToken)
    {
        // self == newParent is also caught by the walk below (depth 0), but checked first for a
        // clearer, cheaper failure.
        if (newParentId == folderBeingMovedId)
        {
            return true;
        }

        Guid? currentId = newParentId;
        for (var depth = 0; depth < MaxDepth && currentId is not null; depth++)
        {
            if (currentId.Value == folderBeingMovedId)
            {
                return true;
            }

            currentId = await getParentId(currentId.Value, cancellationToken);
        }

        return false;
    }
}
