# GOALS.md — milestone acceptance criteria

Tracks the milestones in [PLAN.md](PLAN.md). Checkboxes are verified against actual repo
state (code, files, commits), not against notes or intent. Updated by the weekly
maintenance routine (PLAN.md §0.8) — see that section for how it should evolve
(a `tpx-goal` skill, eventually).

## Phase 0 — The Harness

### 0.1 Repository and verification loop

- [x] `git init`, root `.gitignore`, `.editorconfig`
- [x] `Directory.Build.props` with `TreatWarningsAsErrors=true` and `Nullable=enable`
- [x] `docker-compose.yml`: Postgres 16, `COMPOSE_PROJECT_NAME`/port read from env
- [x] `tools/tpx` CLI implements all seven commands (`verify <module>`, `verify --affected`,
      `verify boundaries`, `test <module> --integration`, `contract lint`, `gen`,
      `worktree new <module>/<feature>`) — builds clean and runs; `contract lint`,
      `verify boundaries` and `verify --affected` each executed and exited 0 with the
      documented "nothing found" output
- [x] `gh` and `pnpm` installed (per PLAN.md 0.1 checklist; `pnpm` confirmed present)

### 0.3 Subagents (`.claude/agents/`)

- [x] `module-architect`
- [x] `dotnet-implementer`
- [x] `angular-implementer`
- [x] `contract-guardian`
- [x] `test-writer`

### 0.4 Project skills (`.claude/skills/`)

- [x] `new-module`
- [x] `new-endpoint`
- [x] `wire-module`
- [x] `mcp-expose`

### 0.5 Hooks (`.claude/settings.json`, project scope)

All four are written and wired in `settings.json`. None of them **fires on Linux**: every
hook command is `pwsh -NoProfile -File .claude/hooks/*.ps1`, and `pwsh` is not present in
a Linux cloud session (`pwsh: command not found`). They work on the Windows dev machine
only, so the Stop-hook backstop in particular is absent exactly where an unattended agent
runs. Marked done-but-qualified rather than unchecked, since the gap is portability, not
absence.

- [x] Global `rtk hook claude` PreToolUse on Bash, copied into project settings
- [x] PreToolUse on Edit/Write blocking `**/clients/**` and `**/generated/**`
      (`.claude/hooks/block-generated.ps1`) — Windows only
- [x] PostToolUse on Edit/Write running build/lint for the touched project
      (`.claude/hooks/verify-on-save.ps1`) — Windows only
- [x] Stop hook running `tpx verify --affected` (`.claude/hooks/stop-verify.ps1`)
      — Windows only
- [ ] Hooks run on Linux as well as Windows — blocked: needs either `pwsh` installed in
      the cloud environment's setup script, or the three `.ps1` scripts ported to POSIX
      shell (or a cross-platform runner) so the same hooks fire in both places

### 0.6 Worktrees

- [x] `tpx worktree new <module>/<feature>` creates a git worktree, allocates a
      Postgres port offset, and writes a unique `COMPOSE_PROJECT_NAME` + port into
      the worktree's `.env` (`tools/tpx/Worktrees.cs`)
- [ ] Verified end-to-end with two worktrees running integration tests simultaneously
      — blocked: no module exists yet to test against (Auth is Phase 1)

### 0.7 MCP servers

- [ ] Auth module MCP server (`get_openapi`, `list_endpoints`, `describe_entity`,
      `find_consumers`, `run_tests`, `get_migrations_status`) — blocked: Auth module
      (Phase 1) doesn't exist yet
- [ ] `.mcp.json` registering module MCP servers — blocked: no MCP server exists yet
      to register

### 0.8 Loops, schedules, and goal tracking

- [x] Weekly `/schedule` cloud routine exists and runs PR review + contract lint +
      `GOALS.md` update (this routine)
- [ ] `tpx-goal` skill that reads/updates `GOALS.md` and reports progress at session
      start — not built yet; this file is currently maintained by hand/by the weekly
      routine instead

### 0.9 Capstone (do after Phase 1)

- [ ] `.claude/agents` + `.claude/skills` + hooks bundled into a `tpxsoft` plugin,
      validated with `claude plugin eval` — blocked: deferred until after Phase 1
- [ ] Headless `claude -p` PR-review job in CI — blocked: no CI workflows exist yet

### 0.10 CLAUDE.md hierarchy (do after Phase 1's first module)

- [ ] `modules/auth/CLAUDE.md` — blocked: Auth module (Phase 1) doesn't exist yet

### Phase 0 completion (PLAN.md "Verification", all 9 must pass from a clean clone)

- [ ] 1. `docker compose up -d && tpx verify auth` green in under 60s
- [ ] 2. `tpx test auth --integration` passes against real Postgres via Testcontainers
- [ ] 3. `tpx worktree new auth/demo` + simultaneous integration test runs in both
      trees, no port conflict
- [ ] 4. Editing a file under `shared/clients/` is blocked by the hook
- [ ] 5. A deliberate `.cs` compile error triggers the PostToolUse build and surfaces
      immediately
- [ ] 6. A fresh session answers an Auth `User` entity question via the Auth MCP
      server without reading `.cs` source
- [ ] 7. `tpx contract lint` fails on a removed required field, `contract-guardian`
      names downstream consumers
- [ ] 8. A background `dotnet-implementer` run on a worktree produces a green
      `tpx verify auth` and a PR via `gh`
- [ ] 9. The Friday `/schedule` routine executes on demand and writes a review +
      `GOALS.md` progress summary — **partially verifiable**: this routine is that
      execution, but items 1–8 below it are still blocked

All nine are blocked on the same root cause: **no `modules/` or `contracts/` exist
yet** (Phase 1, Auth module, hasn't started).

## Phase 1 — Auth module

- [ ] OIDC-ish JWT issuance, users, orgs, roles implemented under `modules/auth/`
- [ ] `contracts/auth.v1.yaml` exists and is the source of truth
- [ ] Auth MCP server built (becomes the template for `new-module`)
- [ ] All Phase 0 verification items (above) pass using Auth as the guinea pig

Not started — no `modules/` directory exists in the repo.

## Phase 2 — Documents module

- [ ] `modules/documents/` implemented: upload, versioning, metadata, permissions
      delegated to Auth
- [ ] .NET Aspire introduced for local orchestration
- [ ] Cross-module wiring via generated client, verified by `contract-guardian`

Not started — blocked on Phase 1 (Auth).

## Phase 3 — Sharepoint-lite

- [ ] `apps/sharepoint/api` and `apps/sharepoint/web` (Angular) consuming both
      Auth and Documents
- [ ] Auth extracted to its own repository

Not started — blocked on Phases 1–2.

## Phase 4+ — Word, Onenote, Forms, Outlook, Teams

- [ ] Word (Sharepoint save)
- [ ] Onenote (Sharepoint import)
- [ ] Forms (mail report)
- [ ] Outlook
- [ ] Teams (chat/realtime)

Not started — blocked on Phase 3.
