# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

`git init` has been run. What exists so far: [PLAN.md](PLAN.md) and this file, `.gitignore` and `.editorconfig`, `Directory.Build.props` (`TreatWarningsAsErrors=true`, `Nullable=enable`), `docker-compose.yml` (Postgres 16, reading `COMPOSE_PROJECT_NAME`/port from env), and `tools/tpx` — a working .NET 9 console CLI (see [tools/tpx/README.md](tools/tpx/README.md)) implementing `verify <module>`, `verify --affected`, `verify boundaries`, `test <module> --integration`, `contract lint`, `gen`, and `worktree new <module>/<feature>` (with real Postgres-port/`COMPOSE_PROJECT_NAME` allocation, state kept in the shared git dir so it works across worktrees). `gh` and `pnpm` are installed. `.claude/agents/` has `module-architect`, `dotnet-implementer`, `angular-implementer`, `contract-guardian`, `test-writer` (PLAN §0.3). `.claude/skills/` has `new-module`, `new-endpoint`, `wire-module`, `mcp-expose` (PLAN §0.4). `.claude/settings.json` wires three hooks (PLAN §0.5): PreToolUse on Edit/Write blocks `**/clients/**` and `**/generated/**` (`.claude/hooks/block-generated.ps1`); PostToolUse on Edit/Write runs a narrow build/lint on the touched project (`verify-on-save.ps1`); Stop runs `tpx verify --affected` (`stop-verify.ps1`). **All three invoke `pwsh`, so none of them fires on Linux — a cloud session has no hook backstop, and no `tpx` on `PATH` either (nothing builds it, and the .NET SDK is not pre-installed). PLAN §0.5 records the deferred setup-script + SessionStart-hook fix; check it before relying on either.** A Friday 18:00 `/schedule` cloud routine is live (code review + contract-lint drift + `GOALS.md` progress report, PLAN §0.8) — that routine lives in Claude Code's own scheduler, not as a file in this repo, so there is nothing under `.claude/` to commit for it.

There is still no root solution, no `contracts/`, no `modules/`, no MCP servers. Every `tpx` command above therefore runs today but reports "nothing found" rather than doing real work — there is nothing yet for it to build, test, or lint. The skills above are procedures written against a layout that doesn't exist yet — nothing to scaffold from, no MCP template until Auth exists (PLAN §0.7, deliberately deferred). `GOALS.md` now exists — written by the Friday routine's first run, and the place to look for per-milestone status and what blocks each incomplete item. Next step: build the Auth module (Phase 1) — the harness's first real guinea pig.

[PLAN.md](PLAN.md) is the shared reference for the project and takes precedence over this file where they disagree. Update PLAN.md whenever a decision changes.

## Working agreement (read this before doing anything)

Nothing is scaffolded ahead of time. Each piece of the harness is built when it is explicitly asked for, so that it is understood rather than inherited.

**Do not** create the repository, write the `tpx` CLI, add agent or skill definitions, configure hooks, add MCP servers, or generate module trees until that specific step is requested. Answering "what would step 0.3 look like?" is not a request to build step 0.3.

This is a learning project. The product (TPXSoft, a modular Microsoft-suite clone) exists to be large enough that MCP, worktrees, skills, subagents, loops, and schedules are genuinely necessary. The goal is the harness, not the features.

## Stack

.NET 9 (ASP.NET Core, EF Core) · Angular multi-project CLI workspace · PostgreSQL 16 · docker-compose now, .NET Aspire from Phase 2 · plain `.sln` + Angular CLI + npm scripts behind a `tpx` CLI.

Postgres is chosen for built-in full-text search, JSONB, `LISTEN/NOTIFY`, and pgvector — all of which later modules need. Nx is deliberately deferred (see PLAN.md "Build tooling rationale"); it may be adopted for the Angular side only, if the frontend grows to many libraries.

## The `tpx` CLI is the stable interface

Agents, hooks, skills, and CI all call `tpx`. Whether it shells out to `dotnet test` or something else is an implementation detail that must stay invisible to callers. Never work around `tpx` by invoking the underlying tool directly in a hook, skill, or agent definition — that is what makes the build tooling cheap to swap.

