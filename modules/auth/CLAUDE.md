# TPXSoft.Auth

Loaded only when working inside `modules/auth/`.

## Bounded context

OIDC-ish JWT issuance, users, orgs, roles — the first module in the harness (PLAN.md Phase 1). Minimal scope: an org is created at registration, each user belongs to exactly one org, and role is a simple field on the user rather than a separate entity. No invite flow, no multi-org membership — that's out of scope until a real need for it shows up.

## Entities

- **Org** — `Id`, `Name`, `CreatedAt`
- **User** — `Id`, `Email`, `PasswordHash`, `OrgId`, `Role` (`Admin` | `Member`), `CreatedAt`
- **RefreshToken** — `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt?`

## Endpoints

Source of truth: [`contracts/auth.v1.yaml`](../../contracts/auth.v1.yaml).

- `POST /auth/register` — creates a new Org + the first User as `Admin`, returns tokens
- `POST /auth/login` — returns `{accessToken, refreshToken}`
- `POST /auth/refresh` — rotates the refresh token
- `POST /auth/logout` — revokes a refresh token
- `GET /auth/me` — current user from the bearer token

## Verify

```bash
tpx verify auth
tpx test auth --integration
```

## Known consumers

None yet — Auth is the first module. Other modules will call it through `shared/clients/**` (generated from the contract above), never by referencing `TPXSoft.Auth.Domain`/`TPXSoft.Auth.Infrastructure` directly (enforced by `tpx verify boundaries`).

## MCP server

`src/TPXSoft.Auth.Mcp` (stdio, registered in root `.mcp.json` as `tpxsoft-auth`) exposes `get_openapi()`, `list_endpoints()`, `describe_entity(name)`, `find_consumers(entity_or_field)`, `run_tests(filter?)`, `get_migrations_status()` — sourced from the contract, not from reading `.cs` files. This module's MCP server is the template `new-module` copies for every module after it (PLAN §0.7).
