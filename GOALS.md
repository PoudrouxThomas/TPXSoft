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
- [x] `gh`, `pnpm`, and `oasdiff` all confirmed on `PATH` in this environment. This
      session's container had neither `dotnet` nor `oasdiff` preinstalled: recovered via
      `apt-get install -y dotnet-sdk-10.0` (only .NET 8/10 are packaged for Ubuntu
      24.04, no 9.0 — the tree targets `net9.0`, so every `tpx`/`dotnet` invocation needs
      `DOTNET_ROLL_FORWARD=Major` in env — corrected here from a prior `LatestMajor` typo
      that disagreed with `.claude/hooks/session-start.sh` and every other mention in this
      file; `Major` is what the hook actually exports) and by fetching the `oasdiff` release
      binary directly from `github.com/oasdiff/oasdiff` (its `/latest/download/` alias
      404s — GitHub redirects aren't followed through this environment's egress proxy —
      but a pinned `/releases/download/vX.Y.Z/...` URL works). Confirms PLAN.md §0.5's
      recorded recipe. `contract lint` runs its real breaking-change diff, not skipping
      it (see Phase 0 completion item 7); re-run this session: both contracts lint clean,
      `tpx verify auth` green in 34.7s, `tpx verify documents` green in 21.2s,
      `tpx verify boundaries` clean.

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
- [x] .NET SDK provisioned automatically in fresh cloud containers — the environment's own
      setup script (claude.ai/code environment settings, outside this repo) now runs
      `apt-get install -y dotnet-sdk-10.0` + `DOTNET_ROLL_FORWARD=Major`, replacing the old
      `dotnet-install.sh` `curl | bash` recipe that silently failed against this
      environment's network policy. Verified end to end in a fresh session with zero manual
      setup: `dotnet --version`, `tpx --help`, and the SessionStart hook's build all work
      from the first turn (PLAN.md §0.5). The manual `apt-get install` workaround is no
      longer needed. Separate, still-open issue found during this verification: the
      `tpxsoft-auth`/`tpxsoft-documents` MCP servers can still show `CONNECTION_CLOSED` in a
      fresh session — a startup-ordering race where Claude Code's MCP client connects before
      SessionStart's build of their DLLs finishes, not a `dotnet`-missing problem (PLAN.md
      §0.5).

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

- [x] `.claude/agents` + `.claude/skills` + hooks bundled into a `tpxsoft` plugin —
      **done, was unchecked in error.** `plugin/marketplace/` exists (`.claude-plugin/marketplace.json`,
      `plugins/tpxsoft/` with `agents/`, `skills/`, `hooks/`, `.claude-plugin/plugin.json`);
      confirmed this session: `claude plugin validate plugin/marketplace/plugins/tpxsoft`
      passes. `claude plugin eval` itself is early-access-gated on this account (not
      usable) — `validate` is the working gate, per PLAN.md §0.9.

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
- [x] 7. `tpx contract lint` fails on a removed required field, `contract-guardian`
      names downstream consumers — **fully confirmed on a throwaway branch**
      (`tmp/contract-lint-test`): removing `role` from `User.required` in
      `contracts/auth.v1.yaml` made `tpx contract lint` fail with
      `response-property-became-optional` on `GET /auth/me`, as expected. Running
      `find_consumers("role")` against that branch then caught a real bug: the tool
      only grepped `shared/clients/**` and `modules/*/src/**`, silently missing
      `apps/**` — so it failed to report that `apps/sharepoint/web/src/app/**` has
      real `User.role` consumers (`auth.service.spec.ts`, `file-explorer.spec.ts`).
      Fixed in `FindConsumers` (both `modules/auth/src/TPXSoft.Auth.Mcp/ContractTools.cs`
      and `modules/documents/src/TPXSoft.Documents.Mcp/ContractTools.cs`) to also walk
      `apps/*/*/src/**`; re-run confirmed both fixture files now surface. `tpx verify
      auth` and `tpx verify documents` both green after the fix.
      **Known gap found in this week's review (not yet fixed):** the walk is
      `apps/*/*/src/**` only, still missing `apps/*/*/e2e/**` — `role: 'Admin'` in
      `apps/sharepoint/web/e2e/tests/register.spec.ts` and
      `apps/sharepoint/web/e2e/tests/support/mock-documents.ts` are real consumers of
      `User.role` that `find_consumers("role")` still won't surface, reproducing the
      same false-negative class this fix addressed for `src/**`, one directory over.
