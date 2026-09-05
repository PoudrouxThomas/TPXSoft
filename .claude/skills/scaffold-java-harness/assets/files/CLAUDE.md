# CLAUDE.md

Loaded every session. Keep it under 2 KB and every line currently true -- a stale line here is
a wrong assumption paid for in every future session.

## Done means

`./dev verify` is green. It runs, stopping at the first failure: format check, checkstyle,
compile with warnings as errors, unit + architecture tests. Call `./dev`, never `gradle` or
`mvn` directly.

Also: `./dev format`, `./dev compile` (fast), `./dev test <pattern>`, `./dev verify-it`
(Testcontainers, on demand and CI only), `./dev openapi`, `./dev run`.

## Layering -- enforced by ArchitectureTest

- `api/` HTTP edge: controllers, DTOs, exception handling. Talks to `application` only.
- `application/` use cases and transactions. Talks to `domain` only.
- `domain/` model and ports. Plain Java: no Spring, no JPA, no Jackson.
- `infrastructure/` adapters that implement the domain ports. Nothing imports it.

A controller never touches a repository. Persistence types stay inside `infrastructure`.
Constructor injection only.

## Never hand-edit

`docs/openapi.json` is produced by `./dev openapi` and is what the frontend generates its client
from. Anything under a `generated/` directory is the same. A hook blocks Edit, Write and Bash
writes to those paths.

## Conventions

- Java __JAVA__, Spring Boot. Records for DTOs and domain values.
- `java.time` only -- never `java.util.Date`.
- Test names state behaviour: `shouldRejectBlankName`, not `testCreate2`.
- Integration tests end in `IT` and extend `IntegrationTestBase` (tagged `it`, outside verify).
- New endpoint: annotate it for OpenAPI, then run `./dev openapi` and commit the document.
