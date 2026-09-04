# The OpenAPI document, and generating a frontend client from it

## How it is produced

springdoc builds the document from the code at runtime; `/v3/api-docs` serves it and
`/swagger-ui.html` renders it. Both are permitted without credentials in the bundled
`SecurityConfig` -- change that if the API is public.

`./dev openapi` boots the app once and writes `docs/openapi.json`. Commit that file: it is the
contract other repositories build against, and a file in git is the only version they can pin.

`OpenApiDocIT` runs the same request without the write flag and fails when the committed copy no
longer matches the code. That is the whole point -- a stale contract is not a documentation
problem, it is a broken build in another repository, discovered a month later.

## Making the document worth generating from

Generators produce method names, types and doc comments from what the document actually says,
so thin annotations produce a thin client.

- `@Tag` on each controller -- becomes the service/class name in most generators. Without tags,
  many generators emit one giant `DefaultApi`.
- `@Operation(summary = ...)` on each method -- becomes the method name and doc comment.
- `@ApiResponse` for every status the client must handle, especially 4xx.
- Response DTOs, never entities. An entity leaks lazy proxies and persistence fields into the
  contract, and renaming a column becomes a breaking API change.
- Validation annotations (`@NotBlank`, `@Size`, `@Min`) -- springdoc turns them into schema
  constraints the client can enforce before the round trip.

## Generating the client

Point the generator at the committed file, not at a running server: a client generated from a
server somebody had running locally is not reproducible.

TypeScript / Angular:

```bash
npx @openapitools/openapi-generator-cli generate \
  -i ../api/docs/openapi.json -g typescript-angular -o src/app/api/generated
```

Other common targets: `typescript-fetch`, `typescript-axios`, `java` (for another service),
`kotlin`. `ng-openapi-gen` and `orval` are lighter alternatives for TypeScript.

Whatever the frontend uses, add its output directory to that repository harness as a protected
path so nobody hand-edits it -- the same rule as here, for the same reason.

## Keeping the two repositories in step

The cheapest arrangement that actually works:

1. This repo publishes `docs/openapi.json` as a CI artifact (already wired in `verify.yml`).
2. The frontend regenerates from a pinned version of that file and commits the result.
3. A contract change that breaks a consumer shows up as a compile error there, not as a 400 in
   production.

If you later want the reverse direction (contract first, server generated from the YAML), keep
the same guard: the generated sources go under a `generated/` path, which the PreToolUse hook
already blocks by default.
