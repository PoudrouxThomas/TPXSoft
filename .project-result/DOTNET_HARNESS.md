# .NET API — agentic harness checklist

A build order for a strong agent harness on a **single .NET backend**, in a company setting:
fully local, no cloud runners, no scheduled routines, limited token quota.

Companion to [JAVA_HARNESS.md](JAVA_HARNESS.md) and [ANGULAR_HARNESS.md](ANGULAR_HARNESS.md).
The spine is identical across all three; this file argues only the points where .NET differs,
and .NET differs more than you would expect — mostly in how noisy and how locale-dependent its
tooling is by default.

---

## The one rule

**The verification loop is the constraint. Build it first, keep it fast, keep it quiet.**

An agent that can check its own work is worth several times an agent that cannot. Everything
below — hooks, subagents, skills, CI — is scaffolding around that loop. If the loop is slow,
agents stop verifying and start guessing, and the harness is worse than nothing because it
looks like it works.

Two hard numbers to hold yourself to:

- **Under 60 seconds** for `dev verify`. If it exceeds that, fix the loop before writing any
  feature code.
- **Under 15 lines of output** on success. This is where .NET will hurt you: MSBuild and VSTest
  are extremely verbose by default, and every line lands in the model's context on every run.
  Left alone, a single verify can cost more than the change that triggered it.

---

## Build order

Do these in order. The ordering is the advice — most of these items are individually obvious,
and most teams still build them in the wrong sequence.

### 1. `dev verify` — fast, quiet, one command

- [ ] One entry point. A shell script, or MSBuild/`dotnet` targets. **Not a compiled CLI tool.**
- [ ] `dev verify` runs: format check → build → unit tests → architecture tests.
- [ ] Every consumer (hooks, CI, agents, humans) calls **this command only**, never `dotnet build`
      or `dotnet test` directly. That is what keeps the build tooling swappable.
- [ ] **Cross-platform from day one** if your developers are on Windows and CI is on Linux.
      A PowerShell-only script silently stops working the moment anything runs in a Linux
      container — including cloud or sandboxed agent sessions. Pick one form that runs on both.
- [ ] Build once, test without rebuilding: `dotnet build --nologo -v quiet` then
      `dotnet test --no-build --no-restore`. Rebuilding inside `test` is the single most common
      reason a .NET loop takes three minutes instead of thirty seconds.
- [ ] Quiet it aggressively: `--nologo`, `-v quiet`, and
      `--logger "console;verbosity=minimal"` on test. Consider Microsoft.Testing.Platform, which
      is dramatically less chatty than VSTest.
- [ ] On success print one summary line plus timing. On failure print the **first** failing test
      and stop.
- [ ] Measure it. Write the number down.

**Force the tool language to English.** `dotnet` emits diagnostics in the machine's locale, so a
developer on a French or German Windows install gets compiler errors the agent has to translate,
and any grep or parse you write against error text breaks per-machine. Set
`DOTNET_CLI_UI_LANGUAGE=en` in the script. Small, non-obvious, and it costs you an afternoon the
first time it bites.

**Watch out for interleaved test output.** `dotnet test` on a solution runs test projects in
parallel and interleaves their output, which produces context that is both long and confusing to
read back. Scope the run, or serialize it.

**File locks are a .NET-specific trap.** Any long-running process holding your build output — a
running host, a background worker, an MCP server, `dotnet watch` — makes `dotnet build` fail with
a file-lock error that reads like a compile failure and sends the agent hunting for a bug that
does not exist. Scope `verify` with a **solution filter** (`.slnf`) that excludes long-running
hosts, and never put `dotnet watch` in the loop.

### 2. Determinism — remove taste from the loop

Anything a tool can decide should never consume model attention or tokens. .NET is unusually good
here: most of this is configuration, not tooling you have to install.

- [ ] **`.editorconfig`** as the single source of style truth, committed.
- [ ] **`dotnet format`** — `dev format` applies it, `dev verify` runs
      `dotnet format --verify-no-changes`.
- [ ] **`Directory.Build.props`** at the repo root, applying to every project:
      ```xml
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      <Nullable>enable</Nullable>
      <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
      <EnableNETAnalyzers>true</EnableNETAnalyzers>
      <AnalysisLevel>latest-recommended</AnalysisLevel>
      ```
- [ ] **`Directory.Packages.props`** — central package management. Underrated for agent work: it
      stops an agent inventing a package version, and keeps every project on one set of pins.
- [ ] **`packages.lock.json`** + `dotnet restore --locked-mode` in CI, for reproducible restores.

