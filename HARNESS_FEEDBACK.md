# HARNESS_FEEDBACK.md

Audit of the Phase 0 harness, run on 2026-08-23 against commit `7c8ff4a` on `main`.

Every item below was executed, not read. Verification items 1, 2, 3 and 7 were run by a Sonnet
subagent; items 4, 5, 6 and the environment survey were run on the main thread. Where a claim
could not be tested, it says so rather than guessing.

The repository was left exactly as found: branch `main`, clean working tree, one worktree, no
leftover containers.

---

## 1. Verification scorecard (PLAN.md § Verification)

| # | Item | Result | Measured evidence |
|---|---|---|---|
| 1 | `docker compose up -d && tpx verify auth` under 60s | **PASS** | Cold (bin/obj deleted) **11.4s**, warm **6.1s**. 35/35 unit tests, exit 0. |
| 2 | `tpx test auth --integration` | **PASS** | 13/13 tests, 19.6s wall / 9s execution. Testcontainers Postgres torn down; no leftovers in `docker ps -a`. |
| 3 | Parallel worktree, simultaneous integration runs | **PASS** | `tpx worktree new auth/verifydemo` allocated `POSTGRES_PORT=5433`, `COMPOSE_PROJECT_NAME=tpxsoft_auth_verifydemo`. Both trees 13/13 concurrently (13.8s main, 22.1s worktree incl. NuGet restore). Two Postgres containers on distinct ephemeral ports, zero conflicts. |
| 4 | Editing `shared/clients/**` blocked by hook | **PASS** | `tpx hook block-generated` exits 2 with a message pointing at `contracts/` for both `clients/` and `generated/` paths; exits 0 for normal paths. |
| 5 | Compile error surfaced by PostToolUse | **PARTIAL** | Hook correctly exits 2 on a deliberate C# error, but writes **0 bytes to stderr** — the error text never reaches the agent. |
| 6 | Fresh session answers "what fields does `User` have?" via MCP | **PARTIAL** | The server itself is correct: all six tools returned over stdio (`get_openapi`, `list_endpoints`, `describe_entity`, `find_consumers`, `run_tests`, `get_migrations_status`). But `tpxsoft-auth` never finished connecting in this session, so no `mcp__tpxsoft-auth__*` tool was callable. |
| 7 | `tpx contract lint` fails when a required field is removed | **FAIL** | Removing `refreshToken` from `TokenPair.required` in `contracts/auth.v1.yaml` produced **exit 0**. |
| 8 | Background `dotnet-implementer` produces green verify + PR via `gh` | **NOT RUN** | Prerequisites confirmed only: `gh` 2.97.0 authenticated as `PoudrouxThomas`, `origin` set to `github.com/PoudrouxThomas/TPXSoft.git`. |
| 9 | Friday `/schedule` routine runs on demand | **NOT VERIFIABLE LOCALLY** | The routine lives in Claude Code's cloud scheduler; neither the local scheduled-tasks list nor session cron shows it. `GOALS.md` is evidence it ran at least once. |

**Headline:** the verification loop is genuinely fast and genuinely isolated. 6.1s warm is well
inside the 60s budget, and the worktree port allocator does the job it was built for. Those are
the two hardest things in the plan and they work. What fails is the correctness gate around them.

---

## 2. Findings by severity

Severity is judged by one question: does this let a wrong change reach `main` while reporting green?

---

### CRITICAL — `tpx contract lint` reports green on breaking changes

`oasdiff` is not installed on this machine. [`tools/tpx/Contracts.cs:37`](tools/tpx/Contracts.cs:37)
guards the breaking-change diff with `if (!Shell.Exists("oasdiff"))`, prints one informational
line, and returns success:

```
tpx contract lint: contracts/auth.v1.yaml structurally valid (oasdiff not installed — skipping breaking-change check vs main)
```

Removing a required field from a response schema — the exact scenario PLAN.md item 7 specifies —
passes. The lint that remains checks only that `openapi:`, `info:` and `paths:` keys exist.

`.github/workflows/auth.yml` does not install `oasdiff` either, so CI degrades identically and
also reports green.

**Impact.** Contract-first is the spine of the whole architecture: generated clients, module
boundaries, `contract-guardian`, and the entire cross-module wiring story all assume the contract
is guarded. That guard is currently off, silently, in both local and CI paths. Any agent — or
human — can ship a breaking contract change and see green everywhere. This is also a second-order
problem: `tpx verify <module>` calls contract lint, and the `tpx-goal` skill verifies `GOALS.md`
checkboxes partly by running these commands, so some green checkboxes in `GOALS.md` are unearned.

This is the same failure class as the `pwsh: command not found` bug fixed in § 0.5: a missing
dependency degrades quietly instead of loudly. That pattern has now caused two separate silent
harness failures, which makes it the more important thing to fix than either instance.

**Benefit of fixing.** Restores the only mechanical enforcement of the project's central
architectural rule. Makes green mean something again, which is the precondition for trusting any
agent's self-reported "done". Fixing the *pattern* — never let an absent tool return success —
prevents the next instance of this bug in `tpx gen`, `tpx test`, and anything added later.

