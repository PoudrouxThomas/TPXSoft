# HARNESS_FEEDBACK.md

Audit of the Phase 0 harness. Original pass on 2026-08-23 against `7c8ff4a` on `main`. Rerun the
same day against `6813b84`, after four fix PRs merged (`bbbebef` stderr, `27d52f5` MCP launch,
`54180a3` MCP/verify lock, `40de0f1` contract lint) — every CRITICAL and HIGH finding from the
first pass is now fixed and confirmed live, and has been deleted from this file rather than kept
as a stale warning. What remains below is what is still actually true.

Every item below was executed, not read. Where a claim could not be tested, it says so rather than
guessing.

The repository was left exactly as found each pass: branch `main`, clean working tree, one
worktree (plus four pre-existing `.claude/worktrees/` from the merged fix PRs, untouched), no
leftover containers. The stale-`.ps1`, `tpx gen`, and `CODEOWNERS` findings, plus the CI-coverage
gap, are unchanged from the first pass.

---

## 1. Verification scorecard (PLAN.md § Verification)

| # | Item | Result | Measured evidence |
|---|---|---|---|
| 1 | `docker compose up -d && tpx verify auth` under 60s | **PASS** | 9.9s and 5.3s on rerun (was 11.4s cold / 6.1s warm on the first pass). 35/35 unit tests, exit 0. |
| 2 | `tpx test auth --integration` | **PASS** | 13/13 tests, 19.6s wall / 9s execution. Testcontainers Postgres torn down; no leftovers in `docker ps -a`. |
| 3 | Parallel worktree, simultaneous integration runs | **PASS** | `tpx worktree new auth/verifydemo` allocated `POSTGRES_PORT=5433`, `COMPOSE_PROJECT_NAME=tpxsoft_auth_verifydemo`. Both trees 13/13 concurrently (13.8s main, 22.1s worktree incl. NuGet restore). Two Postgres containers on distinct ephemeral ports, zero conflicts. |
| 4 | Editing `shared/clients/**` blocked by hook | **PASS** | `tpx hook block-generated` exits 2 with a message pointing at `contracts/` for both `clients/` and `generated/` paths; exits 0 for normal paths. |
| 5 | Compile error surfaced by PostToolUse | **PASS (fixed)** | Re-tested with stdout redirected to `/dev/null`: the compiler error still prints, on stderr, before exit 2. First pass measured 0 bytes on stderr — traced to a stale globally-installed `tpx` binary, not a live defect once reinstalled; see "Fixed since first pass" below. |
| 6 | Fresh session answers "what fields does `User` have?" via MCP | **PASS (fixed)** | `.mcp.json` now launches `dotnet exec` on the prebuilt DLL; server responds with all six tools in under 1s (was 8s+ via `dotnet run`, and never connected in-session on the first pass). |
| 7 | `tpx contract lint` fails when a required field is removed | **PASS (fixed)** | Removed `refreshToken` from `TokenPair.required` in `contracts/auth.v1.yaml`: `tpx contract lint` now exits **1** and names all three affected endpoints (`POST /auth/login`, `POST /auth/refresh`, `POST /auth/register`) with `response-property-became-optional`. Reverted; lint green again, `git status` clean. |
| 8 | Background `dotnet-implementer` produces green verify + PR via `gh` | **NOT RUN** | Prerequisites confirmed only: `gh` 2.97.0 authenticated as `PoudrouxThomas`, `origin` set to `github.com/PoudrouxThomas/TPXSoft.git`. |
| 9 | Friday `/schedule` routine runs on demand | **NOT VERIFIABLE LOCALLY** | The routine lives in Claude Code's cloud scheduler; neither the local scheduled-tasks list nor session cron shows it. `GOALS.md` is evidence it ran at least once. |

**Headline:** every item that was previously FAIL or PARTIAL is now PASS. The verify loop is fast
(5–10s), the MCP server connects in under a second, and the contract lint gate genuinely blocks a
breaking change with a specific, correct diagnosis. `GOALS.md` checkboxes that depend on contract
lint are no longer resting on a check that could not fail.

---

## 2. Fixed since the first pass (kept as record, not as open items)

