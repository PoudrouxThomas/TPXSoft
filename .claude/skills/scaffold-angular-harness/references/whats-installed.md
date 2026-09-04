# What the installer puts in the repo

Read this when you need to explain, tune, or defend a specific piece.

## `npm run verify` — `tools/harness/verify.mjs`

Runs, stopping at the first failure: `prettier --check` → `eslint --max-warnings 0` →
type check → unit tests. On success it prints one line with per-step timings. On failure
it prints the failing step's output capped at 40 lines (head for the fast checks, tail for
tests, where the summary is at the end).

Two decisions worth keeping:

- **`ng build` is not in it.** A production build is a release step. `ngc --noEmit` catches
  what the inner loop needs in a fraction of the time, and CI still runs the real build.
- **Everything calls this script, never `ng` / `eslint` / `vitest` directly.** That
  indirection is what lets you swap Vitest for Jest, or add a step, without touching
  hooks, CI, agent definitions and muscle memory.

`npm run verify types` (or `lint`, `format`, `test`) runs one step — useful while working
through a backlog of findings.

`package.json > harness.testCommand` overrides the test command, e.g. for Jest or for a
Karma workspace that has not migrated yet.

## Type checking — `tools/harness/typecheck.mjs`

Uses `ngc` (from `@angular/compiler-cli`) rather than `tsc`, because **plain `tsc` does not
look inside templates**. With `strictTemplates` on, `ngc` catches a template type error in
about the same time as `tsc` and turns a blank page at runtime into a compile error.

It discovers tsconfigs from `angular.json` (every project's build and test target), so it
works unchanged in a multi-project workspace. `--for <file>` narrows to the owning project,
which is how the PostToolUse hook stays fast. Errors that appear under both the app and
spec tsconfig are printed once.

## Hooks — `.claude/settings.json` + `tools/harness/hooks/`

| Event | What it does |
|---|---|
| PreToolUse `Edit\|Write\|MultiEdit` | blocks writes to `harness.protectedPaths` |
| PreToolUse `Bash` | same, deny-by-default: a Bash command touching a protected path is blocked unless every segment is a known read-only command |
| PostToolUse `Edit\|Write\|MultiEdit` | type-checks the owning project after a `.ts`/`.html` edit |
| Stop | runs `npm run verify`; **exit 2** on failure |

Exit 2 matters. Exit 1 is a warning the agent never sees, so a red verify would quietly
become "task complete". The Stop hook honours `stop_hook_active` so it cannot loop.

Matching Bash as well as Edit is the difference between a guard and the appearance of one.
`tools/harness/hooks-selftest.mjs` proves it, including `sed -i`, heredocs and `rm -rf`.

## Import boundaries — `eslint.config.mjs`

Angular has no ArchUnit; lint rules are the executable form of the layering decision.

```
core/      singletons, guards, interceptors     -> may use core, shared, api
shared/    dumb reusable components             -> may use shared, api  (never a feature)
features/<name>/                                -> may use core, shared, api, and itself
<generated>/                                    -> imported by everyone, edited by no one
```

Element patterns match **folders, outermost first**, so the layer folders must not nest
inside one another — that is why the generated client sits beside `core/` rather than
inside it. Files directly under `src/app` (`main.ts`, `app.ts`, `app.config.ts`) belong to
no layer and are unconstrained.

The rule only works if imports actually resolve; `import/resolver` is configured for `.ts`.
A boundary rule that silently passes is worse than none, so verify it fires once (Step 4 of
SKILL.md).

## `tsconfig.json`

`strict`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`,
`noFallthroughCasesInSwitch`, `noUnusedLocals`, plus `strictTemplates`,
`strictInjectionParameters`, `strictInputAccessModifiers`. This is the frontend equivalent
of `-Werror`, and `strictTemplates` is the single highest-leverage line in the file.

The installer rewrites this file as JSON, which drops the comment header Angular ships.

## Permissions and token hygiene

`.claude/settings.json` denies reads of `node_modules/`, `.angular/`, `dist/`,
`package-lock.json` and the generated client — a single accidental lock file read can cost
more than a whole task — and allows the read-only commands you run constantly, so they stop
prompting.

Worth telling the user, since no config can enforce it: one task per session, `/clear`
between tasks, and remember that "read this component" means up to four files.

## e2e and CI

Playwright, headless, `npm run e2e`, **never in the Stop hook** — CI and on demand only.
The bundled spec is one journey (the app boots with no console errors); add login, the main
create flow, and one error path. Not one spec per component.

CI runs `npm run verify`, then `npm run build`, then e2e. `npm ci`, never `npm install`.
Nothing in CI checks something local verify does not — otherwise "green locally" stops
meaning anything.
