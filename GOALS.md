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
- [x] `tools/tpx` CLI implements ten commands (`verify <module>`, `verify --affected`,
      `verify boundaries`, `test <module> --integration`, `contract lint`, `gen`,
      `worktree new <module>/<feature>`, `worktree list`, `worktree rm <module>/<feature>`,
      `hook <name>`) — builds clean and runs; `contract lint`, `verify boundaries` and
      `verify --affected` each executed and exited 0 with the documented "nothing found"
      output
- [x] `gh`, `pnpm`, and `oasdiff` all confirmed on `PATH` in this environment
      (`gh`, `pnpm`, `oasdiff`, `dotnet --version` → 9.0.306) — checked directly this
      session, not inferred. `contract lint` now runs its real breaking-change diff
      instead of skipping it (see Phase 0 completion item 7).

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
- [x] Verified end-to-end with two worktrees running integration tests
      *simultaneously* — Docker Desktop started this session and confirmed working
      (earlier "daemon can't start" note was sandbox-specific, not universal). Created
      `auth/concurrent-demo-a` (port 5433) and `auth/concurrent-demo-b` (port 5434),
      brought up both Postgres stacks at once (distinct containers/networks/volumes,
      both healthy simultaneously), ran `tpx test auth --integration` in both worktrees
      concurrently: 13/13 pass in each, no collision. Both worktrees and containers
      torn down afterward.

### 0.7 MCP servers

- [x] Auth module MCP server (`get_openapi`, `list_endpoints`, `describe_entity`,
      `find_consumers`, `run_tests`, `get_migrations_status`) — built
      (`modules/auth/src/TPXSoft.Auth.Mcp`) and verified over real stdio JSON-RPC
      (`initialize`, `tools/list`, and a `tools/call` per tool): `list_endpoints`/
      `describe_entity` return data matching `contracts/auth.v1.yaml`, `find_consumers`
      correctly reports zero consumers (Auth is the first module), `run_tests`/
      `get_migrations_status` both shell out successfully
- [x] `.mcp.json` registering module MCP servers — registers both `tpxsoft-auth` and
      `tpxsoft-documents` (stdio, `dotnet exec .../bin/Debug/net9.0/TPXSoft.<Module>.Mcp.dll`)

### 0.8 Loops, schedules, and goal tracking

- [x] Weekly `/schedule` cloud routine exists and runs PR review + contract lint +
      `GOALS.md` update (this routine) — user ran it manually and confirmed it works
      (see Phase 0 completion item 9)
- [x] `tpx-goal` skill built (`.claude/skills/tpx-goal/SKILL.md`) — re-verifies each
      checkbox against real repo state (file/dir existence, actually re-running
      `tpx verify`/`tpx contract lint`/etc., agent/skill/hook presence) rather than
      trusting a prior claim, and reports a progress summary. Invoked on request
      (`/tpx-goal` or asking to check goal status) — not wired as a session-start hook.

### 0.9 Capstone (do after Phase 1)

- [ ] `.claude/agents` + `.claude/skills` + hooks bundled into a `tpxsoft` plugin,
      validated with `claude plugin eval` — blocked: deferred until after Phase 1
- [ ] Headless `claude -p` PR-review job in CI — blocked: no CI workflows exist yet

### 0.10 CLAUDE.md hierarchy (do after Phase 1's first module)

- [x] `modules/auth/CLAUDE.md` — bounded context, entities, endpoints, JWT claim set,
      config keys, deferred decisions, environment notes, verify line, known consumers

### Phase 0 completion (PLAN.md "Verification", all 9 must pass from a clean clone)

Re-run against the real Auth module. Seven of nine pass outright; the other two have
a specific, named blocker rather than being generically "not done" — see each line.

- [x] 1. `docker compose up -d && tpx verify auth` green in under 60s —
      Docker Desktop started this session (earlier "daemon can't start" was
      sandbox-specific, not universal), `docker compose up -d` brought Postgres
      healthy, then `tpx verify auth` green in 6.1s
