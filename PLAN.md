# TPXSoft — Bootstrap Plan (Phase 0: AI Harness)

## Context

This is a greenfield learning project. `D:\Dev\AI\TPXSoft` is empty. The goal is not the product — it is to learn AI-assisted development at scale: MCP, git worktrees, skills, asynchronous subagents, agent workflows, loops/schedules, and goal tracking. TPXSoft (a modular Microsoft-suite clone: Teams, Outlook, Sharepoint, Word, Onenote, Forms) exists to be big enough that these techniques are actually necessary rather than ceremonial.

The intended outcome of this plan is a working *harness* — repository, contracts, verification loop, agents, skills, hooks, and MCP servers — proven end-to-end on one real module (Auth). Once the harness is fast and trustworthy, every subsequent module and app becomes cheap. No product features are built until the verification loop is fast.

### Decisions taken

| Decision | Choice | Rationale |
|---|---|---|
| Repo layout | Monorepo, with one deliberate extraction later | Microsoft (Windows, Office), Google, Meta all use monorepos; per-service repos are a mid-size-org pattern. Realism comes from CODEOWNERS + per-package semver + independent deploys, not from repo walls. Worktrees, agents, and CI all get dramatically cheaper. Auth is extracted to its own repo at Phase 3 so the multi-repo tradeoff is learned deliberately, once. |
| Backend | .NET 9 (ASP.NET Core, EF Core) | User choice; MCP has an official C# SDK (`ModelContextProtocol`). |
| Frontend | Angular (multi-project CLI workspace) | User choice. |
| Database | PostgreSQL 16 | Built-in full-text search (needed by Sharepoint/Outlook), JSONB (Documents/Forms/Onenote payloads), `LISTEN/NOTIFY`, pgvector for later semantic search. MySQL would force an Elasticsearch dependency by Phase 4. |
| Build tooling | Plain `.sln` + Angular CLI workspace + npm scripts, behind a `tpx` CLI | Low-novelty on purpose: the learning budget goes to the AI workflow, not build config. See "Build tooling rationale" below — this is a cheaply reversible decision. |
| Local orchestration | docker-compose at Phase 0; **.NET Aspire from Phase 2** | At one module Aspire is overhead. At four services + Postgres + Angular it earns its place: service discovery, container lifecycle, telemetry dashboard, Microsoft-native. Its dynamic port assignment also helps the parallel-worktree collision problem. |
| First vertical slice | Auth → Documents → Sharepoint-lite | Lowest incidental complexity that still proves module boundaries, contract-first wiring, and cross-module MCP. |
| Build order | Harness first | Retrofitting hooks/CI onto agent-written code costs more than building them first. |

### Build tooling rationale

The plain toolchain hand-rolls three things a full build tool provides: affected-project detection (`tpx verify --affected` vs `nx affected`), module boundary enforcement (`tpx verify boundaries` vs `@nx/enforce-module-boundaries`), and scaffolding (the `new-module` skill vs Nx generators). It also gives up computation caching.

Nx is nonetheless deferred, for three reasons. The affected-graph and cache pay off at hundreds of projects; at three to six .NET modules a per-module `.sln` build is already inside the 60-second target. `@nx-dotnet` is community-maintained with a small user base, which would place a third-party plugin on the critical path of the agent verification loop — the one component that must never be flaky. And Nx carries its own mental model (targets, executors, inferred tasks, plugin graph), a cost that lands squarely on stack learning rather than AI learning.

Adopt Nx **for the Angular side only**, if and when the frontend grows to many libraries.

This stays cheap to revisit because **`tpx` is the stable interface**. Agents call `tpx verify auth`, `CLAUDE.md` documents it, hooks invoke it. Whether it shells out to `dotnet test` or to `nx affected` is invisible to every agent, skill, and hook. Swap the implementation without touching the harness.

### Architectural corrections baked into this plan

