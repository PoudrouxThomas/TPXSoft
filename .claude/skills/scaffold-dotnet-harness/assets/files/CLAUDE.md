# __NAME__

.NET REST API. Minimal APIs, EF Core, PostgreSQL.

## The loop

`npm run verify` — build, format check, unit + architecture tests, contract check.
Under 60s, one line when green. Nothing is done until it is green; the Stop hook enforces
that. One check at a time while iterating: `npm run verify build|format|test|contract`.
Integration tests (Testcontainers) are `npm run verify:it`, never the inner loop.

## Layout, and the rule

- `src/__NAME__.Domain` — entities and the interfaces they need. No EF Core, no ASP.NET.
- `src/__NAME__.Infrastructure` — `AppDbContext`, repositories, migrations. The only
  project that may name EF Core.
- `src/__NAME__.Api` — endpoints in `Endpoints/`, wire records in `Contracts/`.

Enforced by `tests/__NAME__.UnitTests/ArchitectureTests.cs`. If a rule fails, fix the
code; changing the rule needs a reason in the commit message.

## Conventions

- Minimal APIs grouped per resource, one static handler per operation, `WithName` on
  every route — that name becomes the method name in every generated client.
- Handlers return `Results<Ok<T>, NotFound>`-style unions, which is what keeps the
  OpenAPI status codes accurate without hand-written `.Produces()` calls.
- Failures are ProblemDetails. No exception leaves as a stack trace.
- Migrations are scaffolded, never hand-written:
  `dotnet ef migrations add <Name> --project src/__NAME__.Infrastructure --startup-project src/__NAME__.Api`
- Warnings are errors and nullable is on. Fix the warning; do not suppress it.

## Never hand-edit

`contracts/openapi.json`, `**/Migrations/*.Designer.cs`, `*ModelSnapshot.cs`, `artifacts/`,
`bin/`, `obj/`. To change the contract, change the endpoint or DTO and run
`npm run openapi:accept`. The frontend generates its client from that file, so a breaking
change there breaks them at compile time — say so in the commit message.