```bash
tpx verify <module>            # build + unit tests + contract lint — hard target: under 60s
tpx verify --affected          # maps git diff paths to affected modules
tpx verify boundaries          # fails if a module references another module's .Domain/.Infrastructure
tpx test <module> --integration  # Testcontainers against real Postgres
tpx contract lint              # OpenAPI valid + no breaking change vs main (oasdiff)
tpx gen                        # regenerate shared/clients from contracts/ (NSwag + ng-openapi-gen)
tpx worktree new <module>/<feature>  # worktree + allocated port offset + unique COMPOSE_PROJECT_NAME
```

If `tpx verify <module>` ever exceeds 60 seconds, stop and fix the loop before writing feature code. The verification loop's speed is the constraint everything else is built around: agents that can verify their own work are worth several times agents that cannot.

## Architecture

### Monorepo, one deliberate extraction

Single repo. Realism comes from CODEOWNERS, per-package semver, and independent deploys — not from repo walls. Auth is extracted to its own repo at Phase 3 so the multi-repo tradeoff is learned once, deliberately.

### Contract-first

`contracts/<module>.vN.yaml` (OpenAPI) is the single source of truth. Clients in `shared/clients/` are **generated** — never hand-edit anything under `shared/clients/` or any `generated/` path. To change a client, edit the contract and run `tpx gen`. A PreToolUse hook is intended to block such edits mechanically.

### Module boundaries

A module may only talk to another module through `shared/clients` (generated) or `shared/TPXSoft.Shared.Kernel` (result types, errors, paging — no domain logic). Direct references to another module's `.Domain` or `.Infrastructure` fail `tpx verify boundaries`.

### MCP is a dev-time contract layer, not the runtime bus

Runtime module-to-module calls are plain HTTP over generated clients. Separately, each module ships an MCP server (C# `ModelContextProtocol` SDK, stdio, registered in root `.mcp.json`) exposing `get_openapi()`, `list_endpoints()`, `describe_entity(name)`, `find_consumers(entity_or_field)`, `run_tests(filter?)`, `get_migrations_status()`. An agent working on Outlook queries the Documents contract instead of reading its source — that is where the token savings come from. Auth's MCP server becomes the template for the `new-module` skill.

### Per-module solutions

Each module has its own `TPXSoft.<Module>.sln` so agent builds stay fast; `TPXSoft.sln` at the root contains everything.

## Target layout

```
.claude/          agents, skills, commands, settings.json (project hooks)
.mcp.json         registers every module MCP server
contracts/        OpenAPI YAML — single source of truth
modules/<name>/   CLAUDE.md, src/{Domain,Api,Infrastructure,Mcp}, tests/{Unit,Integration}, .sln
apps/             sharepoint/api, sharepoint/web (Angular)
shared/           Shared.Kernel, clients/ (GENERATED), ui/ (Angular lib)
tools/tpx/        the tpx CLI
GOALS.md          milestones with machine-checkable acceptance criteria
```

Each `modules/<name>/CLAUDE.md` carries that module's bounded context, entities, `tpx verify` line, and known consumers — loaded only when working there. Keep this root file short; it loads every session.

## Parallel worktrees

Port collision between concurrent integration-test runs is the failure that kills parallel worktree agents. `docker-compose.yml` reads `COMPOSE_PROJECT_NAME` and the Postgres port from env; `tpx worktree new` writes both into the new worktree's `.env`. `.claude/` lives at the repo root and is shared by all worktrees automatically.

## Loops vs schedules

`/loop` is session-scoped self-pacing — "implement remaining endpoints in `contracts/auth.v1.yaml` until `tpx verify auth` is green." A recurring weekly review is `/schedule` (cron), not `/loop`.

## Agent conventions

Prefer already-installed agents over new ones: `caveman:cavecrew-investigator` to locate code, `caveman:cavecrew-reviewer` for diff review, plus built-in `/code-review` and `/security-review`. Project-specific agents (`module-architect`, `dotnet-implementer`, `angular-implementer`, `contract-guardian`, `test-writer`) are defined in PLAN.md §0.3 and built when requested.

An implementer agent must run `tpx verify <module>` before reporting done.

Skills add input tokens on every load; they only pay off when they replace repeated exploration. Keep project skills as short procedures, not essays.

