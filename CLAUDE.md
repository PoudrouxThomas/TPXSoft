# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

The repository is empty apart from [PLAN.md](PLAN.md) and this file. `git init` has been run; there are no commits yet, and no `.gitignore`, solution, `tpx` CLI, or modules. Every command listed below is a **target**, not something that exists yet — do not run them expecting them to work, and do not assume a path in the layout section exists until you have checked.

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

## Known gaps

`gh` and `pnpm`/corepack are not installed. `gh` is required for the PR-based agent workflow (PLAN.md verification item 8). Both must be installed before Phase 1.
