# What the installer writes, and how to tune it

Read the section you need; nothing here is required to run the skill.

- [The loop](#the-loop)
- [Determinism](#determinism)
- [Architecture rules](#architecture-rules)
- [Hooks](#hooks)
- [The API skeleton](#the-api-skeleton)
- [Tests](#tests)
- [CI](#ci)
- [Token hygiene](#token-hygiene)

## The loop

**`tools/harness/verify.mjs`** runs four steps and stops at the first failure:

| step | command | why in this order |
|---|---|---|
| `build` | `dotnet build verify.slnf --nologo -v quiet` | restores once, so everything after it passes `--no-restore` |
| `format` | `dotnet format verify.slnf --verify-no-changes --no-restore` | needs a loaded MSBuild workspace anyway, and a compile error matters more than whitespace |
| `test` | `dotnet test <each unit project> --no-build --no-restore --filter "Category!=Integration"` | rebuilding inside `test` is the single most common reason a .NET loop takes three minutes instead of thirty seconds |
| `contract` | `node tools/harness/openapi.mjs check` | the emitted document is already on disk from the build |

`npm run verify <step>` runs one step while you iterate. `npm run verify:it` runs the
integration step, which is not in the default list.

Configuration lives in `package.json > harness`:

```jsonc
{
  "solution": "Orders.sln",
  "verifySolution": "verify.slnf",     // what build and format compile
  "configuration": "Debug",
  "protectedPaths": ["contracts/openapi.json", "artifacts"],
  "openapi": { "emitted": "...", "committed": "..." },
  "testProjects": ["tests/Orders.UnitTests/Orders.UnitTests.csproj"],
  "integrationTestProjects": ["tests/Orders.IntegrationTests/..."]
}
```

`testProjects` is a list of project files, not a solution, and that is deliberate:
**`dotnet test` accepts a `.slnf` and then runs nothing at all, exit code zero.** A gate
that reports success while enforcing nothing is the worst outcome available, so the
projects are named. Naming them also stops `dotnet test` interleaving output from several
projects run in parallel, which is both longer and harder to read back.

**`tools/harness/diagnostics.mjs`** is why a broken build is a few lines instead of a few
thousand tokens: MSBuild prints each diagnostic once inline and again in a summary, with
an absolute path in front and the project file plus a documentation URL behind. It strips
all of that, deduplicates, and shows the first eight.

**`verify.slnf`** starts out listing every project, because a project the loop does not
compile is a project an agent can leave broken and still be told it is done. Its reason to
exist is later: when you add a worker, a second host, or anything that holds a lock on
build output, take it out of this list. A running host makes `dotnet build` fail with a
file-lock error that reads like a compile failure and sends an agent hunting a bug that
does not exist.

## Determinism

Anything a tool can decide should never consume model attention.

- **`Directory.Build.props`** — `TreatWarningsAsErrors`, `Nullable=enable`,
  `EnforceCodeStyleInBuild`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`,
  `RestorePackagesWithLockFile`. The first two are the highest-value pair in the harness.
  `CA1848` (logging performance) and `CA2007` (ConfigureAwait, meaningless in ASP.NET)
  are pre-suppressed; add further IDs here with a comment, never by lowering the level.
- **`Directory.Packages.props`** — central package management, so an agent cannot invent a
  version in one project and every project stays on one set of pins. Versions for the
  framework packages are resolved from NuGet at install time within the target framework's
  major band; `--offline` uses the built-in fallbacks.
- **`.editorconfig`** — the single style truth. `IDE0055` is raised to `warning`, which is
  what makes `dotnet format --verify-no-changes` fail rather than silently reformat on
  someone else's machine later.
- **`global.json`** — pins the SDK so CI and every developer use one compiler.
- **`packages.lock.json`** per project, with `dotnet restore --locked-mode` in CI.

Test projects suppress `CA1707`, `CA1711` and `CA1861` in their own `.csproj`. Test names
are the behavioural specification — `Login_WithExpiredToken_Returns401` fails the moment
it stops being true — and `CA1707` would cost the only executable spec in the repo.

## Architecture rules

`tests/<Name>.UnitTests/ArchitectureTests.cs`, seven NetArchTest rules: the domain knows
no frameworks and no outer layers, the API never names EF Core, infrastructure never names
the API, repositories live only in infrastructure, domain classes are sealed, namespaces
match assemblies.

They are ordinary xUnit tests **inside** verify, not a separate `verify architecture`
subcommand, and that is the point: a rule that runs only in CI is outside the agent's
definition of done, which means it does not exist as far as the agent is concerned. This
is also the rule an agent is most likely to break and the one least visible in a diff — a
single `using Microsoft.EntityFrameworkCore` in a handler looks like nothing and undoes
the whole layering.

Add rules here as the codebase grows. Five to ten is the useful range; beyond that they
start failing for reasons nobody remembers agreeing to.

## Hooks

Wired in `.claude/settings.json`:

- **PreToolUse** on `Edit|Write|MultiEdit|NotebookEdit|Bash` → `block-generated.mjs`.
  Matching only the editing tools leaves the guard trivially bypassable, so Bash is
  deny-by-default on any command mentioning a protected path unless every segment is a
  known read-only command or a generator (`dotnet`, which legitimately writes the emitted
  contract and EF migration metadata).
- **PostToolUse** on edits → `verify-on-save.mjs` builds *only* the `.csproj` that owns the
  edited file, walking up from it. Narrow on purpose: the full loop runs on Stop, and this
  exists so a compile error comes back seconds after it is written. Building the solution
  here would tax every edit until the agent started batching edits to avoid it.
- **Stop** → `stop-verify.mjs`, exit **2** on failure. Exit 1 is a non-blocking warning the
  agent never sees, so a red verify would silently become "task complete".
  `stop_hook_active` prevents a loop on a still-red tree.

Protected paths are `contracts/openapi.json`, `artifacts/`, anything under a `generated`
directory, `bin`/`obj`, and EF's `*.Designer.cs` / `*ModelSnapshot.cs`. Add more in
`package.json > harness.protectedPaths`.

Note `bin`/`obj` are matched against file paths only, never against command text — a guard
that fires on `/bin/sh`, `node_modules/.bin/...` or `rm -rf obj` is a guard people turn off.

## The API skeleton

Minimal APIs, grouped per resource, one static handler per operation. Handlers return
`Results<Ok<T>, NotFound>`-style unions rather than `IResult`, because the union is what
tells the OpenAPI generator which status codes exist — the document stays accurate without
anyone maintaining a parallel list of `.Produces()` calls.

`Domain` holds entities and the interfaces they need and references nothing. `Infrastructure`
is the only project that names EF Core, behind a single `AddInfrastructure(configuration)`
seam. `Api` wires them together and never sees a `DbContext`.

The `Todo` entity and its endpoints are sample scaffolding — delete them once real
endpoints exist. They are there so the harness has something to verify from minute one,
including a failing case you can watch the loop catch.

## Tests

- `tests/<Name>.UnitTests` — domain tests plus the architecture rules. In the loop.
- `tests/<Name>.IntegrationTests` — xUnit + Testcontainers + `WebApplicationFactory`
  against a real Postgres, traited `[Trait("Category", "Integration")]`. Out of the loop,
  run by `npm run verify:it` and by CI.

`ApiFactory` starts one container per collection and disposes it; keep that dispose, since
orphaned containers turn a fast suite into a slow one over a week without anyone noticing.

## CI

`.github/workflows/verify.yml` runs `npm run verify` — literally the same command — plus
`dotnet restore --locked-mode`, the hook self-test, and the integration tests. Nothing in
CI checks something local verify does not; if it did, the model could not reproduce the
failure locally and would burn tokens guessing at it.

## Token hygiene

`.gitignore` and the permission `deny` list keep `bin/`, `obj/`, `artifacts/`,
`TestResults/` and `node_modules/` out of reach. `obj/` matters most: it is full of
generated `.cs` files that look like source and are not.

The permission `allow` list covers the read-only and verify commands an agent runs
constantly. Every prompt is a round trip, so this is free latency and free tokens.

Quiet output first, always, before installing anything that claims to save tokens — on
.NET restore banners, per-project DLL paths, VSTest version headers and "no tests matched
the filter" notices, repeated per project, are by far the larger leak.
