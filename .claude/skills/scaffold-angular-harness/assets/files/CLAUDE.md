# CLAUDE.md

Loads every session. Keep it under 2 KB and currently true.

## Done means

`npm run verify` is green. It runs, stopping at the first failure: prettier, eslint
(`--max-warnings 0`), type check including templates, unit tests. Call `npm run verify`,
never `ng` / `eslint` / `vitest` directly.

Also: `npm run format`, `npm run verify types` (one step), `npm run e2e` (on demand and CI
only), `npm run build` (release step, not verification).

## Layering — enforced by eslint

- `src/app/core/` guards, interceptors, app-wide singletons
- `src/app/shared/` dumb reusable components; knows nothing about any feature
- `src/app/features/<name>/` **no feature imports another feature**
- `__GENERATED__/` generated API client

## Never hand-edit

`__GENERATED__/` comes from the OpenAPI contract — run `__GEN_CMD__`. A hand edit is erased
by the next generation and hides real contract drift meanwhile. A hook blocks Edit, Write
and Bash writes there.

## Verify the page, not just the tests

Green tests do not mean the page renders. After a UI change: `preview_start` (see
`.claude/launch.json`, port __PORT__), reload, read the console and network requests, read
the accessibility tree to confirm the content, interact, read again.

Read the page, do not screenshot it — the tree is cheaper and more precise about "is the
label right". Screenshot only to show a human the result.

## Conventions

- Standalone components, `inject()` over constructor injection
- Signals for state; RxJS only where a stream is really a stream
- __STYLE_CONVENTION__
- "A component" is up to four files (`.ts`, `.html`, `.scss`, `.spec.ts`) — open one