- **`tpx contract lint` returned green on breaking changes** (was CRITICAL). `oasdiff` is now
  installed locally and in CI (`.github/workflows/auth.yml` gained an install step); the lint code
  path itself was also changed so a real diff runs. Verified live: a removed required field now
  fails with `exit 1` and names every affected endpoint.
- **PostToolUse hook blocked without saying why** (was HIGH). `Shell.Capture` now reads the
  build's combined output and `Console.Error.WriteLine`s it before returning 2. Verified live with
  stdout suppressed — the error text still surfaces.
- **MCP server and verify loop fought over the same build output** (was HIGH). `tpx verify auth`
  now builds from `modules/auth/TPXSoft.Auth.verify.slnf`, a solution filter that excludes
  `TPXSoft.Auth.Mcp`. Verified live: ran the MCP server and `tpx verify auth` concurrently, no
  `MSB3027` lock error, verify still finished in 5.3s.
- **`.mcp.json` used `dotnet run` on a relative path and never reliably connected** (was HIGH). It
  now runs `dotnet exec ${CLAUDE_PROJECT_DIR}/.../TPXSoft.Auth.Mcp.dll` against a binary that
  `session-start.sh` prebuilds. Verified live: server answers `tools/list` with all six tools in
  under a second.

One caveat surfaced only by rerunning rather than reading the diff: `dotnet tool update --global`
is a no-op when the package version string hasn't changed, so a machine with an already-installed
`tpx` can silently keep running the pre-fix binary after these merges land. That is not a defect
in the fixes themselves — a clean `dotnet tool uninstall` + reinstall picked up the new build
immediately — but it means "the source is fixed" and "the binary on this machine is fixed" are not
the same claim, and worth remembering before trusting a negative result on this machine again.

---

## 3. Still open

### MEDIUM — `tpx gen` has never generated anything

There is no `nswag.json`, no `ng-openapi-gen.json`, no `shared/` directory, no `angular.json`, no
`package.json`, no `node_modules`. `tpx gen` runs and reports:

```
tpx gen: no nswag.json / ng-openapi-gen.json config found yet under modules/ or shared/clients/angular/ — nothing to regenerate
```

**Impact.** The generated-client pipeline is the mechanism by which modules are allowed to talk to
each other, and it is entirely unproven. The `block-generated` PreToolUse hook — which does work —
currently guards a directory that does not exist. Two agents (`angular-implementer`) and one skill
(`wire-module`) are untestable by construction. This is not yet a live defect, because there is no
second module to wire; it becomes one the moment Documents starts.

**Benefit of fixing.** Proving `tpx gen` end to end on Auth alone, before a consumer exists, means
the Documents module starts against a working pipeline instead of debugging codegen and
cross-module wiring simultaneously. Cheaper to learn one thing at a time.

### MEDIUM — CI verifies less than the local loop

`.github/workflows/auth.yml` now installs `oasdiff` and runs `tpx verify auth`, but it still does
not run `tpx verify boundaries` and does not run `tpx test auth --integration`.

**Impact.** CI is weaker than a local run, so a green PR is a weaker signal than a green terminal.
Boundary violations and integration regressions can still reach `main` unchallenged, even though
contract-breaking changes can no longer sneak through either path.

**Benefit of fixing.** Makes the PR check the real gate. This matters more than it looks: PLAN.md
§ 0.9 dropped the headless `claude -p` PR reviewer on cost grounds, which means the deterministic
CI job is the *only* automated gate on a PR. It should be the strong one.

### LOW — Orphaned PowerShell hook scripts

`.claude/hooks/` still contains `block-generated.ps1`, `verify-on-save.ps1` and `stop-verify.ps1`.
None is wired into `settings.json`; the live implementations are `tpx hook <name>` subcommands in
`tools/tpx/Hooks.cs`.

**Impact.** Low today, guaranteed drift tomorrow. The next person to change hook behavior has two
plausible-looking places to change it and no signal about which is real.

**Benefit of fixing.** One obvious place to change hook behavior. Deleting them is a two-second
edit that removes a future half-hour of confusion.

### LOW — `CODEOWNERS` is effectively empty

