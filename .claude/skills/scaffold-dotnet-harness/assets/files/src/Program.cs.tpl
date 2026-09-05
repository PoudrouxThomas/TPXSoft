using __NAME__.Api.Endpoints;
using __NAME__.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Failures leave as ProblemDetails, never as a stack trace: a leaked exception page is
// both an information leak and a response shape no generated client knows about.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();

app.MapTodoEndpoints();

await app.RunAsync();

#pragma warning disable CA1050 // The entry point of a top-level program has no namespace.
/// <summary>Named so the integration tests can boot the real application.</summary>
public partial class Program;
#pragma warning restore CA1050