`TreatWarningsAsErrors` plus `Nullable=enable` is the highest-value pair in this list. It stops
"I'll clean that up later," which agents say and never do, and nullable warnings catch a whole
class of bug before a test exists.

### 3. Architecture rules as tests — NetArchTest

- [ ] Add **NetArchTest.Rules** (or ArchUnitNET) and write 5-10 rules: layer direction, "nothing
      outside `Infrastructure` references the EF Core types", "no controller depends on a
      `DbContext`", namespace containment.
- [ ] Rules live in the unit test project as ordinary xUnit tests, so `dev verify` picks them up
      with no extra tooling and no custom checker to maintain.

This is the highest value-per-line item in the whole .NET harness. It is the rule an agent is
most likely to break, it is nearly invisible in a diff review, and it costs nothing to check.

**Keep it inside `verify`, not beside it.** A rule that runs only in CI is outside the agent's
definition of done, which means it does not exist as far as the agent is concerned. Writing a
separate `verify architecture` subcommand feels tidier and is a mistake for exactly this reason.

### 4. `CLAUDE.md` — after the loop exists, under 2 KB

- [ ] Write it now, not first. Before the loop exists you would be describing intent, not fact.
- [ ] Contents: the one verify command, the architecture rule in a sentence, paths never to be
      hand-edited, and the definition of done.
- [ ] Add the few .NET conventions an agent cannot infer and will otherwise pick at random:
      minimal APIs vs controllers, the project layout, the result/error-handling pattern, whether
      EF migrations are hand-written or scaffolded.
- [ ] Nothing else. This file loads on **every** session, so a stale fact here is a recurring tax
      paid in wrong assumptions.

### 5. Hooks — the backstop that fires whether the agent cooperates or not

- [ ] **PostToolUse** on `.cs` edits → build just the touched `.csproj` (walk up from the edited
      file to find it). Fast feedback at the cheapest possible moment; a compile error surfaces
      seconds after it is written rather than at the end of the task.
- [ ] **Stop** → `dev verify`. **Must exit 2 on failure.** Exit 1 is a non-blocking warning the
      agent never sees.
- [ ] **PreToolUse** blocking writes to generated code — generated API clients, `**/generated/**`,
      scaffolded EF model output. **Match `Bash` as well as `Edit`/`Write`**: a `sed -i` or a
      heredoc redirect is the same write, and matching only the editing tools leaves the guard
      trivially bypassable.
- [ ] Whatever detects "what changed" must work **on the main branch**, with an uncommitted
      working tree, and on untracked files.
- [ ] Write hook bodies once, cross-platform. Two implementations — one for Windows, one for
      Linux — drift, and the one that drifts is the one nobody runs locally.

Every one of these is easy to ship and hard to notice, because a broken gate reports success and
enforces nothing. Test them by running them against a deliberately failing change — a hook that
has only been read has not been verified.

### 6. CI — the same command, not a second implementation

- [ ] CI runs `dev verify`. Literally that command.
- [ ] Nothing in CI checks something local verify does not. If it does, the model cannot
      reproduce the failure locally and burns tokens guessing at it.
- [ ] `dotnet restore --locked-mode`, and pin the SDK with `global.json` so CI and developers
      compile against the same compiler.
- [ ] Pin every external tool CI installs to a release tag. Never `curl | sh` from a moving branch.

### 7. Token hygiene — free, and probably your biggest saving

- [ ] Keep `bin/`, `obj/`, `TestResults/`, `*.nupkg` and generated code out of the agent's reach.
      `obj/` in particular is full of generated `.cs` files that look like source and are not.
- [ ] **One task per session, `/clear` between tasks.** A long session where the model re-reads
      its own history costs more than any tool you could install.
- [ ] **Permission allowlist** in `.claude/settings.json` for the read-only commands you run
      constantly. Every prompt is a round trip.
- [ ] Quiet the verify output (step 1) before installing anything that claims to save tokens.
      On .NET this is by far the larger leak: restore banners, per-project DLL paths, VSTest
      version headers and "no tests matched the filter" notices, repeated per test project.
- [ ] Optional: a CLI output compressor. Cheap, keep it if you like it.
- [ ] **Do not install a token tool whose savings you have not measured on your own repo.**

### 8. One read-only subagent

- [ ] A "where is X / what calls Y" investigator with `Read`, `Grep`, `Glob` only.

A bigger token lever than any CLI compressor, and the item most often left out. The subagent burns
*its own* context on the search and returns twenty lines to yours. On a limited quota this is the
difference between one task per session and four.

Two agents is plenty for a single repo. Resist a specialist per layer.

