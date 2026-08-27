---
name: new-feature
description: Implement a new Angular frontend feature (route/page/component/service) in apps/<app>/web, with unit and e2e tests, gated by /verify-frontend. Use when asked to add or change user-facing behavior in an Angular app.
---

Feature loop for the Angular workspace at repo root. A feature isn't done until `/verify-frontend` is green — never mark it complete on a red or skipped check.

1. Confirm the target app and surface: `apps/<app>/web` (default `sharepoint`, project name `<app>-web` in `angular.json`), and whether this is a new route/page, a new component, or an extension of an existing component/service.
2. Scaffold with `ng generate component <path> --project=<app>-web` (or `ng generate service ...`) instead of hand-writing boilerplate. Delegate to `angular-implementer` if the shape isn't already obvious.
3. Implement the feature: standalone components (no NgModules), Angular Material, SCSS — match existing conventions in `apps/<app>/web/src/app`.
4. Write tests alongside the code, not after:
   - Unit spec colocated with the source (`foo.ts` + `foo.spec.ts`), covering component/service logic.
   - Playwright e2e spec under `apps/<app>/web/e2e/tests/*.spec.ts` covering the user-facing flow — skip only for pure internal refactors with no visible behavior.
5. Run `/verify-frontend <app>` — format, typecheck, unit tests, e2e, in that order, stopping at the first failure. Fix and re-run until it's fully green before reporting done.
