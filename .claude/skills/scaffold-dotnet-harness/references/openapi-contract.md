# The OpenAPI contract

## How the document is produced

`Microsoft.Extensions.ApiDescription.Server` hooks the build. After compiling, it loads the
API assembly, asks it for the document `AddOpenApi()` would serve, and writes it to disk —
no server starts, nothing listens on a port. Three properties in `<Name>.Api.csproj` control it:

```xml
<OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../artifacts/openapi</OpenApiDocumentsDirectory>
<OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
```

`artifacts/` is gitignored. The reviewed copy is `contracts/openapi.json`, promoted by
`npm run openapi:accept`. Keeping them separate is what makes the gate meaningful: if the
build wrote straight into `contracts/`, a contract change would never be visible as a
failure, only as a file that quietly differed.

If you add a second document (`AddOpenApi("internal")`), the emitted files are suffixed per
document. `openapi.mjs` falls back to whatever single `.json` it finds in the directory, so
a rename does not break it — but for two documents, set `harness.openapi.emitted` in
`package.json` to the one the frontend consumes, and check the other separately.

**If build-time emission ever fails** — usually because the host constructor does something
that needs a real environment, such as connecting to a database in `Program.cs` — the fix is
to make the host constructible without external services, not to abandon build-time
emission. Configuration binding and DI registration must be side-effect free; move anything
that connects into a hosted service or an endpoint. The escape hatch, if you truly need it,
is `dotnet tool install -g Microsoft.dotnet-openapi` plus a `GetDocument.Insider` invocation,
but every version of that path is more fragile than fixing `Program.cs`.

## What makes a document good to generate from

- **`operationId` on every operation.** It comes from `.WithName("ListTodos")` and becomes
  the method name in every generated client. Without it, generators invent names from the
  route and those names change whenever the route does.
- **Tags.** `.WithTags("Todos")` groups operations into one service class per resource.
  A spec with no tags generates one anonymous god-client.
- **Accurate status codes.** Returning `Results<Ok<T>, NotFound>` rather than `IResult` is
  what puts 404 in the document. `IResult` produces an operation with one undocumented 200
  and a client that cannot tell "missing" from "broken".
- **Named request and response records** rather than anonymous objects, so the generated
  types have stable names.

## The breaking-change classification

`openapi.mjs` compares the committed document with the emitted one and sorts differences
into breaking and additive itself, rather than shelling out to `oasdiff` — no download, no
version pin, no network in the gate.

Counted as **breaking**: a removed path, operation, schema, property or 2xx response; a
parameter removed or newly required; a property that changed type or became required; a
request body that became required.

Counted as **additive**: new paths, operations, schemas, optional parameters, non-2xx
responses, and optional properties.

It is deliberately blunt about direction — a property removed from a response and one
removed from a request are both flagged. Over-reporting costs one sentence in a commit
message; under-reporting costs a consumer a runtime failure.

## Generating the client on the frontend side

The contract is a plain OpenAPI 3 document, so any generator works. What matters is that
the generated directory is **guarded on the frontend too** — hand-edits there are erased by
the next generation and hide a real drift until then.

| stack | generator |
|---|---|
| Angular | `ng-openapi-gen` (services per tag, `HttpClient`) |
| React / TypeScript | `openapi-typescript` + `openapi-fetch`, or `orval` |
| .NET consumer | `NSwag` (`nswag openapi2csclient`) |

Point the generator at `contracts/openapi.json` in the API repo, or at a copy committed to
the frontend repo and refreshed by a small script. Generating against a **running server**
looks convenient and is the thing to avoid: it makes client generation depend on someone
having the API up, and it silently generates from unreviewed local changes.

If the frontend is a separate repository, the honest options are a committed copy refreshed
by a scheduled or manual job, or publishing the document as a package artifact from the API
build. Either way the generated client is checked in, so a contract change shows up as a
compile error in a pull request rather than at runtime.
