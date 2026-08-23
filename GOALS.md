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
- [x] `gh` and `pnpm` installed (per PLAN.md 0.1 checklist) — **caveat added this
      session**: `pnpm` confirmed present here, but this particular sandbox has
      neither `gh` nor `oasdiff` on `PATH` (both `which` exit 1). GitHub MCP tools
      stand in for `gh` where needed; `oasdiff`'s absence is the direct cause of
      Phase 0 verification item 7 not holding today (see below). Not re-checking
      this box to unchecked since it may be sandbox-specific rather than universal
      — but don't assume either tool is actually there without checking first.

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

All five are written, wired in `settings.json`, and **fire on Linux as well as Windows**
now. The three PreToolUse/PostToolUse/Stop hooks were first rewritten from `pwsh` to `bash`,
then moved again into `tpx hook <name>` subcommands (`tools/tpx/Hooks.cs`) — same behavior,
no `bash`+`jq` dependency, one binary. A SessionStart hook installs `tpx` as a `dotnet`
global tool every session, so it resolves for the main thread, subagents, and the remote
`/schedule` routine's own subprocess alike (PLAN.md §0.5 "Fixed — `tpx` unreachable from
routines and subagents").

- [x] Global `rtk hook claude` PreToolUse on Bash, copied into project settings
- [x] SessionStart hook installing `tools/tpx` as a global `dotnet tool` and onto `PATH`
      (`.claude/hooks/session-start.sh`) — no-ops if `dotnet` isn't present rather than
      failing session start
- [x] PreToolUse on Edit/Write blocking `**/clients/**` and `**/generated/**`
      (`tpx hook block-generated`)
- [x] PostToolUse on Edit/Write running build/lint for the touched project
      (`tpx hook verify-on-save`)
- [x] Stop hook running `tpx verify --affected` (`tpx hook stop-verify`)
- [ ] .NET SDK provisioned automatically in fresh cloud containers — blocked: that's an
      environment-setup-script change outside the repo (claude.ai/code environment
      settings), not something a SessionStart hook can do. Verified working recipe is
      recorded in PLAN.md §0.5 (`apt-get install -y dotnet-sdk-10.0` +
      `DOTNET_ROLL_FORWARD=Major`); until applied there, run it by hand once per fresh
      session before the SessionStart hook above has anything to build.

### 0.6 Worktrees

- [x] `tpx worktree new <module>/<feature>` creates a git worktree, allocates a
      Postgres port offset, and writes a unique `COMPOSE_PROJECT_NAME` + port into
      the worktree's `.env` (`tools/tpx/Worktrees.cs`) — confirmed live: created
      `auth/verification-demo`, got a distinct port (5433) and `COMPOSE_PROJECT_NAME`,
      ran `tpx verify auth` green inside it, cleaned up
- [ ] Verified end-to-end with two worktrees running integration tests
      *simultaneously* — blocked: this sandbox's Docker daemon can't start
      (permission-restricted container, `ulimit: Operation not permitted`), and
      Testcontainers needs a live daemon. The worktree/port-allocation mechanism
      itself is confirmed working (above); only the concurrent-Postgres part is
      unverified here.

### 0.7 MCP servers

- [x] Auth module MCP server (`get_openapi`, `list_endpoints`, `describe_entity`,
      `find_consumers`, `run_tests`, `get_migrations_status`) — built
      (`modules/auth/src/TPXSoft.Auth.Mcp`) and verified over real stdio JSON-RPC
      (`initialize`, `tools/list`, and a `tools/call` per tool): `list_endpoints`/
      `describe_entity` return data matching `contracts/auth.v1.yaml`, `find_consumers`
      correctly reports zero consumers (Auth is the first module), `run_tests`/
      `get_migrations_status` both shell out successfully
- [x] `.mcp.json` registering module MCP servers — registers `tpxsoft-auth`
      (stdio, `dotnet run --project modules/auth/src/TPXSoft.Auth.Mcp/...`)

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

- [x] `modules/auth/CLAUDE.md` — bounded context, entities, endpoints, JWT claim set,
      config keys, deferred decisions, environment notes, verify line, known consumers

### Phase 0 completion (PLAN.md "Verification", all 9 must pass from a clean clone)

