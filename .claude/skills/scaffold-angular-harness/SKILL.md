---
name: scaffold-angular-harness
description: Build a complete agent harness for an Angular app - one fast quiet `npm run verify` (prettier, eslint, strict type + template check, unit tests), enforced import boundaries between core/shared/features, Claude hooks that block edits to generated API clients and gate Stop on a green verify, a browser verification loop, Playwright e2e, CI, and a sub-2KB CLAUDE.md. Use this whenever someone wants an Angular repo made agent-ready, safe for agents, or "properly set up" - and also when they ask for any piece of it. A verify script, eslint import boundaries, strictTemplates, a generated OpenAPI client that agents must not hand-edit, PostToolUse/Stop hooks for a frontend repo, or a CLAUDE.md for an Angular project. Works on a brand-new app (`ng new` first) and on an existing one.
---

# Scaffold an Angular agent harness

An agent working on a frontend fails in a way a backend agent does not: **a green test
suite does not mean the page renders.** So this harness has two halves — a fast
verification loop the agent runs after every change, and a browser loop that proves the
change actually rendered. Both go in before any feature work, because the speed and
honesty of that loop is the constraint everything else depends on.

Targets, and they are checkable: `npm run verify` **under 60 seconds** and **one line of
output when green**. Verbose webpack/Vitest output lands in the model's context on every
single run, which is why the loop swallows output and prints only the first failing check.

Most of the work here is done by a bundled installer, so the scaffolding costs almost no
tokens. Your job is the judgement around it: what kind of repo this is, whether there is
an OpenAPI backend, and getting the tree to green afterwards.

## Step 1 — establish the two facts you cannot guess

Detect what you can (`ls angular.json`, `cat package.json`), then ask the user only what
is genuinely unknowable. Use one AskUserQuestion with both:

1. **Does a backend serve an OpenAPI document for this app?** If yes, get the URL or file
   path (`http://localhost:5000/swagger/v1/swagger.json`, `../contracts/api.v1.yaml`).
   This decides whether the client is generated and guarded, or whether HTTP calls are
   hand-written. Do not assume "no" because you cannot find a spec in the repo — the
   backend is often a different repository.
2. **The dev server port**, if it is not the default 4200.

If there is no `angular.json`, create the workspace first:

```bash
npx @angular/cli@latest new <name> --style=scss --ssr=false --package-manager=npm
```

## Step 2 — run the installer

```bash
node <skill-dir>/assets/install.mjs --root . --spec <url-or-path> --port 4200
```

Use `--no-api` instead of `--spec` when there is no OpenAPI backend. Other flags:
`--generated <path>` (default `src/app/api/generated`), `--app-name`, `--force`
(overwrite an existing eslint config / CLAUDE.md), `--no-install` (skip `npm i -D`).

It is idempotent — safe to re-run after a partial install. It writes:

| | |
|---|---|
| `tools/harness/verify.mjs` | the loop: prettier → eslint → types → unit tests, stops at the first failure |
| `tools/harness/typecheck.mjs` | `ngc --noEmit` per project, so **templates** are checked, not just `.ts` |
| `tools/harness/hooks/` | PreToolUse block, PostToolUse type check, Stop gate |
| `tools/harness/hooks-selftest.mjs` | proves the block actually blocks |
| `eslint.config.mjs` | angular-eslint + import boundaries between core/shared/features |
| `.claude/settings.json` | hooks wired, plus a permission allow/deny list |
| `.claude/launch.json` | dev server, so the agent can start the app itself |
| `.claude/agents/frontend-investigator.md` | read-only "where is this used" agent |
| `CLAUDE.md` | under 2 KB (written to `CLAUDE.harness.md` if one already exists — merge by hand) |
| `playwright.config.ts`, `e2e/` | e2e, deliberately outside the inner loop |
| `.github/workflows/verify.yml` | CI runs the same command, plus the real build |
| `package.json`, `tsconfig.json` | scripts; `strict`, `strictTemplates`, `noUnusedLocals` |

