# Java API — agentic harness checklist

A build order for a strong agent harness on a **single Java backend**, in a company setting:
fully local, no cloud runners, no scheduled routines, limited token quota.

Distilled from building and auditing a larger multi-module, contract-first harness: what actually
paid off there, what turned out to be decoration. Scaled down deliberately.

---

## The one rule

**The verification loop is the constraint. Build it first, keep it fast, keep it quiet.**

An agent that can check its own work is worth several times an agent that cannot. Everything
below — hooks, subagents, skills, CI — is scaffolding around that loop. If the loop is slow,
agents stop verifying and start guessing, and the harness is worse than nothing because it
looks like it works.

Two hard numbers to hold yourself to:

- **Under 60 seconds** for `./dev verify`. If it exceeds that, fix the loop before writing
  any feature code.
- **Under 15 lines of output** on success. Verify output lands in the model's context on
  every single run; it is the largest silent token leak in a harness.

---

## Build order

Do these in order. The ordering is the advice — most of these items are individually obvious,
and most teams still build them in the wrong sequence.

### 1. `./dev verify` — fast, quiet, one command

- [ ] Write a single entry point: a shell script `./dev`, or Gradle tasks. Not a compiled CLI.
- [ ] `./dev verify` runs: compile → unit tests → format check → architecture tests.
- [ ] Every downstream consumer (hooks, CI, agents, humans) calls **this command only**, never
      `mvn`/`gradle` directly. That is what makes the build tool swappable later.
- [ ] Suppress build-tool noise: `gradle -q --console=plain`, or `mvn -q -o`.
- [ ] On success print one summary line plus timing. On failure print the **first** failing
      test's message and stop — not the full reactor output, not stack traces for 40 tests.
- [ ] Measure it. Write the number down.

**Gradle over Maven if the choice is open.** Incremental compilation and the build cache are
worth more to this loop than any other harness feature. If you are on Maven: `-o` (offline),
`-T 1C`, `-q`, and make sure the inner loop never touches the network.

### 2. Determinism — remove taste from the loop

Anything a tool can decide should never consume model attention or tokens.

- [ ] **Spotless** + google-java-format (or palantir-java-format). `spotlessApply` inside
      `./dev format`, `spotlessCheck` inside `./dev verify`.
- [ ] **Error Prone** + **NullAway** — catches real bugs at compile time, no test needed.
- [ ] **Checkstyle** for the rules the formatter can't express.
- [ ] **Warnings as errors** (`-Werror`, `-Xlint:all`). This one matters more than it looks:
      it stops "I'll clean that up later," which agents say and never do.

### 3. Architecture rules as tests — ArchUnit

- [ ] Add **ArchUnit** and write 5-10 rules: layer direction, package dependencies, "no
      controller injects a repository", "nothing outside `infrastructure` imports the JDBC types".
- [ ] Rules live in `src/test` as plain JUnit tests, so `./dev verify` picks them up with no
      extra tooling.

This is the highest value-per-line item in the whole Java harness. It is the one rule an agent
is most likely to break, it is invisible in a diff review, and it costs nothing to check.

**Keep it inside `verify`, not beside it.** A rule that runs only in CI is outside the agent's
definition of done, which means it does not exist as far as the agent is concerned.

### 4. `CLAUDE.md` — after the loop exists, under 2 KB

- [ ] Write it now, not first. Before the loop exists you would be describing intent, not fact.
- [ ] Contents: the one verify command, the architecture rule in a sentence, paths that must
      never be hand-edited, and the definition of done. Nothing else.
- [ ] Everything in it must be re-derivable as false. Stale facts here are a per-session tax
      paid in wrong assumptions — this file is loaded on **every** session.
- [ ] Re-read it every few weeks and delete whatever the repo now says better.

### 5. Hooks — the backstop that fires whether the agent cooperates or not

- [ ] **PostToolUse** on `.java` edits → compile just the touched module. Fast feedback, at the
      cheapest possible moment.
- [ ] **Stop** → `./dev verify`. **Must exit 2 on failure.** Exit 1 is a non-blocking warning
      the agent never sees.
- [ ] **PreToolUse** blocking writes to generated sources (`**/generated/**`, generated API
      clients). **Match `Bash` as well as `Edit`/`Write`** — a `sed -i` or a heredoc redirect is
      the same write, and matching only the editing tools leaves the guard trivially bypassable.
- [ ] Whatever detects "what changed" must work **on the main branch**, with an uncommitted
      working tree, and on untracked files.

Every one of these four is easy to ship and hard to notice, because a broken gate reports success
and enforces nothing. They are cheap to get right on day one. Test them by running them, with a
deliberately failing change — a hook that has only been read has not been verified.

### 6. CI — the same command, not a second implementation

