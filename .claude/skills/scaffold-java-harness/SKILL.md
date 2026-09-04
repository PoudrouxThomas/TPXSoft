---
name: scaffold-java-harness
description: Build a complete agent harness for a Java backend - one fast quiet `./dev verify` (google-java-format, Checkstyle, Error Prone + NullAway, warnings as errors, unit and ArchUnit architecture tests), Claude hooks that block edits to generated code and gate Stop on a green verify, a springdoc OpenAPI document a frontend generates its client from, Spring Security locked by default, Testcontainers integration tests kept out of the inner loop, CI that runs the same command, and a sub-2KB CLAUDE.md. Use this whenever someone wants a Java, Spring Boot, Maven (or Gradle) repo made agent-ready, safe for agents, "properly set up", or scaffolded from scratch - and also when they ask for any single piece of it: a verify script, ArchUnit layer rules, a Stop hook that blocks on failing tests, Spotless plus Checkstyle wiring, an OpenAPI contract for a frontend to generate from, or a CLAUDE.md for a Java service. Works on a brand-new API and on an existing codebase.
---

# Scaffold a Java agent harness

**The verification loop is the constraint. Build it first, keep it fast, keep it quiet.**

An agent that can check its own work is worth several times one that cannot. Everything else here
— hooks, the read-only investigator, CI — is scaffolding around that loop. If the loop is slow,
agents stop verifying and start guessing, and the harness is then worse than nothing because it
looks like it works.

Two numbers to hold yourself to, both checkable:

- **`./dev verify` under 60 seconds.** Over that, fix the loop before writing feature code.
- **Under 15 lines of output on success.** Verify output lands in the model's context on every
  single run; it is the largest silent token leak in a harness.

A bundled installer does the mechanical work, so the scaffolding itself costs almost no tokens.
Your job is the judgement around it: what kind of repo this is, whether the sample vertical slice
should survive, and getting the tree to green without weakening it.

## Step 1 — establish what you cannot guess

Detect first (`ls pom.xml build.gradle.kts`, `java -version`), then ask only
what is genuinely unknowable. One AskUserQuestion, at most these:

1. **New service or existing repo?** Detectable — an existing build file means harness-only mode.
   Do not ask what you can see.
2. **Base package**, e.g. `com.acme.orders`. Needed for source paths, NullAway and the ArchUnit
   rules. In an existing repo, read it off the tree instead of asking.
3. **Database?** Postgres with Flyway and Testcontainers is the default; `--db none` gives an
   in-memory adapter behind the same port interface.

**Maven is the default build tool**, since that is what most Spring Boot teams already run.
`--build gradle` installs the Gradle variant instead — same checks, same `./dev` commands, and a
faster inner loop (incremental compile plus a build cache Maven has no equivalent of), so it is
worth offering if the repo is new and the team has no preference. Do not switch a Maven team to
Gradle to win a few seconds of verify time.

## Step 2 — run the installer

```bash
java <skill-dir>/assets/Install.java --root . --files <skill-dir>/assets/files \
  --package com.acme.orders --name orders-api --java 21 --port 8080
```

`--files` is required — it is how the installer finds its templates. Other flags:
`--build gradle`, `--db none`, `--db-port 5433`, `--group com.acme`, `--title "Orders API"`,
`--harness-only` (skip the sample application), `--force` (overwrite existing config).

It is idempotent, so a partial install can simply be re-run, and it never overwrites an existing
`CLAUDE.md` or `.claude/settings.json` — it writes `.harness` variants beside them to merge.

| | |
|---|---|
| `dev`, `dev.cmd` | the one entry point: verify, format, compile, test, verify-it, openapi, run |
| `tools/harness/HookGuard.java` | all three hooks, run from source — no Node, no jq |
| `tools/harness/HookSelfTest.java` | 15 cases proving the write guard actually blocks |
| `pom.xml` (or `build.gradle.kts`) | Spotless, Checkstyle, Error Prone + NullAway, `-Werror`, tagged test split |
| `.mvn/jvm.config` | the javac exports Error Prone and google-java-format need on JDK 16+ |
| `src/main/java/...` | a working vertical slice: controller, DTOs, service, domain port, JPA adapter |
| `.../config/SecurityConfig.java` | locked by default, no default password |
| `.../ArchitectureTest.java` | 9 ArchUnit rules — layering, no field injection, java.time only |
| `.../it/*IT.java` | Testcontainers integration tests, tagged `it`, outside the loop |
| `.claude/` | hooks wired, permission allow/deny, a read-only investigator agent, launch config |
| `CLAUDE.md` | under 2 KB |
| `.github/workflows/verify.yml` | CI runs `./dev verify`, then `./dev verify-it` |
| `docs/adr/` | two ADRs: why ADRs, and why the loop looks like this |

On a new project without a wrapper, create one (`mvn -N wrapper:wrapper`, or
`gradle wrapper --gradle-version 8.12` on the Gradle variant). `dev` uses the wrapper when it
exists, prefers `mvnd` when that is installed, and falls back to the system tool otherwise.