Re-run against the real Auth module for the first time this session. Six of nine
pass outright; the other three have a specific, named blocker rather than being
generically "not done" — see each line.

- [ ] 1. `docker compose up -d && tpx verify auth` green in under 60s —
      **`tpx verify auth` alone confirmed green repeatedly (10.3–13.8s, well under
      budget)**; `docker compose up -d` itself can't run in this sandbox (Docker
      daemon can't start, see 0.6)
- [ ] 2. `tpx test auth --integration` passes against real Postgres via Testcontainers
      — 13 tests written and structurally complete
      (`modules/auth/tests/TPXSoft.Auth.IntegrationTests`), but blocked by the same
      Docker-daemon gap: they fail with `DockerUnavailableException`, not a test or
      code defect
- [x] 3. `tpx worktree new auth/demo` works and `tpx verify auth` runs green inside
      the new worktree with its own allocated port — confirmed live. *Simultaneous*
      integration-test runs in two trees specifically is still blocked by the
      Docker gap (tracked under 0.6, not re-listed as a separate failure here)
- [x] 4. Editing a file under `shared/clients/` is blocked by the hook — confirmed
      live: a real `Write` to `shared/clients/csharp/HookTestProbe.cs` was rejected
      by `block-generated.sh` with the expected message
- [x] 5. A deliberate `.cs` compile error triggers the PostToolUse build and surfaces
      immediately — confirmed live: a syntax error added to `Role.cs` via `Edit`
      was caught and blocked by `verify-on-save.sh` before the edit completed;
      reverted immediately after
- [x] 6. An Auth `User` entity question is answered via the Auth MCP server without
      reading `.cs` source — confirmed at the protocol level (`describe_entity("User")`
      over stdio JSON-RPC returns the correct schema, sourced from
      `contracts/auth.v1.yaml`). Not separately re-confirmed by spawning a literal
      fresh Claude Code session and observing its tool choice.
- [ ] 7. `tpx contract lint` fails on a removed required field, `contract-guardian`
      names downstream consumers — **tested and found not to hold today**: on a
      throwaway branch, removing `refreshToken` from `TokenPair`'s `required` list
      still passed `tpx contract lint` (exit 0), because `oasdiff` isn't installed
      in this sandbox and `Contracts.Lint` gracefully skips the breaking-change
      diff when it's missing (by design, not a bug) — and separately, `main` has
      no version of `contracts/auth.v1.yaml` to diff against yet until this branch
      merges. Real gap: install `oasdiff` (environment setup script, same category
      as the .NET SDK item under 0.5) before this check does anything
- [ ] 8. A background `dotnet-implementer` run on a worktree produces a green
      `tpx verify auth` and a PR via `gh` — **partially demonstrated**: `module-architect`,
      `dotnet-implementer`, and `test-writer` all ran as background subagents this
      session and each produced a green `tpx verify auth`, but none ran inside an
      isolated worktree specifically, and no PR was opened by an agent directly
      (`gh` isn't installed in this sandbox either — the GitHub MCP tools stand in
      for it here). Worth a real run once there's a natural small follow-up task.
- [ ] 9. The Friday `/schedule` routine executes on demand and writes a review +
      `GOALS.md` progress summary — the routine (`trig_01Ab56gX37f3vCqfXA1nqqJD`,
      "TPXSoft Weekly Review", Fridays 16:00 UTC) exists, is enabled, and last ran
      2026-08-21; deliberately **not** fired on demand this session to avoid an
      unplanned second PR/branch outside this task's scope

## Phase 1 — Auth module

- [x] OIDC-ish JWT issuance, users, orgs, roles implemented under `modules/auth/`
      (minimal scope: org created at registration, one org per user, role as a
      plain `User` field)
- [x] `contracts/auth.v1.yaml` exists and is the source of truth
- [x] Auth MCP server built (becomes the template for `new-module`)
- [ ] All Phase 0 verification items (above) pass using Auth as the guinea pig —
      6 of 9 do; the other 3 are blocked by this sandbox's missing Docker daemon,
      missing `oasdiff`, and a deliberately-not-fired schedule (see each item above)

Implemented (Domain/Infrastructure/Api/Mcp + 35 unit tests + 13 integration tests),
`tpx verify auth` green (~11s). Not yet merged to `main`.

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