**Fix.** Install `oasdiff`; change the guard to fail with a clear message when the binary is
absent rather than skipping; add the install step to `.github/workflows/auth.yml`.

---

### HIGH — PostToolUse hook blocks without saying why

`tpx hook verify-on-save` correctly exits 2 when a touched `.cs` file fails to build. But
`Shell.Run(..., redirect: false)` lets `dotnet build` inherit the hook's stdout, and `dotnet`
writes compile errors to **stdout**. Claude Code reads **stderr** when a hook exits 2. Measured:
exit code 2, stderr length **0 bytes**.

**Impact.** The agent is told "blocked" and given nothing to act on. It cannot self-correct, so it
either retries the same edit or asks the human — which is precisely the review cost the hook was
built to remove. A hook that blocks silently is worse than no hook: it spends a round trip and
delivers no information.

**Benefit of fixing.** This is the highest-leverage single line in the harness. Compile errors
reaching the agent at the exact moment of the edit is the cheapest possible correction point —
cheaper than the Stop hook, far cheaper than human review. Fixing it turns the fast loop from a
gate into a feedback channel.

**Fix.** Capture the build output (`Shell.Capture`) and re-emit it on stderr before returning 2.

---

### HIGH — The MCP server and the verify loop fight over the same build output

`TPXSoft.Auth.Mcp` is a project inside `modules/auth/TPXSoft.Auth.sln`, and `tpx verify auth` runs
`dotnet build` on that solution. `.mcp.json` launches the server with `dotnet run`, so a live MCP
server holds a Windows file lock on `bin/Debug/net9.0/TPXSoft.Auth.Mcp.exe`. Observed independently
by both auditors:

```
error MSB3027: ... Le fichier est verrouillé par : "TPXSoft.Auth.Mcp (22768)"
```

The subagent hit the same class of failure from a stale `TPXSoft.Auth.Api (5740)` left over from a
previous session, and had to kill PIDs 5740 and 22768 before a clean measurement was possible.

**Impact.** The verify loop can fail for a reason that has nothing to do with the code under test.
That is the worst possible property for the one component PLAN.md § "Build tooling rationale"
says "must never be flaky" — it is the stated reason Nx was rejected. An agent that hits MSB3027
has no way to distinguish it from a real compile failure, and will either give up or start
"fixing" working code. It also gets worse with every module added, since every module ships an
MCP server registered in the same `.mcp.json`.

**Benefit of fixing.** Removes the only known source of nondeterminism in the verification loop.
Makes `tpx verify` safe to run concurrently with a live session, which is a hard prerequisite for
the parallel-worktree agent workflow the whole harness is built around.

**Fix.** Either exclude `.Mcp` projects from the `tpx verify` build target, or publish the MCP
server to a separate output directory and point `.mcp.json` at that binary. Optionally add a
stale-lock pre-check to `tpx verify` so the failure at least names itself.

---

### HIGH — `.mcp.json` launches the server in a way that does not reliably connect

```json
"command": "dotnet", "args": ["run", "--project", "modules/auth/src/TPXSoft.Auth.Mcp/TPXSoft.Auth.Mcp.csproj"]
```

Three problems in one line: `dotnet run` performs a build on every session start (slow, and the
source of the lock contention above); the path is relative, so it depends on the client's working
directory; and a build that is slow or fails leaves the client stuck in "connecting". In this
session `tpxsoft-auth` never came up, and no `mcp__tpxsoft-auth__*` tool was available.

The server binary itself is correct — driven directly over stdio it returns all six tools. The
defect is entirely in the launch configuration.

**Impact.** PLAN.md § "Architectural corrections" states plainly that MCP contract queries are
"where the token savings actually come from". If the server does not connect, that saving is zero
and every agent falls back to reading `.cs` source. The harness silently loses its main economic
justification, and nothing reports the loss. Verification item 6 cannot pass while this holds.

**Benefit of fixing.** Makes the dev-time contract layer actually load, which is what makes the
cost model of the whole project work. It also unblocks the measurement suggested in § 3 — you
cannot compare "MCP query vs read the source" until the MCP query runs.

**Fix.** Build once and point `.mcp.json` at the produced executable (or use `dotnet run
--no-build`), with an absolute or `${CLAUDE_PROJECT_DIR}`-anchored path.

---

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

---

### MEDIUM — CI verifies less than the local loop

`.github/workflows/auth.yml` runs `dotnet build tools/tpx` then `tpx verify auth`. It does not run
`tpx verify boundaries`, does not run `tpx test auth --integration`, and does not install
`oasdiff`.

**Impact.** CI is weaker than a local run, so a green PR is a weaker signal than a green terminal.
Boundary violations and integration regressions reach `main` unchallenged. Combined with the
CRITICAL finding, a PR can break the contract, cross a module boundary, and fail integration tests
while showing a green check.

**Benefit of fixing.** Makes the PR check the real gate. This matters more than it looks: PLAN.md
§ 0.9 dropped the headless `claude -p` PR reviewer on cost grounds, which means the deterministic
CI job is the *only* automated gate on a PR. It should be the strong one.

---

### LOW — Orphaned PowerShell hook scripts

