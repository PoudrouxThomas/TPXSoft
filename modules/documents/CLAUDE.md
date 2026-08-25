# TPXSoft.Documents

Loaded only when working inside `modules/documents/`.

## Bounded context

Document storage, virtual folders, ownership, and sharing (PLAN.md Phase 2). Content is stored as Postgres `bytea` -- no external blob store. No document version history. No anonymous access except the public-link download route.

The contract (`contracts/documents.v1.yaml`) was written ahead of this scaffold and already describes the full v1 surface below; nothing has been implemented against it yet -- `src/` and `tests/` are empty skeletons (`new-endpoint` fills them in one endpoint at a time).

## Entities (from the contract; no Domain types exist yet)

- **Document** — `Id`, `OwnerUserId`, `FolderId?`, `FileName`, `ContentType`, `SizeBytes`, `Visibility` (`Private` | `Organization` | `PublicLink`), `PublicLinkToken?`, `CreatedAt`, `UpdatedAt`
- **Folder** — `Id`, `OwnerUserId`, `ParentFolderId?`, `Name`, `CreatedAt`, `UpdatedAt`
- **DocumentShare** — `Id`, `DocumentId`, `GrantedToUserId`, `GrantedByUserId`, `CreatedAt` — explicit per-user grant, independent of `Visibility` and persists regardless of it

## Feature documentation

[`documentation/`](documentation/) holds one file per feature — validation rules, authorization
rules, persistence shape, error mapping, test list, and open questions per slice of the contract.
Read [`documentation/README.md`](documentation/README.md) before implementing any endpoint here; it
carries the shared decisions (domain model, access-rules table, `DocumentError` mapping, config
keys, the tri-state PATCH rule) the per-feature files build on.

## Endpoints

Source of truth: [`contracts/documents.v1.yaml`](../../contracts/documents.v1.yaml).

- `POST /documents` — upload a document (multipart), owned by the caller
- `GET /documents` — list documents visible to the caller (`folderId`, `mine` filters)
- `GET /documents/{id}` / `PATCH /documents/{id}` / `DELETE /documents/{id}` — metadata read/rename-move/delete
- `GET /documents/{id}/content` / `PUT /documents/{id}/content` — download / replace raw bytes
- `PUT /documents/{id}/visibility` — change `Visibility`; setting `PublicLink` (re)generates `publicLinkToken`, setting `Private`/`Organization` clears it
- `GET /documents/{id}/shares` / `POST /documents/{id}/shares` / `DELETE /documents/{id}/shares/{userId}` — per-user share grants
- `GET /public/documents/{token}/content` — unauthenticated download by public-link token; never listed anywhere, reachable only by holding the exact link
- `POST /folders`, `GET /folders`, `GET /folders/{id}`, `PATCH /folders/{id}`, `DELETE /folders/{id}` (fails on non-empty), `GET /folders/{id}/children`

## Auth

`bearerAuth` on every route except the public-link download -- the same access token issued by `TPXSoft.Auth` (see `contracts/auth.v1.yaml`'s `bearerAuth` description). `sub` (User.Id) and `orgId` drive ownership and `Organization`-visibility checks here; this module has no login/token-issuance of its own.

## Config keys (not yet wired -- `Infrastructure/DependencyInjection.cs` is an empty stub)

Expected once the first endpoint lands: `ConnectionStrings:DocumentsDb` (placeholder already in `appsettings.json`/`appsettings.Development.json`, mirroring Auth's shape).

## Known assumptions and deferred decisions

- **Content in Postgres `bytea`, not a blob store.** Fine for v1 scope; revisit if document sizes or volume make it painful.
- **No version history.** Replacing content (`PUT .../content`) overwrites in place.
- **`Visibility` and per-user `DocumentShare` grants are independent axes** -- an explicit share grant is not revoked by narrowing `Visibility`, and does not widen who can list the document, only who can fetch it.

## Verify

```bash
tpx verify documents
tpx test documents --integration   # needs a live Docker daemon
```

## Known consumers

None yet.

## MCP server

`src/TPXSoft.Documents.Mcp` (stdio, registered in root `.mcp.json` as `tpxsoft-documents`) exposes `get_openapi()`, `list_endpoints()`, `describe_entity(name)`, `find_consumers(entity_or_field)`, `run_tests(filter?)`, `get_migrations_status()` -- copied from Auth's MCP server per PLAN §0.7, sourced from the contract rather than `.cs` files (which barely exist yet here).
