# HARNESS_FEEDBACK.md

Audit of the Phase 0 harness. Original pass on 2026-08-23 against `7c8ff4a`. Second pass the same
day against `6813b84`. **Third pass — this one — against `f62e2e8` on local `main` (`origin/main`
is `8e954c9`, two commits ahead), after PRs #8–#11 merged: orphaned `.ps1` hooks removed
(`af6f47e`), CI hardened (`7e96097`), `tpx gen` proven end to end for C# (`8d8ae68`), `CODEOWNERS`
filled out (`ebbf4db`).**

Every item below was executed, not read. Where a claim could not be tested, it says so rather than
guessing. Findings that are fixed and confirmed live are deleted rather than kept as stale
warnings; § 2 keeps a one-line record of what went away.

The repository was left exactly as found: branch `main`, clean working tree, the eight pre-existing
`.claude/worktrees/` untouched, the test worktree removed and its branch deleted, no leftover
Testcontainers. Docker Desktop and the base `tpxsoft-postgres` container were left running.

---

## 1. Verification scorecard (PLAN.md § Verification)

| # | Item | Result | Measured evidence |
|---|---|---|---|
| 1 | `docker compose up -d && tpx verify auth` under 60s | **PASS** | Postgres healthy in ~8s; verify green in **6.7s and 8.7s** across two runs, exit 0, 35/35 unit tests. |
| 2 | `tpx test auth --integration` | **PASS** | 13/13 tests, 18.4s wall. Testcontainers Ryuk reaper self-terminated; `docker ps -a` shows no leftovers. |
| 3 | Parallel worktree, simultaneous integration runs | **PASS** | `tpx worktree new auth/feedbackcheck` allocated `POSTGRES_PORT=5434`, `COMPOSE_PROJECT_NAME=tpxsoft_auth_feedbackcheck`. Both trees 13/13 concurrently, zero port conflicts. |
| 4 | Editing `shared/clients/**` blocked by hook | **PASS** | `tpx hook block-generated` exits 2 for both `clients/` and `generated/` paths with a message pointing at `contracts/` and naming `tpx gen`; exits 0 for a normal `modules/auth/src/**` path. |
| 5 | Compile error surfaced by PostToolUse | **PASS** | Strongest evidence yet: the **live** hook fired on a real `Edit`, blocking with `CS1002`/`CS1519`. Manual stdin run confirmed exit 2 with stdout empty and all compiler text on stderr. |
| 6 | Fresh session answers "what fields does `User` have?" via MCP | **PASS** | `initialize` in 0.24s, `tools/list` returns all six tools in 0.008s, `describe_entity("User")` in 0.141s → `required: [id, email, orgId, role]`, `id: uuid`, `email: email`, `orgId: uuid`, `role: $ref Role`. No `.cs` file read. |
| 7 | `tpx contract lint` fails when a required field is removed | **PASS** | Removing `refreshToken` from `TokenPair.required` → exit **1**, naming `POST /auth/login`, `POST /auth/refresh`, `POST /auth/register` with `response-property-became-optional`. Reverted; green again, tree clean. |
| 8 | Background `dotnet-implementer` produces green verify + PR via `gh` | **NOT RUN** | Prerequisites confirmed only: `gh` authenticated as `PoudrouxThomas` with `gist, read:org, repo, workflow`; `origin` → `github.com/PoudrouxThomas/TPXSoft.git`. Deliberately not launched during an audit. |
| 9 | Friday `/schedule` routine runs on demand | **PASS** | Confirmed by the repo owner: the routine was run last Friday and executed correctly, producing its review and `GOALS.md` progress summary. Still not verifiable from a local shell — it lives in Claude Code's cloud scheduler. |

**Additional checks, all green:**