1. **MCP is a dev-time contract layer, not the runtime bus.** Module-to-module calls at runtime are plain HTTP over generated clients. Each module *additionally* ships an MCP server that exposes its contract to Claude (`get_openapi`, `describe_entity`, `find_consumers`, `run_tests`) so an agent working on Outlook can query the Documents contract instead of reading its source. This is where the token savings actually come from.
2. **Skills add input tokens; they only pay off when they replace repeated exploration.** Project-local skills (`new-module`, `new-endpoint`) will beat generic open-source skills here. Real token reduction comes from subagents (isolated context), RTK (already hooked), a per-module `CLAUDE.md` hierarchy, and MCP contract queries.
3. **A weekly Friday review is `/schedule` (cron), not `/loop`.** `/loop` is session-scoped self-pacing; use it for "iterate until all contract tests pass" within a session.

---

## Repository layout

```
D:\Dev\AI\TPXSoft\
  CLAUDE.md                    # repo map + conventions + verify commands (<150 lines)
  .claude\
    agents\                    # subagent definitions
    skills\                    # project-local skills
    commands\                  # slash commands
    settings.json              # hooks (project-scoped)
  .mcp.json                    # registers every module MCP server
  .github\workflows\           # per-module CI, path-filtered
  CODEOWNERS
  angular.json                 # ONE Angular workspace, every Angular project registered here
  package.json                 # ONE node_modules for the whole front end
  tsconfig.base.json           # path aliases: @tpx/ui, @tpx/clients/*
  GOALS.md                     # milestone acceptance criteria (see 0.8)
  contracts\                   # OpenAPI YAML — single source of truth
    auth.v1.yaml
    documents.v1.yaml
  modules\
    auth\
      CLAUDE.md
      src\TPXSoft.Auth.Domain\
      src\TPXSoft.Auth.Api\
      src\TPXSoft.Auth.Infrastructure\
      src\TPXSoft.Auth.Mcp\        # MCP server, ModelContextProtocol SDK, stdio
      tests\TPXSoft.Auth.UnitTests\
      tests\TPXSoft.Auth.IntegrationTests\   # Testcontainers + Postgres
      TPXSoft.Auth.sln             # per-module solution: fast agent builds
    documents\                     # same shape
  apps\
    sharepoint\api\                # ASP.NET Core host
    sharepoint\web\                # Angular application project
    outlook\api\ , outlook\web\    # same shape, later phases
  shared\
    TPXSoft.Shared.Kernel\         # result types, errors, paging — no domain logic
    clients\csharp\                # GENERATED (NSwag) — never hand-edited
    clients\angular\               # GENERATED (ng-openapi-gen) — never hand-edited
    ui\                            # Angular shared component library
  tools\
    tpx\                           # tpx CLI: verify, scaffold, worktree, affected
  TPXSoft.sln                      # root solution, everything
```

### One repository, front end included

There is no separate front-end repository. The Angular side lives in the same tree as the .NET side, as a single Angular CLI workspace: one root `angular.json`, one `package.json`, one `node_modules`, with each application under `apps/<app>/web` and shared components in `shared/ui` registered as projects.

This is load-bearing rather than a matter of taste. `contracts/*.yaml` is the single source of truth, and `tpx gen` emits both the C# client and the Angular client from it. In one repository, changing an endpoint is one contract edit, one handler change, one Angular call-site change — a single commit, verified by a single `tpx verify`, reviewed in a single PR, inside a single worktree that one agent can hold entirely in context.

Splitting front from back breaks exactly that. The generated Angular client would have to be published as a versioned npm package and pinned; every breaking contract change becomes two coordinated PRs across two repositories with no atomic commit between them. `contract-guardian` could no longer see its own consumers, which makes verification item 7 unachievable, and every cross-stack agent task would need two checkouts and two contexts.

Boundary enforcement: a `tpx verify boundaries` check fails the build if a module project references another module's `.Domain` or `.Infrastructure` directly. Modules may only talk through `shared/clients/*` (generated) or `Shared.Kernel`.

---

## Phase 0 — the harness

### 0.1 Repository and verification loop (do this first, nothing else matters until it's fast)