`references/whats-installed.md` explains each piece and how to tune it. For an existing codebase,
`references/existing-project.md` has the build-file additions and, more importantly, the order to
land them in.

## Step 3 — get to green, without lowering the bar

```bash
./dev format
./dev verify
```

On a fresh scaffold this is green in well under a minute — measured on the Maven variant as
installed: 17s first run, 10s repeat, JDK 17 (the Gradle variant is 2-14s, since it caches task
outputs). On an existing codebase it will not be green,
and that is the point: `-Werror`, NullAway and Checkstyle are surfacing defects that were
previously invisible. Fix the code.

When a specific rule is genuinely wrong for this repo, disable **that rule** with a comment saying
why. Turning off `-Werror` or dropping the architecture test is different in kind — those are the
rules that make an agent's "I'll clean it up later" impossible, which is exactly the promise that
never gets kept. If the volume of findings is large, say so and offer to work through them in
batches rather than quietly narrowing the config.

Then measure, and write the number down:

```bash
time ./dev verify
```

If Maven drifts past 60s as the codebase grows, install `mvnd` before removing any check —
`dev` picks it up automatically and it removes the per-run JVM startup.

## Step 4 — prove the gates, do not assume them

Every one of these is easy to ship broken and hard to notice, because a broken gate reports
success and enforces nothing.

```bash
java tools/harness/HookSelfTest.java
```

15 cases including `sed -i`, a heredoc, a python one-liner, `tee`, `rm -rf` and a chained command
— the ways a guard that only matches `Edit` and `Write` gets walked straight through. All must
pass.

Then prove the Stop hook actually blocks, since exit 1 is a warning the agent never sees:

```bash
echo '{}' | java tools/harness/HookGuard.java stop-verify; echo "exit=$?"
```

On a green tree that prints `exit=0`. Break something trivially (delete a semicolon) and it must
print `exit=2`. Put the file back.

Last, prove one architecture rule fires — a layered rule whose packages do not match anything
passes silently, which is the worst possible failure here:

```bash
# a controller reaching straight for a repository
printf 'package com.acme.orders.api;\nimport com.acme.orders.domain.ItemRepository;\npublic class Bad { ItemRepository r; }\n' \
  > src/main/java/com/acme/orders/api/Bad.java
./dev verify    # must FAIL on CONTROLLERS_DO_NOT_REACH_FOR_REPOSITORIES
rm src/main/java/com/acme/orders/api/Bad.java
```

## Step 5 — the OpenAPI document

```bash
./dev openapi     # boots the app once, writes docs/openapi.json
```

Commit that file: it is the contract a frontend generates its client from, and a file in git is
the only version another repo can pin. `OpenApiDocIT` fails when the committed copy stops matching
the code, so a contract change nobody regenerated breaks here rather than in someone else's build
a month later.

Check what came out before moving on — an untagged controller or a missing `@Operation` produces
a client with anonymous services and unnamed methods. `references/openapi-client.md` covers the
annotations that matter and how the frontend should generate from it.

## Step 6 — tailor CLAUDE.md, then hand over

The installed `CLAUDE.md` is true for the scaffold. Make it true for **this** repo: the real
layering, the real conventions, the real definition of done. Keep it under 2 KB — it is a tax on
every session forever, and a stale line is worse than a missing one.

```bash
wc -c CLAUDE.md
```

Then tell the user, concretely: the verify time you measured, what verify covers, what CI adds,
which paths are now off-limits and why, and the one thing to do next — **run three real tasks and
note the tokens and wall time**. Without that number nobody can tell whether the harness paid for
itself, which is why the cost argument for most harnesses stays faith rather than fact.

## Deliberately not built

Worth saying out loud so nobody adds them by reflex. Each earns its place on a large multi-module
codebase and each is wrong for one Java service on one team:

- **Git worktrees and port allocation** — only pays for parallel agents, which a token quota
  makes unlikely.
- **An MCP server for this API** — MCP earns its place when it replaces *reading source code*.
  For one repo the agent can already read, it is thousands of tokens of tool definitions per
  session for nothing. The same goes for GitLab/Jira MCPs: `glab` and `jira` behind a two-line
  skill cost zero until called.
- **A specialist agent per layer** — one read-only investigator is the lever; a second agent is
  the ceiling.
- **A compiled CLI instead of `dev`** — it grows into hundreds of untested lines gating every
  other line in the repo.
- **More skills, now** — write one after you have done the workflow manually three times.
  Encoding a workflow you have not run is encoding a guess, and it costs input tokens every
  session either way.

## References

- `references/whats-installed.md` — every generated file, what it does, how to tune it
- `references/existing-project.md` — installing into a codebase with history, in the right order
- `references/openapi-client.md` — the document, the annotations, generating the frontend client
- `references/troubleshooting.md` — verify over 60s, `-Werror` fallout, NullAway noise, JDK 16+
  export flags, silently passing ArchUnit rules, Windows, hooks that do not block
