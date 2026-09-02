# Angular web app — agentic harness checklist

A build order for a strong agent harness on a **single Angular application**, in a company
setting: fully local, no cloud runners, no scheduled routines, limited token quota.

Companion to [JAVA_HARNESS.md](JAVA_HARNESS.md). The build order is the same; the tooling and
two of the steps are genuinely different. Read the Java file first if you are building both —
this one only argues the points where the frontend diverges.

---

## The one rule

**The verification loop is the constraint. Build it first, keep it fast, keep it quiet.**

Frontend adds a second constraint the backend does not have: **a passing test suite does not
mean the page renders.** A harness that only runs unit tests will happily report success on a
blank screen. The browser loop in step 6 is not optional here — it is the frontend half of
"an agent that can check its own work."

Two hard numbers:

- **Under 60 seconds** for `npm run verify`.
- **Under 15 lines of output** on success. Webpack/Vite/Karma output is verbose by default and
  lands in the model's context on every run.

---

## Build order

### 1. `npm run verify` — fast, quiet, one command

- [ ] One npm script that runs, in order, stopping at the first failure:
      **prettier --check → eslint → tsc --noEmit → unit tests**.
- [ ] Every consumer (hooks, CI, agents, humans) calls **this script only**, never `ng`
      or `vitest` directly.
- [ ] **Do not put `ng build` in the inner loop.** A production build is a release step, not a
      verification step. `tsc --noEmit` catches what you need in a fraction of the time.
- [ ] Unit tests headless and quiet: Vitest (Angular 20+) or Jest. Karma + a real Chrome is
      too slow for this loop.
- [ ] On failure, print the first failing check only.

### 2. Determinism — remove taste from the loop

- [ ] **Prettier** with a committed `.prettierrc`. `format` applies, `verify` checks.
- [ ] **ESLint** with `angular-eslint`, run with `--max-warnings 0`.
- [ ] **TypeScript strict everything** in `tsconfig.json` — this is the frontend equivalent of
      `-Werror` and the single highest-leverage setting in the file:
      ```jsonc
      "strict": true,
      "noImplicitOverride": true,
      "noPropertyAccessFromIndexSignature": true,
      "noImplicitReturns": true,
      "noUnusedLocals": true,
      // angularCompilerOptions
      "strictTemplates": true,
      "strictInjectionParameters": true
      ```
- [ ] `strictTemplates` especially. Without it, template type errors surface as a blank page at
      runtime — exactly the failure an agent cannot see and will report as done.

### 3. Architecture rules — import boundaries

Angular has no ArchUnit. The equivalent is lint rules, and they are worth the setup:

- [ ] Decide the layering up front: `core/` (singletons, guards, interceptors), `shared/`
      (dumb reusable components), `features/<name>/` (everything else).
- [ ] Enforce it with `eslint-plugin-boundaries`, or `@typescript-eslint/no-restricted-imports`:
      - no `features/a` importing from `features/b`
      - no `shared/` importing from `features/`
      - no deep imports past a feature's public entry point
- [ ] Runs inside `npm run verify`, therefore inside the agent's definition of done.

Without this, feature-to-feature imports appear gradually, each one individually reasonable, and
by the time it is visible in a diff it is a week of untangling.

### 4. `CLAUDE.md` — after the loop exists, under 2 KB

- [ ] The one verify command, the folder layering rule, paths never to hand-edit, definition of done.
- [ ] Add the few Angular conventions an agent cannot infer and will otherwise get wrong:
      standalone components vs modules, signals vs RxJS, the state approach, the component-style
      convention (inline vs separate files).
- [ ] Nothing else. This file loads on **every** session; stale content is a recurring tax.

### 5. Hooks

- [ ] **PostToolUse** on `.ts` edits → `tsc --noEmit` scoped to the touched project.
- [ ] **Stop** → `npm run verify`. **Must exit 2 on failure** — exit 1 is a warning the agent
      never sees.
- [ ] **PreToolUse** blocking writes to generated API clients (see step 7). **Match `Bash` as
      well as `Edit`/`Write`** — otherwise `sed -i` or a heredoc walks straight through it.
- [ ] "What changed" detection must work on the main branch, with uncommitted changes, and on
      untracked files.

### 6. The browser loop — the frontend-specific one

This is what separates a real frontend harness from a backend harness pointed at TypeScript.
Claude Code can drive a browser against your dev server, so the agent verifies its own UI change
instead of asking you to look at it.

- [ ] Add `.claude/launch.json` with your dev server (name, command, port) so the agent can start
      it without shelling out.
- [ ] Establish the loop in `CLAUDE.md`: **start preview → reload → read console for errors →
      read the accessibility tree to confirm content → interact → confirm.**
