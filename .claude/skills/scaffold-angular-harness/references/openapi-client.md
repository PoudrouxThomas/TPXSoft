# The generated API client

The point is not tidiness. When the backend changes, regenerating turns a runtime surprise
into a compile error — something the agent can see and fix on its own. That only holds if
the generated code is never hand-edited, which is why the guard exists in three layers:
lint-ignored, prettier-ignored, read-denied, and write-blocked including through Bash.

## Configuration

`ng-openapi-gen.json`, written by the installer:

```json
{
  "$schema": "./node_modules/ng-openapi-gen/ng-openapi-gen-schema.json",
  "input": "http://localhost:5000/swagger/v1/swagger.json",
  "output": "src/app/api/generated",
  "ignoreUnusedModels": false,
  "indexFile": true
}
```

`input` takes a URL or a local `.json`/`.yaml` path. Useful additions:

- `"servicePrefix"` / `"modelPrefix"` when generated names collide with your own
- `"ignoreUnusedModels": true` to emit only models reachable from an operation
- `"defaultTag"` groups operations that carry no OpenAPI tag into one service — worth
  setting if the backend is untagged, otherwise everything lands in `ApiService`

Regenerate with `npm run gen:api` (generate, then prettier the output).

## Using it

Register `provideHttpClient()` in `app.config.ts` and set the base URL once:

```ts
import { ApiConfiguration } from './api/generated/api-configuration';

provideAppInitializer(() => {
  inject(ApiConfiguration).rootUrl = environment.apiUrl;
});
```

Feature code injects the generated services directly. Boundary rules allow every layer to
import the client, so no wrapper is required; write one only when you have real
app-specific behaviour to add (retry policy, error mapping), and put it in `core/`.

## Keeping it honest in CI

The generated directory is committed, so a stale client is visible in review. To catch
drift automatically, add a CI step that regenerates and fails on a diff:

```yaml
- run: npm run gen:api
- run: git diff --exit-code src/app/api/generated
```

This needs the spec to be reachable from CI — a committed spec file, or a spec artifact
published by the backend build. Skip the step rather than pointing CI at a dev server.

## If the generated code fails the type check

Generated output compiles cleanly under `strict` in current versions, but if a
`noUnusedLocals` or `strictNullChecks` error appears in generated files, fix it in the
generator config or with a targeted `"skipLibCheck"`-style narrowing — never by editing the
output, and never by weakening `strict` for the whole app. As a last resort, give the
generated directory its own `tsconfig` include with the one option relaxed, and say so in
CLAUDE.md.

## Other generators

`ng-openapi-gen` is the default because it is Angular-native, emits `HttpClient` services,
and needs nothing but node. Two alternatives, if the spec defeats it:

- **`@openapitools/openapi-generator-cli`** (`typescript-angular`) — broadest spec support,
  needs a JRE. Swap the `gen:api` script; everything else in the harness is unchanged,
  since the guard follows `harness.protectedPaths`.
- **Orval** — config-driven, good ergonomics, smaller Angular ecosystem.

Whichever you use, keep one command, one output directory, and that directory in
`package.json > harness.protectedPaths`. That is the whole contract the rest of the harness
depends on.
