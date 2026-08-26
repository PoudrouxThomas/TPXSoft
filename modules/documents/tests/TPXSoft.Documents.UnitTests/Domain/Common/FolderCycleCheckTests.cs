using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>Exercises FolderCycleCheck directly with an in-memory ancestor-lookup delegate --
/// no database, per documentation/07-manage-folders.md's "Tests" section and the type's own doc
/// comment (that's exactly why getParentId is injected).</summary>
public sealed class FolderCycleCheckTests
{
    // Tree: a (root) -> b (child of a) -> c (child of b, i.e. grandchild of a). e is an
    // unrelated root-level sibling.
    private static (Guid A, Guid B, Guid C, Guid E, Func<Guid, CancellationToken, Task<Guid?>> GetParentId) BuildTree()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var e = Guid.NewGuid();

        var parents = new Dictionary<Guid, Guid?>
        {
            [a] = null,
            [b] = a,
            [c] = b,
            [e] = null
        };

        Task<Guid?> GetParentId(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(parents.GetValueOrDefault(id));

        return (a, b, c, e, GetParentId);
    }

    [Fact]
    public async Task WouldCreateCycle_MovingFolderUnderItself_ReturnsTrue()
    {
        var (a, _, _, _, getParentId) = BuildTree();

        var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(a, a, getParentId, CancellationToken.None);

        Assert.True(wouldCycle);
    }

    [Fact]
    public async Task WouldCreateCycle_MovingFolderUnderItsOwnChild_ReturnsTrue()
    {
        var (a, b, _, _, getParentId) = BuildTree();

        var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(a, b, getParentId, CancellationToken.None);

        Assert.True(wouldCycle);
    }

    [Fact]
    public async Task WouldCreateCycle_MovingFolderUnderItsOwnGrandchild_ReturnsTrue()
    {
        var (a, _, c, _, getParentId) = BuildTree();

        var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(a, c, getParentId, CancellationToken.None);

        Assert.True(wouldCycle);
    }

    [Fact]
    public async Task WouldCreateCycle_MovingFolderUnderAnUnrelatedSibling_ReturnsFalse()
    {
        var (a, _, _, e, getParentId) = BuildTree();

        var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(a, e, getParentId, CancellationToken.None);

        Assert.False(wouldCycle);
    }

    [Fact]
    public async Task WouldCreateCycle_MovingAnUnrelatedFolderUnderTheDeepestNode_ReturnsFalse()
    {
        // Moving e under c (a's grandchild) is unrelated to e's own subtree -- no cycle.
        var (_, _, c, e, getParentId) = BuildTree();

        var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(e, c, getParentId, CancellationToken.None);

        Assert.False(wouldCycle);
    }

    [Fact]
    public async Task WouldCreateCycle_SyntheticCyclicChainThatNeverReachesTheMovedFolder_TerminatesAtMaxDepth()
    {
        // A parent chain that is already corrupt (cyclic) and never includes the folder being
        // moved -- MaxDepth's bounded loop is what stops this from hanging forever, not the
        // algorithm ever finding the moved folder.
        var movedFolderId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        var callCount = 0;

        Task<Guid?> GetParentId(Guid id, CancellationToken cancellationToken)
        {
            callCount++;
            if (id == newParentId)
            {
                return Task.FromResult<Guid?>(x);
            }

            return Task.FromResult<Guid?>(id == x ? y : x);
        }

        var walk = FolderCycleCheck.WouldCreateCycleAsync(movedFolderId, newParentId, GetParentId, CancellationToken.None);
        var winner = await Task.WhenAny(walk, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(walk, winner);
        Assert.False(await walk);
        Assert.Equal(FolderCycleCheck.MaxDepth, callCount);
    }
}