- [ ] **Read the page, do not screenshot it.** The accessibility tree and page text are cheap
      and precise; screenshots cost roughly a thousand tokens each and are worse at answering
      "is the label right." Screenshot only to show a human the final visual result.
- [ ] Check the browser console and network requests explicitly — a silent runtime error is the
      classic "tests green, page blank" failure.
- [ ] For responsive or theming work, emulate viewport and color scheme rather than guessing.

A frontend agent without this reports success on a page that never rendered. It is the highest
value item in this list after step 1.

### 7. Generated API clients — contract-first

- [ ] Generate the client from the backend's OpenAPI (`ng-openapi-gen` or `openapi-generator`)
      into a directory nobody hand-edits.
- [ ] One command to regenerate. Wire the PreToolUse block from step 5 onto that path.
- [ ] Never let an agent hand-write an HTTP call that duplicates the contract.

The payoff is not just tidiness: when the backend changes, regeneration turns a runtime surprise
into a compile error, which the agent can actually see and fix on its own.

### 8. CI — the same command

- [ ] CI runs `npm run verify`, plus `ng build` (the real production build belongs here, not in
      the inner loop), plus e2e.
- [ ] `npm ci`, never `npm install`. Commit the lock file.
- [ ] Nothing in CI checks something local verify does not.

### 9. Token hygiene — free, and larger than it sounds on frontend

Angular repos are unusually good at wasting context:

- [ ] Keep the agent out of `node_modules/`, `.angular/cache/`, `dist/`, `package-lock.json`,
      and the generated client. A single accidental read of a lock file can cost more than a
      whole task.
- [ ] **One task per session, `/clear` between tasks.**
- [ ] **Permission allowlist** for the read-only commands you run constantly.
- [ ] Be aware that "read this component" means four files — `.ts`, `.html`, `.scss`, `.spec.ts`.
      Ask for the one you need. Consider inline templates for small components; it genuinely
      reduces the per-component read cost.
- [ ] Prefer the accessibility tree over screenshots, always (step 6).
- [ ] Do not install a token tool whose savings you have not measured on your own repo.

### 10. One read-only subagent

- [ ] A "where is this component used / what calls this service" investigator with `Read`,
      `Grep`, `Glob` only. It burns its own context on the search and hands you the answer.

### 11. `npm run e2e` — Playwright, separate and slow

- [ ] Playwright, headless, against a locally served build.
- [ ] Never in the Stop hook. CI and on demand only.
- [ ] A handful of real user journeys — login, the main create flow, one error path. Not a
      per-component e2e suite.

### 12. Measure, once

- [ ] Log tokens and wall time for three real tasks, so you know whether any of this paid.

### 13. Only now: skills

- [ ] Write a skill for a workflow you have done **manually three times** — most likely
      "new feature: route + component + service + spec + e2e" once you know its real shape.
- [ ] Short procedure, not an essay. Skills cost input tokens every session.

---

## Specs: make them executable

Same rule as the backend. Prose specs rot silently and agents believe them.

| Intent | Executable form |
|---|---|
| API surface | The backend's OpenAPI contract + the generated client |
| Behavior | Test names, and Playwright scenario names |
| Architecture | ESLint import-boundary rules |
| Visual intent | A screenshot attached to the ticket — not prose in the repo |
| Decisions | Short ADRs in `docs/adr/`, append-only, never edited |

---

## Do not build

| Skip | Why |
|---|---|
| Nx, for one app | Nx pays for many libraries. One app plus a shared folder does not need a build graph. Adopt it if and when the app splits into real libraries. |
| A component-library harness | Unless you are actually publishing one. Storybook is a design tool, not a verification loop. |
| Visual regression testing | High maintenance, noisy, and it will block agents on unrelated pixel shifts long before it catches a real bug. |
| Per-component e2e | Slow, brittle, and duplicates unit tests. A few journeys is the right number. |
| A specialized agent per layer | Two read-only agents is the ceiling for one repo. |
| MCP servers wrapping CLIs | Every tool definition loads into every session. `glab`/`jira` CLI behind a skill costs nothing until called. |

---

## Done when

- [ ] `npm run verify` is green, under 60s, under 15 lines of output.
- [ ] `strict` and `strictTemplates` are on and the repo is clean under both.
- [ ] Import boundaries are enforced by lint, inside `verify`.
- [ ] The agent can start the app, read the console, and confirm its own change rendered —
      without asking a human to look.
- [ ] A red verify blocks the agent from finishing (Stop hook, exit 2).
- [ ] The generated API client cannot be hand-edited, including through Bash.
- [ ] `CLAUDE.md` is under 2 KB and currently true.

Steps 1-6 are about a week. That is a strong harness for one Angular app, and roughly where the
value plateaus for a single team.
