# Troubleshooting

## The boundary rule never fires

Almost always import resolution, not the rule. If `eslint-plugin-boundaries` cannot resolve
an import, it has no target element and quietly allows it — a boundary rule that passes on
everything looks identical to one that is working.

Check with the plugin's own diagnostics:

```bash
ESLINT_PLUGIN_BOUNDARIES_DEBUG=1 npx eslint src/app/features/b/b.ts
```

In the dependency description, `to.element.filePath: null` means unresolved. Two causes:

- **Path aliases** (`@app/...`, `@shared/...`): the bundled `node` resolver does not read
  `tsconfig` paths. Install `eslint-import-resolver-typescript` and replace the
  `import/resolver` setting with `{ typescript: { project: './tsconfig.json' } }`. Note it
  pulls a native binary through a postinstall script, which some npm setups block.
- **Wrong element pattern**: `from.element.types` shows what the file was classified as.
  Patterns match folders outermost-first, so a layer folder nested inside another layer
  (a generated client inside `core/`) is swallowed by the outer one. Move it, or list the
  inner element first *and* make the outer pattern not match it.

## Tests are slow, or the loop is over 60 seconds

- **Karma**: a real browser is too slow for the inner loop. Angular 20+ ships the
  Vitest-based `@angular/build:unit-test` builder — migrate the `test` target to it, or
  move to Jest. The installer detects Karma and says so.
- **`ng test` seems to hang**: it is in watch mode. The harness passes `--watch=false`;
  if you changed `harness.testCommand`, keep that flag.
- **First run is slow, later runs fast**: cold Vitest/esbuild start. Measure the warm
  number — that is what the agent experiences.
- Still slow: check that `ng build` has not crept into the loop, and that the type check
  is `--noEmit`.

## Prettier wants to reformat the entire repo

Expected on an existing codebase. Run `npm run format` as its own commit before any
behaviour change, so the formatting diff never hides a real one. If a directory should
never be formatted (vendored code, fixtures), add it to `.prettierignore` rather than
skipping the check.

## `strict` produces hundreds of errors

Fix them in batches, and say plainly how many there are rather than quietly disabling the
flag. Ordering that works: `strictNullChecks` fallout first (it is most of them), then
implicit `any`, then templates. `npm run verify types` gives just that step.

If the backlog is genuinely too large for one pass, the honest compromise is per-file
`// @ts-expect-error` with a TODO — visible, greppable, and it fails once fixed — not a
weaker `tsconfig`, which silently applies to all future code as well.

## An old ESLint setup is already there

The bundled config is flat config (ESLint 9+). If the repo still has `.eslintrc.json`,
migrate it (`npx @eslint/migrate-config .eslintrc.json`) and merge the rules you care about
into `eslint.config.mjs`, rather than running both. The installer will not overwrite an
existing config unless you pass `--force`.

## Multi-project workspace / monorepo

The type check discovers every project in `angular.json`, so it works unchanged. Two things
usually need adjusting by hand:

- `eslint.config.mjs` element patterns assume `src/app/...`. For `apps/<app>/src/app/...`,
  update the four `boundaries/elements` patterns (glob the app segment: `apps/*/src/app/core`).
- `.claude/launch.json` holds one dev server; add a configuration per app you preview.

If several apps must be verified together, keep one `npm run verify` at the root that runs
them in sequence — the single entry point matters more than the granularity.

## Angular older than 16

Flat ESLint config and `ng-openapi-gen` both expect 16+. The verify script, hooks and
typecheck work regardless; the lint config and the client generator may not. Upgrade
Angular first if you can, and if you cannot, keep the harness and drop the boundaries
plugin to `@typescript-eslint/no-restricted-imports` patterns.

## Windows

Everything shipped is Node, not bash, so hooks work in PowerShell, cmd and Git Bash alike.
The one gotcha: `node_modules/.bin` is only on `PATH` inside an npm script, which is why
`verify.mjs` and `typecheck.mjs` add it explicitly. Keep that if you edit them.

## The Stop hook fights the agent

If verify is red for a reason the agent cannot fix (a broken dependency, a half-done
migration), the Stop hook will keep pushing back. Fix the tree or temporarily comment out
the Stop hook — but treat a long-lived red tree as the actual problem, because everything
in this harness assumes a green tree means done.
