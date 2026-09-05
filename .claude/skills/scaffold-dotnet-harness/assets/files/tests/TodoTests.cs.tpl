using __NAME__.Domain;

namespace __NAME__.UnitTests;

/// <summary>
/// Test names are the behavioural specification: <c>Method_Condition_Result</c> fails
/// the moment it stops being true, which a prose spec never does.
/// </summary>
public class TodoTests
{
    [Fact]
    public void Constructor_WithBlankTitle_Throws() =>
        Assert.Throws<ArgumentException>(() => new Todo("   "));

    [Fact]
    public void Constructor_WithPaddedTitle_TrimsIt() =>
        Assert.Equal("Write the harness", new Todo("  Write the harness  ").Title);

    [Fact]
    public void Complete_MarksTheTodoCompleted()
    {
        var todo = new Todo("Write the harness");

        todo.Complete();

        Assert.True(todo.IsCompleted);
    }
}
