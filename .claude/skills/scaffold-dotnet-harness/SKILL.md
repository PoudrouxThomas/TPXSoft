---
name: scaffold-dotnet-harness
description: Scaffold a .NET REST API and the agent harness around it — a fast quiet `npm run verify` (build, dotnet format, unit + NetArchTest architecture tests, OpenAPI contract check), TreatWarningsAsErrors with central package management, Claude hooks that block hand-edits to generated files and gate Stop on a green verify, a build-time OpenAPI document a frontend generates its client from, Testcontainers integration tests, CI, and a sub-2KB CLAUDE.md. Use this whenever someone wants a .NET or ASP.NET Core API created, made agent-ready, hardened, or "properly set up" — and also when they ask for any single piece of it: a verify script for a .NET repo, architecture tests, dotnet format enforcement, an OpenAPI contract for frontend code generation, PostToolUse/Stop hooks for a C# project, Directory.Build.props/Directory.Packages.props conventions, or a CLAUDE.md for a .NET backend. Works on an empty directory and on an existing API.
---

# Scaffold a .NET API agent harness

**The verification loop is the constraint. Build it first, keep it fast, keep it quiet.**

An agent that can check its own work is worth several times one that cannot. Everything
else here — hooks, architecture rules, CI, the contract gate — is scaffolding around that
loop. If the loop is slow or noisy, agents stop verifying and start guessing, and the
harness is worse than nothing because it still looks like it works.

Two numbers to hold yourself to, and they are checkable:

- **under 60 seconds** for `npm run verify`
- **one line of output** when it is green

The second one is where .NET specifically hurts. MSBuild and VSTest are extremely verbose
by default, they repeat every diagnostic in a summary, and every line lands in the model's
context on every run. Left alone, one verify costs more than the change that triggered it.

A bundled installer does the mechanical work, so scaffolding costs almost no tokens. Your
job is the judgement around it: what this repo is, whether there is already code here, and
getting the tree green afterwards without weakening anything.

## Step 1 — establish what you cannot guess

Detect first (`ls *.sln *.csproj`, `ls src`), then ask only what is genuinely unknowable.
One AskUserQuestion, at most these:

1. **The project name** — becomes `<Name>.Domain` / `.Infrastructure` / `.Api` and the
   root namespace. Default to the directory name in PascalCase; confirm rather than
   invent, because renaming afterwards touches every file.
2. **The HTTP port**, if 5080 is taken.

You do not need to ask whether to scaffold: the installer scaffolds when the directory has
no projects of its own and installs harness-only when it finds existing ones.

Requires **.NET 9 or newer** — the OpenAPI document comes from the built-in `AddOpenApi()`,
which does not exist before 9 — plus Node (the harness scripts) and Docker (integration
tests only).

## Step 2 — run the installer

```bash
node <skill-dir>/assets/install.mjs --root . --name Orders --port 5080
```

Flags: `--tf net10.0` to pick a framework other than the newest installed SDK, `--db` for
the Postgres database name, `--harness-only` to skip scaffolding entirely, `--force` to
overwrite an existing `CLAUDE.md` / `.claude/settings.json`, `--offline` to skip the NuGet
version lookup. It is idempotent — safe to re-run after a partial install.

| | |
|---|---|
| `tools/harness/verify.mjs` | the loop: build → format → unit + architecture tests → contract |
| `tools/harness/openapi.mjs` | contract gate: emitted vs committed, with breaking-change classification |
| `tools/harness/diagnostics.mjs` | strips MSBuild's repetition, absolute paths and doc URLs |
| `tools/harness/hooks/` | PreToolUse block, PostToolUse per-project build, Stop gate |
| `tools/harness/hooks-selftest.mjs` | 18 cases proving the block actually blocks |
| `Directory.Build.props` | `TreatWarningsAsErrors`, `Nullable`, analyzers, lock files |
| `Directory.Packages.props` | central package management — an agent cannot invent a version |
| `.editorconfig`, `global.json` | one style truth; one pinned SDK for CI and developers |
| `src/`, `tests/` | Domain / Infrastructure / Api, unit + architecture tests, Testcontainers integration tests |
| `verify.slnf` | the solution filter the loop compiles |
| `.claude/settings.json` | hooks wired, plus a permission allow/deny list |
| `.claude/agents/api-investigator.md` | read-only "where is X / what calls Y" agent |
| `CLAUDE.md` | under 2 KB (written to `CLAUDE.harness.md` if one exists — merge by hand) |
| `.github/workflows/verify.yml` | CI runs the same command, plus locked restore and integration tests |

`references/whats-installed.md` explains any individual piece and how to tune it.

## Step 3 — get to green

```bash
dotnet restore
npm run verify build && npm run openapi:accept
npm run format
npm run verify
```

`npm run format` before the first verify is not optional: `using` ordering is alphabetical
and the project name decides where its own namespace sorts, so a fresh scaffold is
formatted differently for `Acme` than for `Zeta`.

On a brand-new scaffold that is green in about ten seconds. On an existing codebase it
will not be, and that is the point — `TreatWarningsAsErrors` and `Nullable=enable` are
surfacing real defects that were previously invisible. Fix the code.

