# tpx

`tpx` is the verification-loop CLI for TPXSoft. It is the one command every agent, hook, and CI job calls to answer "is this change actually correct?" — see [CLAUDE.md](../../CLAUDE.md) and [PLAN.md](../../PLAN.md) §0.1 for the full rationale.

## Why this exists

The project is built around AI agents writing the code. The main risk with that workflow is an agent producing code that looks correct but silently breaks something: a failing test, a broken build, a module reaching into another module's internals it shouldn't touch, or an API contract change that breaks a consumer.

Rather than every agent, hook, or skill knowing the details of `dotnet build`, `dotnet test`, `git diff`, `oasdiff`, etc., they all call `tpx`. This indirection is deliberate: the tools underneath can change later without touching every agent definition, hook, and skill that depends on verification.

## Commands

| Command | What it does |
|---|---|
| `tpx verify <module>` | Builds the module's solution, runs its unit tests, lints its contract. Target: under 60 seconds, so it can run in a tight agent loop. |
| `tpx verify --affected` | Maps `git diff` (vs `main`) to modules and runs `verify` only on the ones that changed. |
| `tpx verify boundaries` | Fails if a module's project file references another module's `.Domain`/`.Infrastructure` directly. Modules may only talk through a generated client or `Shared.Kernel`. |
| `tpx test <module> --integration` | Runs integration tests against a real Postgres (via Testcontainers) instead of unit-test fakes. Slower, needs Docker, kept separate from `verify`. |
| `tpx contract lint` | Validates every `contracts/*.yaml` is structurally sound, and flags a breaking change if a required field or endpoint was removed compared to `main`. |
| `tpx gen` | Regenerates the C#/Angular API clients under `shared/clients/` from the contracts. Contracts are the source of truth; generated clients are never hand-edited. |
| `tpx worktree new <module>/<feature>` | Creates a git worktree for parallel agent work, and allocates it a unique Postgres port + `COMPOSE_PROJECT_NAME` so two worktrees can run integration tests at the same time without colliding. |
| `tpx worktree list` | Lists allocated worktrees and their ports, flagging any whose directory is gone but whose port allocation wasn't released. |
| `tpx worktree rm <module>/<feature>` | Removes the git worktree, deletes its branch, and frees the port allocation — closes the loop `new` opens. |

## Who executes these, and when

**Agents** (PLAN.md §0.3):
- `dotnet-implementer` / `angular-implementer` must run `tpx verify <module>` before reporting a task done — their own self-check.
- A background `dotnet-implementer` run on a worktree should end with a green `tpx verify <module>` before opening a PR.
- `contract-guardian` runs `tpx contract lint` when reviewing a contract diff.
- A session or skill runs `tpx worktree new <module>/<feature>` before starting parallel work.

**Hooks** (PLAN.md §0.5 — wired in `.claude/settings.json`):
- A **Stop hook** → `tpx verify --affected`, firing automatically at the end of every session regardless of whether the agent remembered to self-check.
- **PostToolUse hooks** on `.cs`/`.ts` edits → narrower, faster checks on just the touched project, catching a broken edit the moment it happens.
- A **PreToolUse hook** blocks edits under `shared/clients/**` or `**/generated/**`, forcing contract-first discipline mechanically (the only legitimate way those files change is `tpx gen`).

The split matters: hooks are a backstop that fires no matter what the agent does, cost no extra tokens, and catch errors at the cheapest possible moment. Agents calling `tpx verify` themselves is the fast inner loop — check, fix, check again — before ever reaching that backstop.

## How this helps quality and speed

- **Quality**: an agent that can check its own work catches its own mistakes before a human sees them. Boundary checks and contract linting catch architectural violations a quick human review might miss.
- **Speed**: because `verify` is fast and uniform across modules, agents can iterate rapidly without waiting on a slow CI pipeline. Because worktrees don't collide on ports, multiple agents can verify in parallel instead of queuing behind each other.

## Current status

The Auth module (Phase 1) is built, so every command above does real work against it: `tpx verify auth` builds, tests, and lints its contract in single-digit seconds; `tpx test auth --integration` runs against a real Postgres via Testcontainers; `tpx contract lint` genuinely fails a breaking change and names the affected endpoints; `tpx gen` regenerates `shared/clients/csharp` from `modules/auth/nswag.json`. `tpx worktree new/list/rm` manage real worktrees with allocated ports, independent of any module existing. Auth is still the only module — commands that operate across modules (`find_consumers`, cross-module `wire-module`) remain unproven until a second one exists (see [PLAN.md](../../PLAN.md) §0.7, §0.4).

## Running it

From source (dev loop):

```bash
cd tools/tpx
dotnet run -- verify boundaries
```

Installed as a global tool (what agents/hooks/CI/routines should call as plain `tpx`) — this is what `.claude/hooks/session-start.sh` does every session:

```bash
dotnet pack tools/tpx -o tools/tpx/.nupkg --nologo -v quiet
dotnet tool update --global --add-source tools/tpx/.nupkg TPXSoft.Tpx
```

Installs to `~/.dotnet/tools`, on the machine's real, persistent `PATH` — resolves for the main session, subagents, and any routine's own subprocess, not just this Claude Code session (see PLAN.md §0.5).
