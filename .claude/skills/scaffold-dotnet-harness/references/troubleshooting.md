# Troubleshooting

## `dotnet test` prints nothing and exits 0

It was given a `.slnf`. `dotnet test` accepts a solution filter, matches no test projects,
and reports success — a gate enforcing nothing while looking green. That is why
`package.json > harness.testProjects` names project files. If you add a test project, add
it there; the installer discovers projects whose path matches `tests?` and splits out ones
matching `integration` or `.e2e`.

## Diagnostics come back in French, German, Japanese…

`dotnet` emits in the machine's locale, so a developer on a localised Windows install gets
compiler errors the agent has to translate, and any grep written against error text breaks
per-machine. The loop sets `DOTNET_CLI_UI_LANGUAGE=en` **and** `VSLANG=1033` — the first is
not enough on its own: `dotnet format` and parts of MSBuild read only `VSLANG`. If something
still speaks the local language, it needs the same two variables in its environment.

## A file-lock error that reads like a compile failure

`error MSB3027` / `MSB3021`, "the process cannot access the file … because it is being used
by another process". Something is holding the build output: a running host, a background
worker, an MCP server, or `dotnet watch`. Never put `dotnet watch` in the loop, and take
long-running hosts out of `verify.slnf` — that is what the filter is for.

## Verify is over 60 seconds

Measure per step first (`npm run verify build`, `format`, `test`), then look at the one that
dominates.

- **build** — check nothing re-restores. `dotnet build` restores by default; only `test`
  passes `--no-build --no-restore`, which is fine because build ran first.
- **format** — `dotnet format` loads an MSBuild workspace and costs several seconds no
  matter what. If it dominates on a large solution, narrow `verifySolution` to the projects
  people actually edit and let CI format the rest.
- **test** — if a "unit" test opens a socket, a file or a container, it is an integration
  test. Trait it and move it out.

## Existing repo: hundreds of errors after installing

Expected, and the point. `TreatWarningsAsErrors` and `Nullable=enable` surface defects that
were always there. Work through them in batches with `npm run verify build`. Suppress a
specific analyzer ID in `Directory.Build.props` with a comment when a rule is genuinely
wrong for the repo; do not turn off the two properties themselves, and do not add
`#pragma warning disable` scattered through files — a suppression nobody can find is a rule
nobody enforces.

If a legacy project must be exempt while it is being cleaned up, drop it from
`verify.slnf` and put its name and a date in the commit message. That is visible; a silently
weakened root config is not.

## `NETSDK1013: TargetFramework not recognised`

`Directory.Build.props` sets `TargetFramework` for every project. A project with its own
`<TargetFramework>` overrides it, so this only appears when the props file was copied
without substitution — re-run the installer.

## An architecture rule passes when it obviously should not

NetArchTest skips compiler-generated types, so a violation that exists only inside a lambda
in top-level statements is invisible. Assert on real declarations — a field, parameter or
return type in a named class — when you write a new rule, and prove each new rule fails
before trusting it.

## Migrations

The scaffold ships none, and the integration tests use `EnsureCreatedAsync()` so they run
without one. Once you add the first migration:

```bash
dotnet ef migrations add Initial --project src/<Name>.Infrastructure --startup-project src/<Name>.Api
```

switch `ApiFactory` from `EnsureCreatedAsync()` to `MigrateAsync()`. Leaving it on
`EnsureCreated` means integration tests run against a schema built from the model while
production runs one built from migrations, and the two drift without any test noticing.

`*.Designer.cs` and `*ModelSnapshot.cs` are hook-blocked. To change a migration, change the
model and scaffold again.

## `dotnet restore --locked-mode` fails in CI

A package version changed without the lock file being regenerated. Run `dotnet restore`
locally and commit the updated `packages.lock.json` files. If it fails immediately after
install, a version resolved from NuGet does not exist for this target framework — fix it in
`Directory.Packages.props`; the error names the package.

## Hooks do not fire

They are relative commands (`node tools/harness/hooks/...`), so they only work when the
session's working directory is the repo root. Confirm `.claude/settings.json` is the one in
effect — if the installer found an existing file it wrote `.claude/settings.harness.json`
alongside instead, for you to merge. After merging, re-run `npm run hooks:selftest`; a hook
that has only been read has not been verified.