### 9. `dev verify-it` — integration, separate and slow

- [ ] xUnit + **Testcontainers** against a real database.
- [ ] Traited (`[Trait("Category", "Integration")]`) and excluded from the inner loop with
      `--filter "Category!=Integration"`, so `dev verify` never starts a container.
- [ ] Runs in CI and on demand. Never in the Stop hook.
- [ ] Check that containers are actually reaped between runs. Orphaned containers turn a fast
      loop into a slow one over a week, quietly.

### 10. Measure, once

- [ ] Log tokens and wall-clock time for three real tasks.

Without this number you cannot tell whether any of the above paid for itself. It is the step most
teams skip, which is why the cost argument for most harnesses stays faith rather than fact.

### 11. Only now: skills

- [ ] Write a skill for a workflow you have already performed **manually three times** — most
      likely "new endpoint: contract, handler, test, verify" once you know its real shape.
- [ ] Keep it a short procedure, not an essay. Skills cost input tokens on every session.

Encoding a workflow before you have run it is encoding a guess. Skills written ahead of the work
they describe tend to sit unused — and still cost tokens on every session.

---

## Specs: make them executable

Prose specs rot silently, and agents believe them. You end up with documents confidently asserting
things that stopped being true months ago — nobody lied, the code moved. One prose spec per
feature is one rotting claim per feature.

Put the specification where the harness can check it:

| Intent | Executable form |
|---|---|
| API surface | OpenAPI — built-in `AddOpenApi` (.NET 9+), Swashbuckle, or contract-first with NSwag |
| Behavior | Test names — `Login_WithExpiredToken_Returns401` fails when it stops being true |
| Architecture | NetArchTest rules |
| Decisions | Short ADRs in `docs/adr/`, append-only, never edited |

Feature prose in Jira or a wiki is fine for humans. Just never let it be the agent's source of truth.

If a frontend consumes this API, go contract-first: the OpenAPI document is the source of truth,
the client is generated from it, and the PreToolUse block from step 5 covers the generated path.
This is the one place where the discipline pays back immediately — a backend change becomes a
compile error in the frontend instead of a runtime surprise, and the agent can see and fix it
alone.

---

## Do not build

Scaling down is most of the value here. Each of these earns its place on a large multi-service
codebase, and each is wrong at single-repo, local, quota-limited scale.

| Skip | Why |
|---|---|
| .NET Aspire | Only pays when you orchestrate several services. For one API and a database, `docker compose` is less to learn and less to break. |
| Git worktrees + port allocation | Only pays for parallel agents. With a token quota you will not run parallel agents. |
| Plugin / marketplace packaging | One team, one repo — copy the files. A packaged copy of your agents and skills is a second source of truth, and it drifts within weeks unless CI checks it. |
| Scheduled routines | No cloud. |
| An MCP server for your own API | One service. There are no cross-module contract questions to answer. |
| A specialized agent per layer | One repo, one language. Two read-only agents is the ceiling. |
| A custom CLI in a compiled language | Tempting in .NET, because writing it in C# feels natural. It quietly grows into hundreds of untested lines that gate every other line in the repo — and now the thing deciding "green" is itself unverified. Keep it a script. |
| A project per feature | `Domain` / `Api` / `Infrastructure` plus test projects is enough. Every extra project is another build edge in your 60-second budget. |

### MCP servers for GitLab / Jira — think twice

An MCP server injects **every one of its tool definitions into every session**, used or not. A
GitLab MCP is typically 30-60 tools: thousands of tokens per session, permanently, for something
you touch a few times a day.

`glab` and the Jira CLI behind a two-line skill cost **zero** until called. Reaching for the `gh`
CLI rather than a GitHub MCP is the same trade, and the right one.

MCP earns its place when it replaces **reading source code** — answering a structural question in
one call instead of ten file reads. Wrapping a CLI you could already call is not that.

---

## Done when

- [ ] `dev verify` is green, under 60s, under 15 lines of output.
- [ ] The same script runs on a developer's Windows machine and on Linux CI.
- [ ] `TreatWarningsAsErrors` and `Nullable=enable` are on, and the repo is clean under both.
- [ ] Architecture rules run inside `verify`, not beside it.
- [ ] A red verify blocks the agent from finishing (Stop hook, exit 2).
- [ ] Generated code cannot be hand-edited, including through Bash.
- [ ] `CLAUDE.md` is under 2 KB and every statement in it is currently true.
- [ ] You have measured the token cost of three real tasks.

Steps 1-6 are about a week of work. That is a strong harness for a single .NET backend, and it is
roughly where the value plateaus for one team on one repo.