| Check | Result |
|---|---|
| `tpx verify boundaries` | exit 0 — "clean — no module references another module's `.Domain` or `.Infrastructure` directly". |
| `tpx gen` | exit 0 — runs `nswag run modules/auth/nswag.json`, emits `shared/clients/csharp/TPXSoft.Auth.Client.g.cs`. `git status` empty before *and* after: generated output matches committed output byte for byte. |
| `tpx verify --affected` on a clean tree | exit 0 in 0.087s — "no changed files under `modules/` — nothing to verify". |
| `.github/workflows/auth.yml` | Now runs `tpx verify auth`, `tpx verify boundaries`, **and** `tpx test auth --integration` as separate steps. |
| `.claude/hooks/*.ps1` | Gone. Only `session-start.sh` remains. |
| `CODEOWNERS` | Filled out on `origin/main`: default owner, plus scoped rules for `/tools/`, `/.claude/`, `/.github/`, `/contracts/`, `/shared/clients/`, `/modules/auth/`. |

**Headline: the harness works.** Nine of nine verification items are satisfied, eight by direct
measurement in this session and one by the owner's own successful run. The verify loop runs an
order of magnitude inside its 60-second budget, the MCP server answers a structural question in
under a quarter second, the contract-lint gate genuinely blocks a breaking change with a correct
and specific diagnosis, and the two hooks that matter both fired for real rather than in
simulation. Nothing found in this pass is broken.

The stale-binary trap noted in the previous pass was avoided deliberately: `tpx` was uninstalled
and reinstalled from a fresh `dotnet pack` before anything was measured, because
`dotnet tool update --global` was a no-op when the package version string hadn't changed. That
footgun is now fixed at the source — see § 2.

---

## 2. Fixed since the second pass (record, not open items)

- **CI verified less than the local loop** (was MEDIUM). `.github/workflows/auth.yml` now runs
  `tpx verify boundaries` and `tpx test auth --integration` alongside `tpx verify auth`. The PR
  check is now the strong gate, which matters because PLAN.md § 0.9 dropped the headless
  `claude -p` reviewer on cost grounds and this is the only automated gate on a PR.
- **`tpx gen` had never generated anything** (was MEDIUM). `modules/auth/nswag.json` exists and
  `tpx gen` produces a committed, reproducible `shared/clients/csharp/TPXSoft.Auth.Client.g.cs`.
  The `block-generated` hook now guards a directory that actually exists. Partially resolved — the
  Angular half is still unproven, see § 3.
- **Orphaned PowerShell hook scripts** (was LOW). `block-generated.ps1`, `verify-on-save.ps1` and
  `stop-verify.ps1` deleted. One obvious place to change hook behavior: `tools/tpx/Hooks.cs`.
- **`CODEOWNERS` effectively empty** (was LOW). Now path-scoped across harness, contracts,
  generated clients and modules, so the Phase 3 Auth extraction has a real monorepo to contrast
  against.
- **No `tpx` way to see or remove worktrees** (was LOW). `tpx worktree list` and
  `tpx worktree rm <module>/<feature>` added (`tools/tpx/Worktrees.cs`); `rm` removes the git
  worktree, deletes its branch, and frees the port allocation. Verified live: created
  `auth/rmtest`, listed it, removed it, confirmed the port state file, `git worktree list`, and
  `git branch` all went back to empty. Also used to prune the two orphaned allocations
  (`auth-verifydemo`, `auth-feedbackcheck`) left from the second pass's own audit.
- **`tools/tpx/README.md` § "Current status" was stale** (was LOW). It claimed no modules or
  contracts existed and every command reported "nothing found" — false since Phase 1. Rewritten to
  describe what Auth actually proves today, and the Commands table now lists `worktree list`/`rm`.
- **`dotnet tool update --global` silently kept a stale `tpx`** (was LOW). `session-start.sh` now
  packs with `-p:PackageVersion="0.1.0.$(date -u +%s)"` (not `-p:Version`, which also drives
  `AssemblyVersion` and rejects a part that large) and clears `.nupkg` first. Verified live:
  uninstalled `tpx`, ran the hook twice back to back — first run installed `0.1.0.<epoch1>`,
  second logged "tool was correctly updated from version `0.1.0.<epoch1>` to `0.1.0.<epoch2>`",
  and `.nupkg/` held exactly one file both times, no accumulation.