`.claude/hooks/` still contains `block-generated.ps1`, `verify-on-save.ps1` and `stop-verify.ps1`.
None is wired into `settings.json`; the live implementations are `tpx hook <name>` subcommands in
`tools/tpx/Hooks.cs`.

**Impact.** Low today, guaranteed drift tomorrow. The next person to change hook behavior has two
plausible-looking places to change it and no signal about which is real. PLAN.md § 0.5 says they
were kept "for the Windows dev machine", but Windows now runs the same `tpx hook` path as Linux,
so the stated reason no longer applies.

**Benefit of fixing.** One obvious place to change hook behavior. Deleting them is a two-second
edit that removes a future half-hour of confusion.

---

### LOW — `CODEOWNERS` is effectively empty

32 bytes. PLAN.md § "Decisions taken" argues the monorepo is realistic because "realism comes from
CODEOWNERS + per-package semver + independent deploys, not from repo walls". None of the three
exists yet.

**Impact.** None on correctness. But the monorepo decision is currently unjustified by its own
stated rationale, and the Phase 3 Auth extraction is supposed to teach the multi-repo tax by
contrast — a contrast that only works if the monorepo actually carries the ownership machinery.

**Benefit of fixing.** Makes the Phase 3 comparison meaningful, and exercises path-scoped review
routing, which is itself a technique worth learning.

---

## 3. What is missing for the stated learning goal

PLAN.md is explicit that the product exists to make the harness necessary, and that the goal is
learning AI-assisted development at scale. Judged against that, ranked by learning per unit of
effort.

### 1. Build the Documents module

The single largest gap. At one module, half the harness is theory rather than practice:
`find_consumers` has nothing to find, `contract-guardian` has no downstream consumer to name,
`wire-module` has nothing to wire, and asynchronous parallel agents have no independent work to
split. Verification item 7's second clause — "and `contract-guardian` names the downstream
consumers" — is structurally unreachable at n=1, which is why this audit could only test the first
clause.

Cross-module contract breakage is the most instructive thing this design can teach, and it is
currently untestable.

### 2. Measure the token savings

The harness is built on a cost argument — MCP contract queries instead of reading source — that
has never been measured. `rtk gain` covers CLI proxying, and nothing covers the MCP layer.

Concretely: ask the same question twice, once via `describe_entity("User")` and once by letting an
agent grep the `.cs` files, and record both token counts. Until that number exists, the central
justification of § 0.7 is faith. It would also immediately have caught the "MCP server never
connected" defect above.

### 3. Evaluate the agents themselves

`claude plugin eval` is early-access-gated on this account, but a hand-rolled equivalent is not: a
fixed set of prompts, run `dotnet-implementer` on each, assert `tpx verify auth` goes green and the
diff touches only expected paths. Right now, editing an agent definition produces no measurable
signal — improvements and regressions look identical. Building the eval harness is itself one of
the more transferable skills in this space.

### 4. Reconsider headless `claude -p`, locally

§ 0.9 dropped the PR-review job because a GitHub runner needs its own paid `ANTHROPIC_API_KEY`.
That reasoning is correct for a cloud runner. It does not apply to a local pre-push hook or a
`tpx review` subcommand shelling out to `claude -p`, which runs on the existing subscription at no
marginal cost. Same headless-mode learning, none of the bill.

### 5. Actually invoke `/loop`

The one Phase 0 technique never exercised. § 0.8 notes it is "usable but not yet actually invoked".
Cheap to try: point it at the remaining endpoints in `contracts/auth.v1.yaml` and let it iterate
until `tpx verify auth` is green.

### 6. `.claude/commands/` does not exist

Skills are built; slash commands are skipped entirely. They have a different cost model — loaded on
invocation rather than scanned every session — and the distinction between the two is worth
learning by building one of each rather than by reading about it.

### 7. Broaden the MCP surface beyond tools

The C# `ModelContextProtocol` SDK also exposes resources and prompts. `get_openapi()` is a natural
*resource*, not a tool call, and is cheaper as one. Choosing between MCP primitives is a real skill
and this module is the right place to practise it.

### 8. Tune permissions

Every session re-prompts for the same read-only Bash calls. The `fewer-permission-prompts` skill
generates the allowlist automatically. Small, but it compounds across every future session.

---

## 4. Suggested order

1. Install `oasdiff`; make its absence a hard failure; add it to CI. *(CRITICAL)*
2. Re-emit build output on stderr in `verify-on-save`. *(HIGH, one-line class of fix)*
3. Fix `.mcp.json` to launch a prebuilt binary at an anchored path. *(HIGH)*
4. Separate MCP build output from the verify target. *(HIGH — 3 and 4 are one change)*
5. Add `boundaries` + integration + `oasdiff` to `.github/workflows/auth.yml`. *(MEDIUM)*
6. Delete the orphaned `.ps1` hooks. *(LOW, trivial)*
7. Re-run this scorecard and correct `GOALS.md`, which currently trusts a lint that cannot fail.
8. Then start Documents — against a harness that no longer lies.

Items 1–4 are what make green mean green. Everything after that is worth more once it does.
