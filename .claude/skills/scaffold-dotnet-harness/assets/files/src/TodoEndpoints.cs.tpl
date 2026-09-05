using Microsoft.AspNetCore.Http.HttpResults;
using __NAME__.Api.Contracts;
using __NAME__.Domain;

namespace __NAME__.Api.Endpoints;

/// <summary>
/// Minimal APIs, grouped per resource, one static handler per operation.
///
/// Handlers return <c>Results&lt;...&gt;</c> unions rather than <c>IResult</c> because the
/// union is what tells the OpenAPI generator which status codes exist -- so the document
/// stays accurate without anyone maintaining a parallel list of <c>.Produces()</c> calls.
/// </summary>
public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/todos").WithTags("Todos");

        // WithName sets operationId, which is the method name in every generated client.
        group.MapGet("/", ListAsync).WithName("ListTodos");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetTodo");
        group.MapPost("/", CreateAsync).WithName("CreateTodo");
        group.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteTodo");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteTodo");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<TodoResponse>>> ListAsync(
        ITodoRepository repository,
        bool? completed,
        CancellationToken cancellationToken)
    {
        var todos = await repository.ListAsync(completed, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TodoResponse>>([.. todos.Select(TodoResponse.From)]);
    }

    private static async Task<Results<Ok<TodoResponse>, NotFound>> GetAsync(
        ITodoRepository repository,
        Guid id,
        CancellationToken cancellationToken)
    {
        var todo = await repository.FindAsync(id, cancellationToken);
        return todo is null ? TypedResults.NotFound() : TypedResults.Ok(TodoResponse.From(todo));
    }

    private static async Task<Results<Created<TodoResponse>, ValidationProblem>> CreateAsync(
        ITodoRepository repository,
        CreateTodoRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["title"] = ["Title is required."] });
        }

        var todo = new Todo(request.Title);
        await repository.AddAsync(todo, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/todos/{todo.Id}", TodoResponse.From(todo));
    }

    private static async Task<Results<NoContent, NotFound>> CompleteAsync(
        ITodoRepository repository,
        Guid id,
        CancellationToken cancellationToken)
    {
        var todo = await repository.FindAsync(id, cancellationToken);
        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        todo.Complete();
        await repository.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        ITodoRepository repository,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await repository.RemoveAsync(id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await repository.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