Resist making the loop pass by lowering the bar. If one analyzer rule is genuinely wrong
for this repo, add that specific ID to `NoWarn` in `Directory.Build.props` with a comment
saying why. Turning off `TreatWarningsAsErrors` or `Nullable` is different in kind: those
two are the highest-value pair in the whole harness, because they stop "I'll clean that up
later", which agents say and never do. If the volume is large, say so and offer to work
through it in batches — `npm run verify build` alone iterates fastest.

## Step 4 — prove the guards, do not assume them

A broken gate reports success and enforces nothing, which is worse than no gate because
everyone believes it. Three checks, all of which must actually be run:

```bash
npm run hooks:selftest
```

Eighteen cases, including `sed -i`, a heredoc redirect, a python one-liner, `cp` and
`rm -rf` against the committed contract — the ways a Bash-blind guard gets walked through
— plus the false positives (`ls node_modules/.bin`, `rm -rf obj`) that get a guard
switched off. All eighteen must pass.

Then prove the architecture rule fires, because a rule that silently passes on everything
is the most expensive kind of green:

```bash
# temporarily leak EF Core into the API layer
printf '\npublic static class Leak { public static string N => typeof(Microsoft.EntityFrameworkCore.DbContext).Name; }\n' >> src/<Name>.Api/Endpoints/TodoEndpoints.cs
npm run verify build && npm run verify test    # must FAIL Api_does_not_touch_EntityFramework_directly
git checkout -- src/<Name>.Api/Endpoints/TodoEndpoints.cs
```

Then prove Stop blocks. Break something small, run
`echo '{}' | node tools/harness/hooks/stop-verify.mjs; echo $?` and confirm it prints
**2**. Exit 1 is a warning the agent never sees, so a red verify would silently become
"task complete".

Last, check the file that loads on every future session is still small:

```bash
node -e "console.log(require('fs').statSync('CLAUDE.md').size, 'bytes')"
```

The template is about 1.9 KB. Tailor it to the conventions this repo actually follows —
and keep it under 2048 bytes by cutting something else, because it is a tax on every
session forever and a stale line there is worse than a missing one.

## Step 5 — the contract, and the frontend that consumes it

`dotnet build` emits the OpenAPI document to `artifacts/openapi/openapi.json`; the
committed copy at `contracts/openapi.json` is what a frontend generates against. `verify`
fails when they diverge, and `npm run openapi:accept` promotes the emitted document while
printing what the change does to consumers:

```
updated contracts/openapi.json  (1 breaking, 2 additive)
  BREAKING  CreateTodoRequest.priority added (required)
```

That is the whole reason to go contract-first: a backend change becomes a compile error in
the frontend rather than a runtime surprise, and an agent can see and fix it alone.

Check the emitted document before moving on — every operation needs an `operationId`,
which comes from `.WithName(...)` on the route, because that string becomes the method
name in every generated client. `references/openapi-contract.md` covers the generators on
the frontend side, multiple documents, and the fallback if build-time emission fails.

## Step 6 — integration tests, deliberately outside the loop

```bash
npm run verify:it     # needs Docker
```

xUnit and Testcontainers against a real Postgres, traited `Category=Integration` and
excluded from the inner loop by trait *and* by not being in the test project list — so
`npm run verify` never starts a container. Run them here, and check afterwards that no
container was orphaned (`docker ps -a`); orphans are what quietly turn a fast suite slow
over a week.

## Step 7 — hand it over

Tell the user, concretely: the verify time you actually measured, what verify covers, what
CI adds, which paths are now off-limits and why, and the one thing to do next — run three
real tasks and note the tokens and wall time. Without that number nobody can tell whether
any of this paid for itself, which is why the cost argument for most harnesses stays faith
rather than fact.

Two habits are worth more than any tool you could add, so say them out loud: **one task
per session with `/clear` between**, and **use the `api-investigator` subagent** for
"where is X / what calls Y". The subagent burns its own context on the search and returns
twenty lines to yours; on a limited quota that is the difference between one task per
session and four.

## Deliberately not built

Say these out loud too, so nobody adds them by reflex. Each earns its place on a large
multi-service codebase and each is wrong for one API and one team.

| Skipped | Why |
|---|---|
| .NET Aspire | Pays when you orchestrate several services. For one API and a database, `docker compose` is less to learn and less to break. |
| Git worktrees, port allocation | Only pays for parallel agents, which a token quota rules out. |
| An MCP server for this API | MCP earns its keep by replacing *reading source code* across services. One service has no cross-module contract questions, and every tool definition costs tokens in every session, used or not. |
| A GitLab / Jira MCP | 30-60 tool definitions in every session, permanently, for something touched a few times a day. `glab` or `gh` behind a two-line skill costs zero until called. |
| A compiled `tpx`-style CLI | Tempting in .NET. It grows into hundreds of untested lines that gate every other line in the repo, and now the thing deciding "green" is itself unverified. Keep it a script. |
| A specialist agent per layer | One repo, one language. Two read-only agents is the ceiling. |
| A project per feature | Every extra project is another build edge inside the 60-second budget. |

## References

- `references/whats-installed.md` — what each generated file does and how to tune it
- `references/openapi-contract.md` — build-time emission, frontend generators, the fallback
- `references/troubleshooting.md` — file locks, locale, slow verify, existing repos, migrations
