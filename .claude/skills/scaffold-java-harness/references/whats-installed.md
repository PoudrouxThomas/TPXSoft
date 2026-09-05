# What the installer writes, and how to tune it

Ordered roughly by how often you will touch it.

## `dev` (and `dev.cmd`)

The single entry point. A shell script on purpose: a compiled CLI quietly grows into hundreds
of untested lines that gate every other line in the repo.

| command | what it runs |
|---|---|
| `./dev verify` | format check, checkstyle, compile (warnings as errors), unit + architecture tests |
| `./dev verify format` (or `lint`, `compile`, `test`) | one step, while working through findings |
| `./dev format` | rewrites sources with the formatter |
| `./dev compile` | compile only -- what the save hook calls |
| `./dev test <pattern>` | filtered unit tests |
| `./dev verify-it` | integration tests, Testcontainers, never in the loop |
| `./dev openapi` | rewrites `docs/openapi.json` from the running app |
| `./dev run` | starts the app |

`verify` is deliberately **one** build-tool invocation: `mvn spotless:check checkstyle:check test`
(or the Gradle task equivalent). Maven pays full JVM startup on every run and has no build cache,
so four invocations would spend a large slice of the 60s budget on startup alone. `dev` uses
**mvnd** when it is installed -- a drop-in `mvn` that keeps a warm JVM -- and `./mvnw` otherwise.

Measured on the scaffold as installed (JDK 17, single module): first run **17s**, repeat run
**10s**. The Gradle variant is 2-14s for the same checks, because it caches task outputs.

Output rules live in `run_step`: silence on success, and on failure the slice starting at the
first line that looks like a real error, capped at 40 lines. If you change one thing here, keep
that cap -- verify output lands in the model context on every single run.

`dev.cmd` shells out to Git Bash so hooks work on Windows.

## `tools/harness/HookGuard.java`

All three hooks in one file, run from source with `java tools/harness/HookGuard.java <mode>`.
No build step, no Node, no jq -- the JDK is the one runtime a Java repo certainly has.

- `block-generated` (PreToolUse, `Edit|Write|MultiEdit` **and** `Bash`) -- refuses writes to
  anything in `tools/harness/protected-paths.txt` or any `generated/` path segment. Bash is
  deny-by-default: a command mentioning a protected path passes only when its head is a known
  read-only tool and it contains no redirect.
- `verify-on-save` (PostToolUse) -- compiles after a `.java` edit. Exit 2 with the first errors.
- `stop-verify` (Stop) -- runs `./dev verify`; **exit 2** on red. Exit 1 would be a warning the
  agent never sees, which is how a gate ends up enforcing nothing while looking fine.

To protect another path, add a line to `protected-paths.txt`. To let a tool through, add its
name to `READ_ONLY` -- and re-run the self-test afterwards.

## `tools/harness/HookSelfTest.java`

15 cases driving the real hook over stdin: `sed -i`, a heredoc, a python one-liner, `rm -rf`,
`cp`, `mv`, `tee`, an echo redirect, a chained command, plus the reads that must stay allowed.
Run it after any change to the guard. A hook that has only been read has not been verified.

## Build file (`pom.xml`, or `build.gradle.kts` on the Gradle variant)

- **Spotless + google-java-format** -- layout is never a review topic again.
- **Error Prone + NullAway** -- real bugs at compile time, no test needed. NullAway makes the
  main source set null-checked; it is disabled for tests, which are allowed to be blunt.
- **Checkstyle** -- only the rules a formatter cannot express (see `config/checkstyle/`).
- **`-Xlint:all -Werror`** -- this one matters more than it looks: it ends "I will clean that up
  later", which agents say and never do.
- Unit tests exclude the `it` tag (surefire `excludedGroups`); integration tests belong to
  failsafe and `**/*IT.java`. Surefire stops after the first failing test so the output stays
  short.

Two Maven-specific settings that look optional and are not:

- **`<fork>true</fork>` plus the `-J--add-exports` args.** In-process, maven-compiler-plugin
  cannot render an Error Prone diagnostic and reports every finding as *"An unknown compilation
  problem occurred"* -- a real bug with its message discarded. Forked, you get the file, line and
  check name. The `-J` flags are what the forked javac needs on JDK 16+.
- **`.mvn/jvm.config`** carries the same exports for the Maven JVM itself, which is where
  google-java-format runs.

The Gradle variant needs neither: its Error Prone plugin and `gradle.properties` handle both.

## Tests

- `ArchitectureTest` -- ArchUnit rules as plain JUnit, so verify picks them up with no extra
  tooling. Highest value per line in the whole harness: these are the rules an agent breaks
  most often and a diff review notices least.
- `ItemServiceTest`, `ItemControllerTest` -- the two shapes worth copying: a pure unit test and
  a `@WebMvcTest` slice.
- `IntegrationTestBase` -- tagged `it`, holds the container. Everything slow inherits from it.
- `OpenApiDocIT` -- fails when `docs/openapi.json` no longer matches the code.

## `.claude/`

`settings.json` wires the three hooks and carries a permission allow list (the read-only
commands you run constantly -- every prompt is a round trip) and a deny list for build output.
`agents/java-investigator.md` is a read-only "where is X" agent: it burns its own context and
returns twenty lines to yours, which on a limited quota is the difference between one task per
session and four.

`launch.json` lets an agent start the app itself and open it in a browser pane.

## `CLAUDE.md`

Under 2 KB, loaded every session. The verify command, the architecture rule in a sentence, the
paths that must never be hand-edited, the definition of done. Nothing else -- every stale line
is a wrong assumption paid for in every future session.

## CI

`.github/workflows/verify.yml` runs `./dev verify`, then `./dev verify-it`, then publishes
`docs/openapi.json` as an artifact. Nothing in CI checks something local verify does not: the
moment it does, a failure cannot be reproduced locally and everyone burns time guessing.

`dependabot.yml` covers dependency updates without a scanner to install or a key to rotate.

## Small things that are deliberate

- ArchUnit rule fields are `UPPER_SNAKE_CASE`: Checkstyle runs over test sources too, and
  `static final` fields are constants as far as it is concerned.
- `./dev verify` prints Checkstyle violations as `file:line [Rule] message`. The build tool
  itself only prints a path to an HTML report, which is worth nothing to a hook or a CI log.
- The logger is `LOG`, not `log`, for the same reason.
- NullAway excludes JPA and injected fields (`NullAway:ExcludedFieldAnnotations`) -- the
  framework fills them in after construction, so without that it flags every entity.