32 bytes: `/modules/auth/ @PoudrouxThomas`. PLAN.md § "Decisions taken" argues the monorepo is
realistic because "realism comes from CODEOWNERS + per-package semver + independent deploys, not
from repo walls". Two of the three don't exist yet.

**Impact.** None on correctness. But the monorepo decision is currently unjustified by its own
stated rationale, and the Phase 3 Auth extraction is supposed to teach the multi-repo tax by
contrast — a contrast that only works if the monorepo actually carries the ownership machinery.

**Benefit of fixing.** Makes the Phase 3 comparison meaningful, and exercises path-scoped review
routing, which is itself a technique worth learning.

---

## 4. What is missing for the stated learning goal

PLAN.md is explicit that the product exists to make the harness necessary, and that the goal is
learning AI-assisted development at scale. Judged against that, ranked by learning per unit of
effort. Unchanged from the first pass — none of these depend on the fixes above.

### 1. Build the Documents module

The single largest gap. At one module, half the harness is theory rather than practice:
`find_consumers` has nothing to find, `contract-guardian` has no downstream consumer to name,
`wire-module` has nothing to wire, and asynchronous parallel agents have no independent work to
split. Verification item 7's second clause — "and `contract-guardian` names the downstream
consumers" — is structurally unreachable at n=1.

Cross-module contract breakage is the most instructive thing this design can teach, and it is
currently untestable.

### 2. Measure the token savings

The harness is built on a cost argument — MCP contract queries instead of reading source — that
has never been measured. `rtk gain` covers CLI proxying, and nothing covers the MCP layer. Now
that the MCP server reliably connects, this is actually runnable: ask the same question twice, once
via `describe_entity("User")` and once by letting an agent grep the `.cs` files, and record both
token counts. Until that number exists, the central justification of § 0.7 is faith.

### 3. Evaluate the agents themselves

`claude plugin eval` is early-access-gated on this account, but a hand-rolled equivalent is not: a
fixed set of prompts, run `dotnet-implementer` on each, assert `tpx verify auth` goes green and the
diff touches only expected paths. Right now, editing an agent definition produces no measurable
signal — improvements and regressions look identical.

### 4. Reconsider headless `claude -p`, locally

§ 0.9 dropped the PR-review job because a GitHub runner needs its own paid `ANTHROPIC_API_KEY`.
That reasoning is correct for a cloud runner. It does not apply to a local pre-push hook or a
`tpx review` subcommand shelling out to `claude -p`, which runs on the existing subscription at no
marginal cost. Same headless-mode learning, none of the bill.

### 5. Actually invoke `/loop`

The one Phase 0 technique never exercised. Cheap to try: point it at the remaining endpoints in
`contracts/auth.v1.yaml` and let it iterate until `tpx verify auth` is green.

### 6. `.claude/commands/` does not exist

Skills are built; slash commands are skipped entirely. They have a different cost model — loaded on
invocation rather than scanned every session — and the distinction is worth learning by building
one of each rather than by reading about it.

### 7. Broaden the MCP surface beyond tools

The C# `ModelContextProtocol` SDK also exposes resources and prompts. `get_openapi()` is a natural
*resource*, not a tool call, and is cheaper as one. Choosing between MCP primitives is a real skill
and this module is the right place to practise it.

### 8. Tune permissions

Every session re-prompts for the same read-only Bash calls. The `fewer-permission-prompts` skill
generates the allowlist automatically. Small, but it compounds across every future session.

---

## 5. Suggested order

1. Add `boundaries` + integration tests to `.github/workflows/auth.yml`. *(MEDIUM)*
2. Prove `tpx gen` end to end on Auth before Documents needs it. *(MEDIUM)*
3. Delete the orphaned `.ps1` hooks. *(LOW, trivial)*
4. Fill out `CODEOWNERS` before Phase 3 needs it as contrast. *(LOW)*
5. Then start Documents — against a harness whose green now means green.

Everything CRITICAL/HIGH from the first pass is done; what's left is lower-severity hardening plus
the learning-goal gaps in § 4, of which Documents (§4.1) is by far the highest-value next step.