- [x] 8. A background `dotnet-implementer` run on a worktree produces a green
      `tpx verify auth` and a PR via `gh` — **fully confirmed this session**, isolated
      worktree specifically: `tpx worktree new auth/me-created-at` created a real
      worktree (`D:\Dev\AI\TPXSoft.worktrees\auth-me-created-at`, port 5433), a
      background `dotnet-implementer` agent ran entirely inside it (contract edit
      adding `createdAt` to `/auth/me`, `tpx gen`, API + test changes), reported
      `tpx verify auth` green in 7.8s, then pushed the branch and opened
      [PR #20](https://github.com/PoudrouxThomas/TPXSoft/pull/20) itself via `gh pr
      create` — confirmed open with `gh pr view 20` (CI `auth` workflow running).
      Integration tests skipped (no Docker daemon in this environment this run), noted
      by the agent rather than silently omitted.
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
- [x] All Phase 0 verification items (above) pass using Auth as the guinea pig —
      9 of 9 now confirmed; item 8's end-to-end worktree-to-PR `dotnet-implementer` run
      completed this session (see item 8 above, PR #20)

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
      Auth and Documents — **partially started**: `apps/sharepoint/web` exists
      (Angular CLI workspace, `apps/sharepoint/web/src/app`) with a login/register/home
      feature set (`features/auth/login`, `features/auth/register`, `features/home`)
      and an `AuthService`/`authGuard`/`authInterceptor` under `core/auth/`, consuming
      the generated `@tpxsoft/auth-client` (contract-first, not hand-rolled HTTP) —
      confirmed by reading `auth.service.ts`. Documents-module integration is now also
      in: `core/documents/documents.service.ts` plus `features/files/` (`file-explorer`,
      `preview-dialog`, `move-document-dialog`), consuming the generated
      `@tpxsoft/documents-client` — user manually tested the web app end to end and
      confirmed it works. `apps/sharepoint/api` still does not exist (no ASP.NET Core
      host under `apps/sharepoint/api` — ls confirmed this session).
      Port-mismatch/no-env-config blocker from prior session is **fixed** this session:
      added `environments/environment.ts` (prod defaults) and
      `environments/environment.development.ts` (dev), wired via `fileReplacements` in
      `angular.json`'s `development` build configuration; `app.config.ts` now reads
      `environment.authApiUrl`/`environment.documentsApiUrl` instead of hardcoded
      strings. Both dev values point at `5081`/`5082`, matching `docker-compose.yml`'s
      `AUTH_PORT:-5081`/`DOCUMENTS_PORT:-5082` defaults. `npx ng build sharepoint-web
      --configuration development` and `--configuration production` both build clean;
      grepping the dev bundle confirmed `localhost:5081`/`localhost:5082` baked in.
      Separately found (pre-existing, unrelated to this fix, not fixed here):
      `npx ng test sharepoint-web` still fails to compile — `auth.service.spec.ts`'s
      `testUser: User` literal (line 11) is missing the `createdAt` field added to the
      generated `User` interface in PR #20 (`shared/clients/angular/src/models/user.ts`
      declares it required, non-optional). Re-verified this session:
      `file-explorer.spec.ts`'s fixture was fixed since and now includes `createdAt`,
      but `auth.service.spec.ts` was not — still an open follow-up task.

      **Live end-to-end login confirmed working** in a follow-up session: user ran
      `docker compose up -d` and hit a real CORS failure on `OPTIONS /auth/login`. Root
      cause found and fixed — not a code/CORS-config bug (`Auth:Cors:AllowedOrigins` was
      already wired correctly via `docker-compose.yml`'s `Auth__Cors__AllowedOrigins__0`
      env var). Actual cause: root `.env` (gitignored, not in version control) had a
      stale `AUTH_PORT=5080` left over from before the port scheme changed to `5081`,
      overriding `docker-compose.yml`'s own default and disagreeing with
      `.env.example` (which already correctly said `5081`) — so `auth-api` was
      listening on `5080` while the newly-fixed frontend correctly called `5081`, and
      the browser reported the resulting failed/refused preflight as a generic CORS
      error. Fixed by correcting `.env`'s `AUTH_PORT` to `5081` and recreating
      containers (`docker compose up -d --force-recreate`). Verified after the fix:
      `curl` preflight to `/auth/login` returns `204` with the correct
      `Access-Control-Allow-Origin` header, `/auth/register` and `/auth/login` both
      return `201`/`200` with tokens, and a real browser session (navigate to
      `localhost:4200`, fill the login form, submit) successfully logs in and lands on
      the file explorer with no console errors.
- [ ] Auth extracted to its own repository — not started

Remaining before this box can check: build `apps/sharepoint/api`. Port/config and live
login are now confirmed working end to end.

## Phase 4+ — Word, Onenote, Forms, Outlook, Teams

- [ ] Word (Sharepoint save)
- [ ] Onenote (Sharepoint import)
- [ ] Forms (mail report)
- [ ] Outlook
- [ ] Teams (chat/realtime)

Not started — blocked on Phase 3.