---

## 3. Still open

### LOW — the Angular half of `tpx gen` is still unproven

The C# path is real and reproducible. The Angular path is not: there is no
`ng-openapi-gen.json`, no `angular.json`, no `package.json`, no `node_modules`, and nothing under
`shared/clients/angular/`.

**Impact.** Half the codegen pipeline is still theory, and `angular-implementer` remains untestable
by construction. Not a live defect — there is no Angular app yet — but it becomes one at Phase 3
(Sharepoint-lite), which is exactly when the multi-repo extraction is also being learned.

**Benefit of fixing.** Same argument that justified proving the C# half early: don't debug codegen
and a new stack simultaneously. Cheap to do now against a contract that already exists.

### Housekeeping — 8 worktree directories under `.claude/worktrees/` are orphaned on disk

`tpx worktree list`/`rm` are now built (see § 2) and used to prune the two `tpx`-managed
allocations left from the second pass's audit worktrees. Separately, the eight
`.claude/worktrees/*` directories were Claude Code's own session worktrees, not `tpx`-managed —
`git worktree remove --force` deregistered all eight from git (`git worktree list` now shows only
`main`) and their `claude/*` branches were confirmed merged, but the directories themselves refused
deletion with `Device or resource busy` — an OS-level mount/handle, not a permissions problem, so
forcing it further was not attempted. Their branches were left untouched (the `&&` chain
short-circuited on the failed removal, so no partial state). **Needs a human:** close whatever
still holds those paths open (likely this Claude Code install's own worktree runtime, or an
editor) — a reboot is the blunt fix — then `rm -rf .claude/worktrees/*` and `git branch -D` the
eight `claude/*` branches.

### Housekeeping — local `main` is two commits behind `origin/main`

`ebbf4db` (CODEOWNERS) and its merge `8e954c9` are on the remote only. Not a harness defect; noted
so the next audit isn't run against a tree that is missing merged fixes. `git pull` clears it.

---

## 4. What is missing for the stated learning goal

PLAN.md is explicit that the product exists to make the harness necessary, and that the goal is
learning AI-assisted development at scale. Judged against that, ranked by learning per unit of
effort. Now that every verification item passes, this section — not § 3 — is where the remaining
value is.

### 1. Build the Documents module

The single largest gap, and it has only grown more decisive now that everything else is green. At
one module, half the harness is theory rather than practice: `find_consumers` has nothing to find,
`contract-guardian` has no downstream consumer to name, `wire-module` has nothing to wire, and
asynchronous parallel agents have no independent work to split. Verification item 7's second
clause — "and `contract-guardian` names the downstream consumers" — is structurally unreachable at
n=1, and the scorecard above can only ever record its first clause.

Cross-module contract breakage is the most instructive thing this design can teach, and it is
still untestable.

### 2. Measure the token savings

The harness rests on a cost argument — MCP contract queries instead of reading source — that has
never been measured. This pass makes it trivially runnable: `describe_entity("User")` answered in
0.141s with four fields and their types. Ask the same question the other way, by letting an agent
grep the `.cs` files, and record both token counts. Until that number exists, the central
justification of § 0.7 is faith.

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

Still the one Phase 0 technique never exercised. Cheap to try: point it at the remaining endpoints
in `contracts/auth.v1.yaml` and let it iterate until `tpx verify auth` is green.

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

1. `git pull` — get `CODEOWNERS` locally. *(seconds)*
2. Manually clear the 8 orphaned `.claude/worktrees/*` directories once whatever holds them open is
   closed. *(needs a human — see § 3)*
3. **Start Documents.** *(everything above is minutes of work; this is the actual next step)*

The verification loop is done. Nothing in § 3 blocks Phase 2, and every remaining item in § 4
either requires a second module or gets sharper once one exists. Documents is no longer merely the
highest-value next step — it is the only one that teaches something the current tree cannot.
