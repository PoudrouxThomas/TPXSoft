using System.Net;
using System.Net.Http.Json;
using __NAME__.Api.Contracts;

namespace __NAME__.IntegrationTests;

/// <summary>
/// Traited so `npm run verify` never starts a container. These run in CI and on demand
/// via `npm run verify:it` -- the inner loop stays under a minute precisely because
/// this suite is not in it.
/// </summary>
[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public class TodoApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_ThenGet_ReturnsTheCreatedTodo()
    {
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/todos", new CreateTodoRequest("Ship the harness"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var todo = await created.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.NotNull(todo);

        var fetched = await client.GetFromJsonAsync<TodoResponse>($"/todos/{todo.Id}");
        Assert.Equal("Ship the harness", fetched?.Title);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        var response = await factory.CreateClient().GetAsync(new Uri($"/todos/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithBlankTitle_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/todos", new CreateTodoRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
