# 01 — Upload a document

Read [`README.md`](README.md) first: the domain model, the access table, the error enum, and the
config keys used below are defined there.

## Endpoint

`POST /documents` — `operationId: uploadDocument`, `multipart/form-data`, bearer token required.

| Part | Type | Required | Meaning |
|---|---|---|---|
| `file` | binary | yes | The file itself. Its filename and content type come from the part headers. |
| `folderId` | uuid, nullable | no | Target folder. Absent or `null` means the caller's root. |

Responses: `201` with a `Document`, `400` validation, `401` unauthenticated. Note there is **no**
`403` or `404` on this route in the contract — an unusable `folderId` therefore has to surface as
`400`; see Open questions.

## Behavior

1. Read `sub` and `orgId` from the token. No claims, no upload — 401.
2. Bind the multipart form. Reject immediately if there is no `file` part, if it has zero bytes, or
   if the request exceeds `Documents:MaxUploadBytes`.
3. If `folderId` is present and not null, load that folder. It must exist **and** be owned by the
   caller. Anything else is a validation failure.
4. Sanitize the file name and the content type (rules below).
5. Create the `Document` (`Visibility = Private`, `PublicLinkToken = null`, `OrgId` from the claim,
   `CreatedAt = UpdatedAt = TimeProvider.GetUtcNow()`) and its `DocumentContent` row in one
   transaction, through the same `IUnitOfWork` pattern Auth uses.
6. Return `201` with the `Document` projection. The body never contains the bytes.

New documents are always `Private`. Making a document org-visible or public is a separate, explicit
act — see file `04`.

## Validation

| Rule | On failure |
|---|---|
| `file` part present | 400 |
| `file` length > 0 | 400 |
| `file` length ≤ `Documents:MaxUploadBytes` | 400 |
| Filename present after sanitization (not empty, not all-stripped) | 400 |
| `folderId`, when supplied, exists and is owned by the caller | 400 |

### File name sanitization

The browser controls this string, so treat it as attacker input:

- Take the last path segment only — strip anything before a `/` or `\`. `..\..\etc\passwd` becomes
  `passwd`. Nothing here ever touches a real filesystem path, but the name is echoed into a
  `Content-Disposition` header later (file `05`), and downstream clients do write it to disk.
- Reject or strip control characters (`U+0000`–`U+001F`, `U+007F`) — these are what break header
  encoding.
- Trim whitespace, collapse the result, cap at **255** characters (`varchar(255)`, matching the
  common filesystem limit). Truncate rather than reject, but preserve the extension when truncating.
- If nothing survives, return 400 rather than inventing a name.

**Duplicate names are allowed.** Two documents with the same name may sit in the same folder; the
contract defines no `409` for this route, so no uniqueness constraint exists. Clients disambiguate
by id.

### Content type

- Take `IFormFile.ContentType`. If it is missing, blank, or not a syntactically valid media type
  (`type/subtype` with optional parameters, RFC 9110 token rules), store `application/octet-stream`.
- Cap at 128 characters, `varchar(128)`.
- **Do not trust it for anything but round-tripping.** It is metadata the uploader chose. The
  defense against a malicious `text/html` upload happens at download time via response headers, not
  here (file `05`).
- Do not sniff the bytes to derive a "real" type in v1 — flagged under Open questions.

## Persistence

```
documents
  id                uuid       PK
  owner_user_id     uuid       not null
  org_id            uuid       not null
  folder_id         uuid       null, FK -> folders(id) ON DELETE RESTRICT
  file_name         varchar(255) not null
  content_type      varchar(128) not null
  size_bytes        bigint     not null
  visibility        varchar(20) not null   -- HasConversion<string>()
  public_link_token varchar(64) null
  created_at        timestamptz not null
  updated_at        timestamptz not null

  index (owner_user_id, folder_id)   -- the listing query in file 02
  index (org_id) where visibility = 'Organization'
  unique index (public_link_token) where public_link_token is not null

document_contents
  document_id  uuid  PK, FK -> documents(id) ON DELETE CASCADE
  bytes        bytea not null
```

`ON DELETE RESTRICT` on `folder_id` is what makes "a folder must be empty to be deleted" (file `07`)
true at the database level and not only in a service check.

`Visibility` is stored as a string via `HasConversion<string>()` — the same choice made for
`User.Role` in Auth, so a migration never depends on enum ordinal stability.

## Streaming vs buffering

The straightforward implementation binds `IFormFile` and calls `CopyToAsync` into a
`MemoryStream`. That buffers the whole file in managed memory before it reaches Postgres.

For v1 that is acceptable: the cap is 25 MiB and the content column is `bytea`, which Npgsql writes
as a single parameter anyway — there is no incremental path to a `bytea` column that avoids holding
the value. Set the form options limit so ASP.NET rejects oversized bodies before the handler ever
allocates:

```csharp
services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);
```

If document sizes ever grow past this, the fix is Postgres large objects or an external blob store,
not a cleverer buffer — that is the revisit condition already recorded in
[`modules/documents/CLAUDE.md`](../CLAUDE.md).

## Errors

| Case | Status | Body |
|---|---|---|
| Missing/empty/oversized file, unusable filename | 400 | `{"message": "Validation failed."}` |
| `folderId` unknown or not owned by the caller | 400 | `{"message": "Validation failed."}` |
| No or invalid bearer token | 401 | `{"message": "Missing or invalid access token."}` |

The folder message stays generic: a distinct "folder not found" would tell a caller which folder ids
exist in other accounts.

## Tests

Unit (`TPXSoft.Documents.UnitTests`):

- `Document.Create` sets `Visibility = Private` and null `PublicLinkToken`.
- `Document.Create` sets `CreatedAt == UpdatedAt` from a frozen `TimeProvider`.
- Filename sanitization: path segments stripped, control characters removed, 255-char truncation
  keeps the extension, empty-after-sanitization is a failure.
- Content type fallback to `application/octet-stream` for blank and malformed values.
- Upload into a folder owned by someone else fails.

Integration (`TPXSoft.Documents.IntegrationTests`, Testcontainers Postgres):

- Upload with no `folderId` → `201`, `folderId` null, row present, content bytes round-trip
  byte-for-byte through `GET /documents/{id}/content`.
- Upload into an owned folder → `201`, `folderId` set.
- Upload into another user's folder → `400`.
- Upload with an empty file part → `400`.
- Upload one byte over `MaxUploadBytes` → `400` (not an unhandled 500 from the form limit).
- Upload with no `Authorization` header → `401`.
- Two uploads with the same filename into the same folder both succeed with different ids.

## Open questions

- **Oversized uploads return 400, not 413.** `413 Content Too Large` is the correct status and is
  not in the contract. Add it to `uploadDocument` and `replaceDocumentContent` when the contract is
  next touched, then map the limit failure to it.
- **A bad `folderId` returns 400, not 404.** Forced by the contract's response set for this route.
  Adding `404` to `uploadDocument` would let this be honest; it is also a wider existence leak.
- **No content sniffing.** A file named `report.pdf` declaring `application/pdf` while containing
  HTML is stored as-is. File `05`'s download headers are what make that harmless, so sniffing is not
  urgent — but it is the reason those headers are mandatory.
- **No per-user or per-org storage quota.** Nothing stops a user filling the database. Needs a
  counter on upload and a `409`/`507` response that the contract does not currently define.