- [x] `git init`, root `.gitignore`, `.editorconfig`.
- [x] `Directory.Build.props` with `TreatWarningsAsErrors=true` and `Nullable=enable`.
- [x] `docker-compose.yml`: Postgres 16, with `COMPOSE_PROJECT_NAME` and port read from env (critical for parallel worktrees — see 0.5).
- [x] `tools/tpx` — a .NET 9 console CLI (see [tools/tpx/README.md](tools/tpx/README.md)):
  - `tpx verify <module>` → build + unit tests + contract lint. **Hard target: under 60 seconds.**
  - `tpx verify --affected` → maps `git diff` paths to affected modules.
  - `tpx verify boundaries` → fails if a module references another module's `.Domain`/`.Infrastructure`.
  - `tpx test <module> --integration` → Testcontainers, real Postgres.
  - `tpx contract lint` → OpenAPI valid + no breaking change vs `main` (use `oasdiff`).
    - **Deferred, revisit once Auth module exists:** also diff controller route attributes against contract paths, failing lint if an endpoint has no matching contract entry. Closes the gap where a dev/agent writes a handler with zero contract coverage — the `new-endpoint` skill documents contract-first but doesn't enforce it mechanically.
  - `tpx gen` → regenerate `shared/clients` from `contracts/` (NSwag for C#, `ng-openapi-gen` for Angular).
  - `tpx worktree new <module>/<feature>` → git worktree + allocated Postgres port + unique `COMPOSE_PROJECT_NAME`.
  - All commands run today but report "nothing found," since no `modules/` or `contracts/` exist yet.
- [x] Install `gh` CLI (missing) and `pnpm`/corepack (missing) — both needed for the PR-based agent workflow.

**This section is the single highest-leverage part of the plan.** Agents that can verify their own work are worth several times agents that cannot; without it, subagents produce plausible-looking code and you spend the savings on review.

### 0.3 Subagents (`.claude/agents/`) — done

| Agent | Model | Role |
|---|---|---|
| `module-architect` | Opus | Reads a contract + `GOALS.md`, produces an implementation plan. No writes. |
| `dotnet-implementer` | Sonnet | Implements C# against a given plan. Must run `tpx verify <module>` before reporting done. |
| `angular-implementer` | Sonnet | Same for Angular projects. |
| `contract-guardian` | Sonnet, read-only | Given a contract diff, reports breaking changes and every downstream consumer. |
| `test-writer` | Sonnet | Writes xUnit/Jasmine tests from acceptance criteria. |

Reuse what's already installed rather than duplicating: `caveman:cavecrew-investigator` for code location, `caveman:cavecrew-reviewer` for diff review, built-in `/code-review` and `/security-review`.

Async usage: launch `dotnet-implementer` and `angular-implementer` in the background on independent worktrees; poll nothing — the harness notifies on completion.

### 0.4 Project skills (`.claude/skills/`) — done

- `new-module` — scaffolds the full module tree above, its `.sln`, its CI job, its `CLAUDE.md`, an empty contract, and its MCP server; registers it in `.mcp.json` and `CODEOWNERS`.
- `new-endpoint` — contract-first: edit `contracts/<m>.vN.yaml` → `tpx gen` → implement handler → write test → `tpx verify`.
- `wire-module` — connect module A to module B through the generated client, including the DI registration and a contract test.
- `mcp-expose` — regenerate a module's MCP tool surface from its OpenAPI.

Each is a short procedure, not an essay — that is the only way a skill pays back its own token cost.

### 0.5 Hooks (`.claude/settings.json`, project scope) — done, cross-platform

- Keep the existing global `rtk hook claude` PreToolUse on Bash.
- **SessionStart hook** installs `tools/tpx` as a real `dotnet` global tool (`dotnet pack` +
  `dotnet tool update --global`), no-ops gracefully if `dotnet` isn't present yet.
  (`session-start.sh`, written via the `session-start-hook` skill)
- **PreToolUse on Edit/Write matching `**/clients/**` or `**/generated/**` → block.** Forces contract-first discipline mechanically instead of by instruction. (`tpx hook block-generated`)
- PostToolUse on Edit/Write of touched project → narrow build/lint check. (`tpx hook verify-on-save`)
- Stop hook → `tpx verify --affected`. (`tpx hook stop-verify`)

Hooks cost zero tokens and catch agent errors at the moment they happen, which is the cheapest possible point.

**Fixed — POSIX port.** The four hooks used to run `pwsh -NoProfile -File .claude/hooks/*.ps1`,
and `pwsh` does not exist in a Linux cloud session (`pwsh: command not found`) — PR #2 only
documented this, it shipped no fix. All three were rewritten as `bash` scripts with identical
behavior. The original `.ps1` files are left in place for the Windows dev machine, just no
longer wired into `settings.json`.

**Fixed — `tpx` unreachable from routines and subagents.** The first cut of the above put a
locally-built `tpx` on `PATH` only via `$CLAUDE_ENV_FILE`, a Claude-Code-session mechanism —
the Friday `/schedule` routine's own subprocess (and a subagent's) could start from a PATH
snapshot taken before that file is sourced, so bare `tpx` wasn't found even though SessionStart
had run. Fixed by installing `tpx` as a genuine `dotnet tool --global` (lands in
`~/.dotnet/tools`, on the machine's real, persistent `PATH`, not dependent on Claude's
env-file plumbing) and by moving the three PreToolUse/PostToolUse/Stop hook bodies from
standalone `bash`+`jq` scripts into `tpx hook <name>` subcommands (`tools/tpx/Hooks.cs`) —
one binary, no `jq` dependency, callable the same way a routine calls any other `tpx`
command. **Known tradeoff, accepted:** a global tool install is one shared, mutable
location per machine — two `tpx worktree new` sessions running SessionStart concurrently
on the same machine will race, and whichever finishes last wins for both. Revisit
(e.g. a per-worktree local tool manifest) if that collision is ever actually observed.

