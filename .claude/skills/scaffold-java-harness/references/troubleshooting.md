# Troubleshooting

## `./dev verify` takes longer than 60 seconds

Measure the steps before changing anything: `./dev verify compile`, then `test`, then `lint`.

- **Tests dominate.** Something in the unit suite is not a unit test -- a `@SpringBootTest`, a
  database, a container. Tag those `@Tag("it")` (or move them behind `IntegrationTestBase`) so
  they run in `./dev verify-it`. One Spring context costs more than every other check combined.
- **Compile dominates.** Check the Gradle daemon is alive (`gradle --status`); a daemon dying
  between runs turns every verify into a cold start. Confirm `org.gradle.caching=true`.
- **First run only.** Dependency resolution and, on a fresh machine, a toolchain download. The
  number to write down is the second run.
- **Maven, every run.** Maven has no daemon and no build cache: it pays JVM startup and
  recompiles each time. Install `mvnd` (`dev` picks it up on its own) before you start removing
  checks. Measured on the fresh scaffold: 17s first run, 10s repeat, JDK 17.
- Once `~/.m2` is warm you can pin the loop off the network with `MAVEN_OFFLINE=1 ./dev verify`.
  It is opt-in because on a fresh machine offline turns the first run into a resolution error.

## Verify is green but CI is red

Something in CI is checking what verify does not. Move that check into `./dev verify` or delete
it -- a failure the model cannot reproduce locally costs more tokens than the check saves.

## `IllegalAccessError` from google-java-format or Error Prone

They reach into javac internals, which is legal only with the `--add-exports` / `--add-opens`
flags. On Maven those live in **two** places and both matter: `.mvn/jvm.config` for the Maven JVM
(google-java-format runs there) and the `-J--add-exports` compiler args in `pom.xml` for the
forked javac (Error Prone runs there). On Gradle they are in `gradle.properties`. If you replaced
either file, put the flags back.

## Maven says "An unknown compilation problem occurred"

That is Error Prone reporting a real finding whose message maven-compiler-plugin threw away,
which happens when javac runs in-process. The bundled pom sets `<fork>true</fork>` precisely to
avoid it; if you removed it, put it back and the file, line and check name come with the error.

A related one: dropping `--should-stop=ifError=FLOW` produces *"The default --should-stop=ifError
policy (INIT) is not supported by Error Prone"*. Both args are load-bearing.

## `-Werror` fails on code you did not write

Deprecation warnings from a library upgrade are the usual cause. Narrow the lint rather than
dropping the flag: `-Xlint:all,-deprecation` keeps every other check. Turning `-Werror` off
entirely brings back "I will clean that up later", which is what it exists to prevent.

## NullAway floods the build with warnings

It only annotates the packages you list. Start with your base package
(`NullAway:AnnotatedPackages`), and add `@Nullable` where the code genuinely returns null rather
than suppressing the check.

## Testcontainers cannot find Docker

`./dev verify-it` needs a running Docker daemon; `./dev verify` never does -- if the inner loop
starts a container, something is tagged wrong. On CI runners without Docker, run only
`./dev verify` and keep integration tests to a job that has it.

## An ArchUnit rule passes but should not

Layered rules pass silently when a layer matches no classes at all -- a renamed package is
enough. Prove each rule fails by breaking it once:

```bash
# a controller reaching straight for a repository must fail the build
printf 'package %s.api;\nimport %s.domain.ItemRepository;\npublic class Bad { ItemRepository r; }\n' \
  <base.package> <base.package> > src/main/java/<path>/api/Bad.java
./dev verify        # must FAIL on controllers_do_not_reach_for_repositories
rm src/main/java/<path>/api/Bad.java
```

A rule you have never seen fail is a rule you do not know is running.

## `mvn spotless:check` cannot resolve the plugin prefix

Prefix resolution only searches `org.apache.maven.plugins` and `org.codehaus.mojo` plus the
plugins declared in your POM. `spotless:check` works because the plugin is declared there; if you
moved it to a profile that is not active, call it by coordinates instead.

## The Stop hook does not block

Run it by hand:

```bash
echo '{}' | java tools/harness/HookGuard.java stop-verify; echo "exit=$?"
```

On a red tree that must print `exit=2`. Exit 1 is a non-blocking warning the agent never sees,
which is exactly the failure that makes a harness look like it works while enforcing nothing.

## The write guard does not block a Bash command

```bash
java tools/harness/HookSelfTest.java
```

All cases must pass. If you added a tool to `READ_ONLY`, make sure it cannot write with a flag
(`tee`, `sed -i`, `python -c`), and re-run the self-test.

## The Stop hook reports red on a perfectly green tree (Windows)

A bare `bash` on the Windows PATH is often the WSL launcher, which cannot see the repository and
fails in a way that looks exactly like a failing build. `HookGuard.bashCandidates()` tries Git
Bash first and skips a candidate whose output mentions WSL. If your shell lives somewhere else,
add its path there -- that list is the one place this is configured.

## Windows

`dev` is a POSIX script; `dev.cmd` runs it through Git Bash, which ships with Git for Windows.
Hooks call `java tools/harness/HookGuard.java`, which picks the right one. If hooks fail with
"bash not found", add Git usr/bin to PATH.

## Spring Boot or plugin version bumps

Versions are pinned deliberately. When bumping Spring Boot, bump springdoc in step (springdoc
2.8.x targets Boot 3.4/3.5); a mismatch usually shows up as an empty `/v3/api-docs`.

## The app starts but every request returns 401

By design: there is no default password. Set `API_PASSWORD` (or `app.security.api-password`),
or read the one-run password from the startup log. Do not add a default credential to
`application.yml` -- that is how a temporary password reaches production.
