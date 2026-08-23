# TPXSoft.Auth

Loaded only when working inside `modules/auth/`.

## Bounded context

OIDC-ish JWT issuance, users, orgs, roles — the first module in the harness (PLAN.md Phase 1). Minimal scope: an org is created at registration, each user belongs to exactly one org, and role is a simple field on the user rather than a separate entity. No invite flow, no multi-org membership — that's out of scope until a real need for it shows up.

## Entities

- **Org** — `Id`, `Name`, `CreatedAt`
- **User** — `Id`, `Email`, `PasswordHash`, `OrgId`, `Role` (`Admin` | `Member`), `CreatedAt`
- **RefreshToken** — `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt?` — server-side state only, never returned in any response

`Org` and `RefreshToken` are Domain entities without a matching contract schema — nothing returns them over the wire yet (register returns a `TokenPair`, not the `Org` it created).

## Endpoints

Source of truth: [`contracts/auth.v1.yaml`](../../contracts/auth.v1.yaml).

- `POST /auth/register` — creates a new Org + the first User as `Admin`, returns tokens (`201`, or `400`/`409`)
- `POST /auth/login` — returns `{accessToken, refreshToken}` (`200`, or `400`/`401`)
- `POST /auth/refresh` — rotates the refresh token, single-use (`200`, or `400`/`401`)
- `POST /auth/logout` — revokes a refresh token, idempotent — `204` even for an unknown token (`auth.v1.yaml`'s documented behavior, not a bug)
- `GET /auth/me` — current user from the bearer token (`200`, or `401`)

## JWT access token claims

Not expressible in OpenAPI, but part of this module's real interface — a change here is breaking even though `tpx contract lint` can't see it (also noted on `bearerAuth`'s description in the contract itself):

`sub` (User.Id) · `email` · `orgId` · `role` (`"Admin"` | `"Member"`) · plus standard `jti`/`iat`/`exp`/`iss`/`aud`.

HMAC-SHA256, `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler` (not the legacy handler), `MapInboundClaims = false` so `sub` isn't silently rewritten to a long claim-type URI on the validation side.

## Config keys

`ConnectionStrings:AuthDb`, `Auth:Jwt:Issuer` (`tpxsoft-auth`), `Auth:Jwt:Audience` (`tpxsoft`), `Auth:Jwt:SigningKey` (**never committed** — env var `Auth__Jwt__SigningKey` or user-secrets only, ≥32 bytes), `Auth:Jwt:AccessTokenLifetimeMinutes` (15), `Auth:RefreshTokenLifetimeDays` (7), `Auth:ApplyMigrationsAtStartup` (`false`, `true` in `appsettings.Development.json`).

## Known assumptions and deferred decisions

Flagged during planning rather than silently baked in — revisit if they stop holding:

- **Multiple concurrent sessions per user are allowed.** Login never revokes a user's other refresh tokens. The alternative (one active session at a time) is a meaningfully different product decision.
- **No refresh-token-family revocation.** A reused (already-rotated-away) refresh token is rejected, but replaying it doesn't revoke the rest of that login's token lineage (the stronger OAuth BCP behavior, useful as a stolen-token signal). Deliberate scope cut, not an oversight.
- **No password rehash-on-verify.** `PasswordHasher<User>.VerifyHashedPassword` returning `SuccessRehashNeeded` is treated as plain success; the hash is not upgraded in place. Fine for v1's single hashing scheme.
- **Email comparison is case-insensitive.** Normalized to trimmed + lowercase-invariant before every lookup and the unique index. Baked into the DB constraint, not just a service-level check.
- **Refresh token hash is deliberately unsalted (SHA-256 of the raw token).** It has to be looked up by hash, which requires a deterministic digest; safe because the token itself is 256 bits of CSPRNG output, not a low-entropy secret like a password. Don't "fix" this into a salted/adaptive hash — that would break the lookup.

## Environment notes

- **Docker daemon may not be available.** In a permission-restricted sandbox (no `ulimit` access), `dockerd` cannot start at all, which blocks `tpx test auth --integration` (Testcontainers needs a live daemon) and real `docker compose up -d` runs. The integration suite is still written and structurally complete under `tests/TPXSoft.Auth.IntegrationTests`; it just can't execute there. Check `docker ps` before assuming a red integration run is a real regression.
- **.NET SDK/runtime mismatch on some cloud images.** If only a newer SDK than the `net9.0` this module targets is installed (e.g. SDK 10 with no `net9.0` runtime), `dotnet test`/`dotnet run` need `DOTNET_ROLL_FORWARD=Major` — the SessionStart hook (`.claude/hooks/session-start.sh`) exports this automatically. `dotnet build` alone isn't affected (it restores the `net9.0` targeting pack from NuGet regardless).

## Verify

```bash
tpx verify auth
tpx test auth --integration   # needs a live Docker daemon -- see Environment notes above
```

## Known consumers

None yet — Auth is the first module. Other modules will call it through `shared/clients/**` (generated from the contract above), never by referencing `TPXSoft.Auth.Domain`/`TPXSoft.Auth.Infrastructure` directly (enforced by `tpx verify boundaries`).

## MCP server

`src/TPXSoft.Auth.Mcp` (stdio, registered in root `.mcp.json` as `tpxsoft-auth`) exposes `get_openapi()`, `list_endpoints()`, `describe_entity(name)`, `find_consumers(entity_or_field)`, `run_tests(filter?)`, `get_migrations_status()` — sourced from the contract, not from reading `.cs` files. `run_tests` shells to `tpx verify auth`; `get_migrations_status` shells to `dotnet-ef migrations list --no-connect` (works without a live database). This module's MCP server is the template `new-module` copies for every module after it (PLAN §0.7).