Read `references/whats-installed.md` if you need to explain or adjust a specific piece.

## Step 3 — get to green, without weakening the harness

```bash
npm run format
npm run verify
```

On a brand-new app this is green immediately. On an existing codebase it will not be, and
that is the point: `strict`, `strictTemplates` and `--max-warnings 0` are finding real
defects that were previously invisible. Fix the code.

Resist the temptation to make the loop pass by lowering the bar. If a specific rule is
genuinely wrong for this repo, disable **that rule** in `eslint.config.mjs` with a comment
saying why. Turning off `strict` or `strictTemplates` is different in kind — without them,
template type errors reappear at runtime as a blank page, which is exactly the failure an
agent cannot see and will report as done.

If the volume of findings is large, say so and offer to fix them in batches rather than
silently narrowing the config. `npm run verify types` (or `lint`, `format`, `test`) runs a
single check while you work through them.

## Step 4 — prove the guards, do not assume them

```bash
node tools/harness/hooks-selftest.mjs
```

Ten cases, including `sed -i`, a heredoc, a python one-liner and `rm -rf` against the
generated client — the ways a Bash-blind guard gets walked through. All ten must pass.

Then prove the boundary rule fires, because a misconfigured resolver makes it pass
silently on everything:

```bash
mkdir -p src/app/features/a src/app/features/b
printf 'export const a = 1;\n' > src/app/features/a/a.ts
printf "import { a } from '../a/a';\nexport const b = a;\n" > src/app/features/b/b.ts
npm run verify lint     # must FAIL with boundaries/dependencies
rm -rf src/app/features/a src/app/features/b
```

If it passes, the imports are not resolving — see `references/troubleshooting.md`.

Last, check the file that loads on every future session is still small:

```bash
node -e "console.log(require('fs').statSync('CLAUDE.md').size, 'bytes')"
```

The installed template is about 1.7 KB. If you tailored it — and you should, with the
conventions this repo actually follows — keep the total under 2048 bytes by cutting
something else. This file is a tax on every session forever, so length is a real cost, and
stale lines are worse than missing ones.

## Step 5 — generate the client (only with an OpenAPI backend)

```bash
npm run gen:api
npm run verify
```

The generated directory is linted-ignored, prettier-ignored, permission-denied for reads,
and hook-blocked for writes — but still type-checked, because a contract change should
surface as a compile error the agent can see and fix, not as a runtime surprise.

Check what came out before moving on. If the spec's operations carry no OpenAPI tags, the
generator emits one anonymous `Api` helper instead of injectable services — set
`"defaultTag"` in `ng-openapi-gen.json` and regenerate. Then replace any hand-written HTTP
call that duplicates the contract with the generated service; leaving both in place is the
drift the contract was supposed to end.

Details, remote specs and other generators: `references/openapi-client.md`.

## Step 6 — close the loop in a browser

Verify the app renders before declaring the harness done:

1. `preview_start` with the name in `.claude/launch.json`
2. read the console and network requests — a silent runtime error is the classic "tests
   green, page blank"
3. read the accessibility tree to confirm the content, rather than screenshotting it: the
   tree is cheaper and far better at answering "is the label right"

## Step 7 — hand it over

Tell the user, concretely: the verify time you actually measured, what verify covers, what
CI adds, which paths are now off-limits and why, and the one thing they should do next —
run three real tasks and note the tokens and wall time, so they know whether any of this
paid for itself.

Two things deliberately **not** built, worth saying out loud so nobody adds them by
reflex: Nx (it pays for many libraries, not for one app) and visual regression testing
(high maintenance, and it blocks agents on unrelated pixel shifts long before it catches a
real bug).

## References

- `references/whats-installed.md` — what each generated file does and how to tune it
- `references/openapi-client.md` — ng-openapi-gen config, remote specs, other generators
- `references/troubleshooting.md` — Karma, older Angular, monorepos, path aliases,
  boundary rules that silently pass, verify over 60s
