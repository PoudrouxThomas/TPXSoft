# TPXSoft

TPXSoft is a modular clone of the Microsoft productivity suite (Teams, Outlook, Sharepoint, Word, OneNote, Forms). The product itself is not the point — this repository exists to learn AI-assisted development at scale: MCP, git worktrees, skills, asynchronous subagents, loops/schedules, and goal tracking, on a codebase large enough that those techniques are actually necessary.

If you're new here, read this file for the day-to-day workflow. For architecture decisions and rationale, read [PLAN.md](PLAN.md). For the exact repo map, conventions, and `tpx` command reference, read [CLAUDE.md](CLAUDE.md) — that file is also what Claude Code loads automatically every session.

## Status

Phase 0 (the harness) is in progress. Check [CLAUDE.md](CLAUDE.md#current-state) for exactly what's built versus still a target — don't assume anything in this README's workflow section exists until you've checked. As of now: the repo skeleton, `Directory.Build.props`, `docker-compose.yml`, the `tpx` CLI (commands exist, mostly report "nothing found" since there are no modules or contracts yet), and the five core subagents (`.claude/agents/`) are in place. Skills, hooks, MCP servers, and the module tree itself are not built yet.

## Prerequisites

- .NET 9 SDK
- Docker + docker-compose (for Postgres 16)
- Node + pnpm (corepack)
- `gh` CLI, authenticated
- `claude` (Claude Code CLI)

## Quickstart

```bash
docker compose up -d
cd tools/tpx && dotnet build -c Release
```

Add `tools/tpx/bin/Release/net9.0/` to `PATH` so `tpx` runs from anywhere. Then:

```bash
tpx verify boundaries
```

Until Phase 1 lands (the Auth module), most `tpx` commands correctly report "nothing found" — there's nothing yet for them to build, test, or lint.

## The `tpx` CLI

Every agent, hook, and CI job talks to the codebase through `tpx`, never through `dotnet`/`ng`/`oasdiff` directly. This keeps the underlying toolchain swappable without touching agent definitions. Full command reference: [tools/tpx/README.md](tools/tpx/README.md).

| Command | Purpose |
|---|---|
| `tpx verify <module>` | Build + unit tests + contract lint. Must stay under 60s. |
| `tpx verify --affected` | Runs `verify` on modules touched by the current diff. |
| `tpx verify boundaries` | Fails if a module reaches into another module's `.Domain`/`.Infrastructure`. |
| `tpx test <module> --integration` | Integration tests against real Postgres (Testcontainers). |
| `tpx contract lint` | Contract validity + breaking-change check vs `main`. |
| `tpx gen` | Regenerates `shared/clients/` from `contracts/`. |
| `tpx worktree new <module>/<feature>` | New worktree with an isolated Postgres port + `COMPOSE_PROJECT_NAME`. |

## Development workflow

Everything below assumes `contracts/<module>.vN.yaml` is the source of truth for any module's public shape, and that a task isn't "done" until its `tpx verify` is green. These aren't just conventions — a `PreToolUse` hook is planned to mechanically block hand-edits to generated clients, and a Stop hook to run `tpx verify --affected` automatically.

### Adding a new module

1. **Write the goal.** Add a milestone to `GOALS.md` with machine-checkable acceptance criteria (e.g. "`tpx verify auth` green", "contract covers N endpoints").
2. **Write the contract.** Create `contracts/<module>.v1.yaml`, or ask Claude to draft one from the `GOALS.md` entry, then review and correct it yourself. The contract is the single source of truth — get it right before any code exists.
3. **Scaffold.** Once the `new-module` skill exists, it creates the module tree, its `.sln`, CI job, `CLAUDE.md`, and MCP server, and registers it in `.mcp.json`/`CODEOWNERS`. Until then, follow the layout in [CLAUDE.md](CLAUDE.md) by hand.
4. **Generate clients.** `tpx gen`.
5. **Plan.** Ask Claude to run the `module-architect` agent against the contract and `GOALS.md`. It reads only — it produces an implementation plan (entities, endpoints, task order), it doesn't write code.
6. **Implement.** Claude dispatches `dotnet-implementer` and/or `angular-implementer` against that plan. Each must land a green `tpx verify <module>` before reporting done.
7. **Test.** `test-writer` fills any coverage gap against the acceptance criteria from step 1.
8. **Review the contract diff.** If the contract changed since you started, `contract-guardian` reports breaking changes and every downstream consumer.
9. **Ship.** Open a PR via `gh`. A background implementer run on a worktree (see below) should already end with a green verify and an open PR.

### Adding a feature to an existing module

Same shape as above, minus scaffolding: extend `GOALS.md` if there's a new acceptance criterion worth tracking, update the contract if the public shape changes, `tpx gen`, then plan (if the change is non-trivial) → implement → test → verify. For a small, well-understood feature, you can skip `module-architect` and go straight to the implementer agent — use judgment on whether a plan is worth the token cost.

### Adding a single endpoint

The narrow case, matching the (planned) `new-endpoint` skill:

1. Edit `contracts/<module>.vN.yaml` to add the operation.
2. `tpx gen`.
3. Implement the handler (`dotnet-implementer`/`angular-implementer`).
4. Write the test (`test-writer`).
5. `tpx verify <module>` green.
6. PR.

No `module-architect` needed here — the contract change already fully specifies the work.

### Hotfix

1. Isolate the change: `tpx worktree new <module>/hotfix-<name>` once that command does real work, or a plain feature branch today.
2. Fix directly — skip `module-architect` for a small, well-understood bug.
3. Add a regression test via `test-writer` if one doesn't already cover the bug.
4. `tpx verify <module>` green before anything else.
5. PR, and call out in the description that it's a hotfix so review can be fast-tracked.

If the fix touches a contract (rare for a hotfix — a red flag if it does), run `contract-guardian` before merging.

## Agents

Defined in [.claude/agents/](.claude/agents/). Invoke by name or just describe the task — Claude picks the right one.

| Agent | Model | Role |
|---|---|---|
| `module-architect` | Opus | Read-only. Turns a contract + `GOALS.md` into an implementation plan. |
| `dotnet-implementer` | Sonnet | Writes C# against a plan. Runs `tpx verify <module>` before reporting done. |
| `angular-implementer` | Sonnet | Same, for Angular. |
| `contract-guardian` | Sonnet, read-only | Reports breaking contract changes and every downstream consumer. |
| `test-writer` | Sonnet | Writes xUnit/Jasmine tests from acceptance criteria. |

Also available and already installed: `caveman:cavecrew-investigator` (locate code), `caveman:cavecrew-reviewer` (diff review), plus built-in `/code-review` and `/security-review`.

## Parallel work

`tpx worktree new <module>/<feature>` creates a git worktree and allocates it its own Postgres port and `COMPOSE_PROJECT_NAME`, so two agents can run integration tests at the same time without colliding. `.claude/` lives at the repo root and is shared automatically by every worktree. This is what makes launching `dotnet-implementer`/`angular-implementer` in the background on independent modules actually safe.

## Not built yet

Tracked in [PLAN.md](PLAN.md) — don't assume these exist without checking:

- Project skills (`new-module`, `new-endpoint`, `wire-module`, `mcp-expose`) — §0.4
- Hooks (client-protection, build-on-save, Stop-hook verify) — §0.5
- Per-module MCP servers — §0.7
- `GOALS.md` itself and the `tpx-goal` tracking skill — §0.8
- The module tree, `contracts/`, and everything in Phase 1+ — §Phases after the harness