**Still open — persistent .NET SDK provisioning.** The SessionStart hook above only builds
`tpx` *if* `dotnet` is already on `PATH`; it does not install the SDK itself. A fresh cloud
container still has no .NET SDK pre-installed. Working recipe, verified end to end in this
session: `apt-get install -y dotnet-sdk-10.0`, plus `DOTNET_ROLL_FORWARD=Major` as an
environment variable (`builds.dotnet.microsoft.com`, where `dotnet-install.sh` fetches from,
is *not* on the Trusted egress allowlist and 403s, while `dotnet.microsoft.com`/`nuget.org`
resolve fine; Ubuntu 24.04 has no `dotnet-sdk-9.0` package but SDK 10 builds this `net9.0`
tree and roll-forward runs the output). This belongs in the cloud environment's own setup
script (claude.ai/code environment settings, not repo content) — until someone applies it
there, every fresh cloud session needs the `apt-get install` run by hand once before the
SessionStart hook has anything to build.

### 0.6 Worktrees — done

`tpx worktree new <module>/<feature>` creates the worktree **and** allocates it a port offset plus a unique `COMPOSE_PROJECT_NAME`, writing them into the worktree's `.env`. Port state is kept in the shared git dir (`git rev-parse --git-common-dir`), not the per-worktree checkout, so allocation is consistent across worktrees. Port collision between concurrent integration-test runs is the failure that kills parallel worktree agents; solving it up front is what makes asynchronous subagents actually usable.

`.claude/` lives at the repo root and is shared by all worktrees automatically.

### 0.7 MCP servers — done (Auth)

One per module, C# `ModelContextProtocol` SDK, stdio transport, registered in root `.mcp.json` so every session and worktree picks them up. Tool surface per module:

- `get_openapi()` — the contract
- `list_endpoints()` / `describe_entity(name)` — cheap structural queries
- `find_consumers(entity_or_field)` — who breaks if this changes
- `run_tests(filter?)` — lets a remote agent verify without shell access
- `get_migrations_status()`

Auth's MCP server (`modules/auth/src/TPXSoft.Auth.Mcp`) is built and registered as `tpxsoft-auth` in root `.mcp.json`; all six tools above are implemented (`ContractTools.cs`: `get_openapi`, `list_endpoints`, `describe_entity`, `find_consumers`; `OperationsTools.cs`: `run_tests`, `get_migrations_status`). It is the template for `new-module` going forward.

