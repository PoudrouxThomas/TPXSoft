---
description: Run the Angular frontend verification loop (format, typecheck, unit tests, e2e) for one app, stopping at the first failure.
argument-hint: [app]
---

Verify the Angular app at `apps/<app>/web`, where `<app>` is `$ARGUMENTS` if given, else `sharepoint`. Angular project name is `<app>-web` (e.g. `sharepoint-web`).

Run these steps **in order** and **stop at the first failure** — do not run later (slower) steps once an earlier one fails.

1. **Format check**
   `npx prettier --check "apps/<app>/web/src/**/*.{ts,html,scss}"`
   On failure: list the unformatted files from the output, tell the user `npx prettier --write "apps/<app>/web/src/**/*.{ts,html,scss}"` will fix them, and stop. Do not run `--write` yourself.

2. **Typecheck** (TS + Angular templates, then specs)
   `npx ng build <app>-web --configuration development`
   `npx tsc -p apps/<app>/web/tsconfig.spec.json --noEmit`
   The build target excludes `*.spec.ts`, so the `tsc` pass covers spec files separately. On failure: show the compiler errors and stop.

3. **Unit tests**
   `npx ng test <app>-web --watch=false`
   Forces a single run (no watch mode). On failure: show the failing test output and stop.

4. **E2E tests**
   `npx playwright test --config=apps/<app>/web/e2e/playwright.config.ts`
   If this fails specifically because Playwright browsers aren't installed (error mentions missing browser executables), run `npx playwright install --with-deps chromium` once, then retry this step once. Any other failure: show the output and stop.

If all four steps pass, report a short summary: app name, and that format/typecheck/unit/e2e all passed. Don't print full passing output — just confirm each step and move on.