- [x] 2. `tpx test auth --integration` passes against real Postgres via Testcontainers
      — confirmed this session: 13/13 pass in 7s
      (`modules/auth/tests/TPXSoft.Auth.IntegrationTests`)
- [x] 3. `tpx worktree new auth/demo` works and `tpx verify auth` runs green inside
      the new worktree with its own allocated port — confirmed live. *Simultaneous*
      integration-test runs in two trees confirmed too this session (see 0.6)
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
      names downstream consumers — **`oasdiff` is now installed and `tpx contract lint`
      runs its real breaking-change diff** (confirmed this session: both
      `contracts/auth.v1.yaml` and `contracts/documents.v1.yaml` lint clean with no
      breaking change vs `main`). The tool-missing blocker from earlier sessions no
      longer applies. Still not re-checked: the destructive case itself (actually
      remove a required field on a throwaway branch and confirm the lint fails, and
      that `contract-guardian` names the right downstream consumers) — that needs a
      deliberate branch edit, not done this session.
- [ ] 8. A background `dotnet-implementer` run on a worktree produces a green
      `tpx verify auth` and a PR via `gh` — **partially demonstrated**: `module-architect`,
      `dotnet-implementer`, and `test-writer` all ran as background subagents in an
      earlier session and each produced a green `tpx verify auth`, but none ran inside
      an isolated worktree specifically, and no PR was opened by an agent directly.
      `gh` is confirmed installed now, so that's no longer a blocker — the only gap is
      an actual end-to-end run. Worth doing once there's a natural small follow-up task.
- [x] 9. The Friday `/schedule` routine executes on demand and writes a review +
      `GOALS.md` progress summary — the routine (`trig_01Ab56gX37f3vCqfXA1nqqJD`,
      "TPXSoft Weekly Review", Fridays 16:00 UTC) exists, is enabled; user ran it and
      confirmed it works (not independently re-verified by tooling this session)

## Phase 1 — Auth module

- [x] OIDC-ish JWT issuance, users, orgs, roles implemented under `modules/auth/`
      (minimal scope: org created at registration, one org per user, role as a
      plain `User` field)
- [x] `contracts/auth.v1.yaml` exists and is the source of truth
- [x] Auth MCP server built (becomes the template for `new-module`)
- [ ] All Phase 0 verification items (above) pass using Auth as the guinea pig —
      7 of 9 do; the other 2 are an untested destructive contract-lint case
      (item 7 — `oasdiff` itself is installed and working) and a not-yet-run
      end-to-end worktree-to-PR `dotnet-implementer` run (item 8; see each item above)

Implemented (Domain/Infrastructure/Api/Mcp + 35 unit tests + 13 integration tests),
`tpx verify auth` green (~11s). Not yet merged to `main`.

## Phase 2 — Documents module

- [x] `modules/documents/` implemented: all 7 features from
      [`modules/documents/documentation/`](modules/documents/documentation/) are built and
      committed — upload (01), virtual folders (02), rename/move/delete (03),
      sharing/visibility (04), preview/download (05), update content (06), manage folders
      (07). `tpx verify documents` green in 10.9s: 180/180 unit tests pass, 0 warnings.
      Integration tests (`TPXSoft.Documents.IntegrationTests`) confirmed this session
      against real Postgres via Testcontainers: 82/82 pass in 21s. Documents module has
      its own MCP server too (`tpxsoft-documents`, see 0.7).
- [ ] .NET Aspire introduced for local orchestration — not done; module still runs
      standalone against `docker-compose.yml` Postgres like Auth.
- [ ] Cross-module wiring via generated client, verified by `contract-guardian` — not
      done; Documents has no consumers yet and doesn't call Auth over HTTP (permissions
      are `sub`/`orgId` claims off the same JWT, not a live call to Auth — see
      `modules/documents/CLAUDE.md` "Known assumptions").

Core module substantially done (7/7 features, unit-tested, contract-lint clean);
Aspire orchestration and cross-module wiring are the remaining Phase 2 items.

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