### 0.8 Loops, schedules, and goal tracking — done

- **`/schedule`** — Friday 18:00 cloud routine is created: `/code-review` over the week's merges, plus `tpx contract lint` drift check, plus a `GOALS.md` progress report. Lives in Claude Code's own scheduler (cron), not as a file in this repo — nothing under `.claude/` to commit for it.
- **`/loop`** — within-session, self-paced: "implement remaining endpoints in `contracts/auth.v1.yaml` until `tpx verify auth` is green." Now usable — `contracts/auth.v1.yaml` exists — but not yet actually invoked.
- **Goal tracking** — `GOALS.md` exists (written by the Friday routine's first run) with milestone acceptance criteria. `.claude/skills/tpx-goal` re-verifies each checkbox against real repo state (files, `tpx verify`/`tpx contract lint` output, agent/skill/hook presence) rather than trusting a prior run's claim, flips only what evidence disagrees with, and reports a progress summary — invoked on request, not a hook.

### 0.9 Capstone (do after Phase 1, not now)

Bundle `.claude/agents` + `.claude/skills` + hooks into a `tpxsoft` plugin served from your own marketplace repo; validate with `claude plugin eval`. Add a headless `claude -p` PR-review job in CI.

### 0.10 CLAUDE.md hierarchy — done (Auth)

Root `CLAUDE.md` already exists and stays short — repo map, `tpx` commands, naming conventions, "never edit `shared/clients` or any `generated/` path", commit/PR conventions.

- `modules/auth/CLAUDE.md`: bounded context, entities, its `tpx verify` line, known consumers. Loaded only when an agent works in that module. Template for every module after.

---

## Phases after the harness

| Phase | Deliverable | New technique exercised |
|---|---|---|
| 1 | **Auth module** — OIDC-ish JWT issuance, users, orgs, roles. Full harness guinea pig. | Every tool above, on one module. MCP server template established. |
| 2 | **Documents module** — upload, versioning, metadata, permissions delegated to Auth. Introduce .NET Aspire for local orchestration. | Contract-first cross-module wiring; `contract-guardian`; parallel worktrees. |
| 3 | **Sharepoint-lite** (Angular + API) consuming both. **Extract Auth to its own repo.** | Multi-repo tax learned deliberately: NuGet/npm publishing, version pinning, cross-repo breaking change. |
| 4+ | Word (Sharepoint save), Onenote (Sharepoint import), Forms (mail report), Outlook, Teams | Chat/realtime and collaborative rich text last — hardest infra and hardest algorithms respectively. |

---

## Verification

Phase 0 is complete when all of the following pass from a clean clone:

1. `docker compose up -d && tpx verify auth` — green in **under 60 seconds**.
2. `tpx test auth --integration` — Testcontainers spins Postgres, tests pass, container torn down.
3. `tpx worktree new auth/demo` then running `tpx test auth --integration` **simultaneously** in both the main tree and the worktree — both pass, no port conflict.
4. Editing a file under `shared/clients/` is **blocked by the hook**, with a message pointing at `contracts/`.
5. Editing a `.cs` file with a deliberate compile error triggers the PostToolUse build and surfaces the error immediately.
6. In a fresh session: `claude` → ask "what fields does the Auth `User` entity have?" — answered via the Auth MCP server without reading any `.cs` source file.
7. `tpx contract lint` fails when a required field is removed from `contracts/auth.v1.yaml`, and `contract-guardian` names the downstream consumers.
8. A background `dotnet-implementer` run on a worktree produces a green `tpx verify auth` and a PR via `gh`.
9. The Friday `/schedule` routine executes once on demand and writes a review + `GOALS.md` progress summary.

If item 1 exceeds 60 seconds, stop and fix the loop before writing any Phase 1 feature code.

---

## Working agreement

Nothing in Phase 0 is scaffolded ahead of time. Each step is built when it is asked for, so that every piece of the harness is understood rather than inherited. Do not scaffold the repository, write the `tpx` CLI, create agent or skill definitions, or configure hooks until that specific step is requested.

This file is the shared reference for the project. Update it whenever a decision changes.