- [ ] CI runs `./dev verify`. Literally that command.
- [ ] Nothing in CI checks something local verify does not. If it does, the model cannot
      reproduce the failure locally and burns tokens guessing at it.
- [ ] Pin every external tool CI installs to a release tag. Never `curl | sh` from a moving branch.

### 7. Token hygiene — free, and probably your biggest saving

- [ ] **One task per session, `/clear` between tasks.** A long session where the model re-reads
      its own history costs more than any tool you could install.
- [ ] **Permission allowlist** in `.claude/settings.json` for the read-only commands you run
      constantly. Every prompt is a round trip.
- [ ] Keep `target/`, `build/`, lock files and generated sources out of the agent's reach.
- [ ] Optional: a CLI output compressor (RTK or similar). Cheap, keep it — but fix your own
      verify output first, it is the larger leak by far.
- [ ] **Do not install a token tool whose savings you have not measured on your own repo.**

### 8. One read-only subagent

- [ ] A "where is X / what calls Y" investigator with `Read`, `Grep`, `Glob` only.

A bigger token lever than any CLI compressor, and the item most often left out. The subagent
burns *its own* context doing the search and returns twenty lines to yours. On a limited quota
this is the difference between one task per session and four.

Two agents is plenty for a single repo. Resist a specialist per layer.

### 9. `./dev verify-it` — integration, separate and slow

- [ ] JUnit 5 + **Testcontainers** against a real database.
- [ ] Tagged (`@Tag("it")`, failsafe) so the inner loop never starts a container.
- [ ] Runs in CI and on demand. Never in the Stop hook.

### 10. Measure, once

- [ ] Log tokens and wall-clock time for three real tasks.

Without this number you cannot tell whether any of the above paid for itself. It is the step most
teams skip, which is why the cost argument for most harnesses stays faith rather than fact.

### 11. Only now: skills

- [ ] Write a skill for a workflow you have already performed **manually three times**.
- [ ] Keep it a short procedure, not an essay. Skills cost input tokens on every session.

Encoding a workflow before you have run it is encoding a guess. Skills written ahead of the work
they describe tend to sit unused — and still cost tokens on every session.

---

## Specs: make them executable

Prose specs rot silently, and agents believe them. You end up with documents confidently asserting
things that stopped being true months ago — nobody lied, the code moved. One prose spec per
feature is one rotting claim per feature.

Put the specification where the harness can check it:

| Intent | Executable form |
|---|---|
| API surface | OpenAPI contract (springdoc, or contract-first with openapi-generator) |
| Behavior | Test names — `shouldRejectExpiredToken()` fails when it stops being true |
| Architecture | ArchUnit rules |
| Decisions | Short ADRs in `docs/adr/`, append-only, never edited |

Feature prose in Jira or a wiki is fine for humans. Just never let it be the agent's source of truth.

If you go contract-first (OpenAPI → generated client/server), add the PreToolUse block on
generated paths from step 5. It works, and it is the one place where the discipline pays back
immediately.

---

## Do not build

Scaling down is most of the value here. Each of these earns its place on a large multi-module
codebase, and each is wrong at single-repo, local, quota-limited scale.

| Skip | Why |
|---|---|
| Git worktrees + port allocation | Only pays for parallel agents. With a token quota you will not run parallel agents. |
| Plugin / marketplace packaging | One team, one repo — copy the files. A packaged copy of your agents and skills is a second source of truth, and it drifts within weeks unless CI checks it. |
| Scheduled routines | No cloud. |
| An MCP server per component | One API. There are no cross-module contract questions to answer. |
| A specialized agent per layer | One repo, one language. Two read-only agents is the ceiling. |
| A custom CLI in a compiled language | `./dev` is a shell script. A compiled CLI quietly grows into hundreds of untested lines that gate every other line in the repo. |

### MCP servers for GitLab / Jira — think twice

An MCP server injects **every one of its tool definitions into every session**, used or not. A
GitLab MCP is typically 30-60 tools: thousands of tokens per session, permanently, for something
you touch a few times a day.

`glab` and the Jira CLI behind a two-line skill cost **zero** until called. Reaching for the `gh`
CLI rather than a GitHub MCP is the same trade, and the right one.

MCP earns its place when it replaces **reading source code** — answering a structural question
in one call instead of ten file reads. Wrapping a CLI you could already call is not that.

---

## Done when

- [ ] `./dev verify` is green, under 60s, under 15 lines of output.
- [ ] CI runs that same command and nothing else.
- [ ] A red verify blocks the agent from finishing (Stop hook, exit 2).
- [ ] Generated paths cannot be hand-edited, including through Bash.
- [ ] `CLAUDE.md` is under 2 KB and every statement in it is currently true.
- [ ] You have measured the token cost of three real tasks.

Steps 1-6 are about a week of work. That is a strong harness for a single Java backend, and it
is roughly where the value plateaus for one team on one repo.
