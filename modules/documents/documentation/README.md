# Documents module — feature documentation

One file per feature. Each file is the working spec for a slice of
[`contracts/documents.v1.yaml`](../../../contracts/documents.v1.yaml) and is meant to be handed to
`new-endpoint` / `dotnet-implementer` as-is: it names the endpoints, the validation rules, the
authorization rules, the persistence shape, the error mapping, and the tests that have to exist
before the slice counts as done.

The contract stays the source of truth. Where a file below says something the contract does not
express (an upload size cap, a response header, a tri-state PATCH rule), that is an implementation
decision recorded here; where a file says the contract is *missing* something, it is flagged under
**Open questions** and needs a contract change plus `tpx gen` before code is written.

| # | Feature | Endpoints |
|---|---------|-----------|
| [01](01-upload-document.md) | Upload a document | `POST /documents` |
| [02](02-virtual-folders.md) | Documents live in virtual folders | `GET /documents`, `GET /documents/{id}` |
| [03](03-rename-move-delete-document.md) | Rename, move, delete a document | `PATCH /documents/{id}`, `DELETE /documents/{id}` |
| [04](04-sharing-and-visibility.md) | Sharing: Private / Organization / PublicLink | `PUT /documents/{id}/visibility`, `GET` + `POST /documents/{id}/shares`, `DELETE /documents/{id}/shares/{userId}` |
| [05](05-preview-and-download.md) | Preview or download | `GET /documents/{id}/content`, `GET /public/documents/{token}/content` |
| [06](06-update-document-content.md) | Update a document's content | `PUT /documents/{id}/content` |
| [07](07-manage-folders.md) | Manage folders | `POST /folders`, `GET /folders`, `GET` + `PATCH` + `DELETE /folders/{id}`, `GET /folders/{id}/children` |

## Suggested build order

`07` → `01` → `02` → `03` → `06` → `04` → `05`.

Folders first because `folderId` is an input to upload; sharing before download because the
download route's authorization check *is* the visibility rules in file `04`. Nothing here requires
building all of it at once — each file is independently shippable behind `tpx verify documents`.

## Domain model

Four Domain entities. Three map to contract schemas; `DocumentContent` is internal and never
crosses the wire.

```
Document        Id, OwnerUserId, OrgId, FolderId?, FileName, ContentType,
                SizeBytes, Visibility, PublicLinkToken?, CreatedAt, UpdatedAt
DocumentContent DocumentId (PK/FK, 1:1 with Document), Bytes
Folder          Id, OwnerUserId, ParentFolderId?, Name, CreatedAt, UpdatedAt
DocumentShare   Id, DocumentId, GrantedToUserId, GrantedByUserId, CreatedAt
```

Entity style follows `modules/auth`: `sealed class`, private parameterless constructor for EF
materialization only, `private set` properties, a static `Create(...)` factory, and `TimeProvider`
injected rather than `DateTimeOffset.UtcNow` called inline.

### Why `OrgId` is denormalized onto `Document`

`Organization` visibility means "any authenticated user in the *owner's* org". The Documents module
has no `users` table — it cannot join to find the owner's org. Copying `OrgId` off the `orgId` claim
at upload time makes the visibility check a single indexed predicate instead of a cross-module HTTP
call on every read. The cost is staleness if a user ever changes org; Auth has no operation that
does that today (one org per user, fixed at registration), so this holds until it doesn't. Repeated
under Open questions in file `04`.

### Why content is a separate table

`Document` rows are read constantly (every list, every metadata fetch). If `bytea` lived on the same
row, EF Core would drag megabytes through every one of those queries unless every single call site
remembered to project. A 1:1 `document_contents` table makes the expensive read explicit: only
`GET` and `PUT /documents/{id}/content` ever touch it.

Table names are snake_case and plural, set explicitly with `builder.ToTable("...")` in
`IEntityTypeConfiguration<T>` classes under `Infrastructure/Persistence/Configurations/`, matching
Auth. One `DocumentsDbContext`, wired with `ApplyConfigurationsFromAssembly`.

## Access rules (the one table everything else refers to)

`sub` = caller's user id, `orgId` = caller's org, both read from the bearer token.

| Caller | List | Read metadata | Download content | Modify (rename/move/delete/content/visibility/shares) |
|---|---|---|---|---|
| Owner | yes | yes | yes | yes |
| Explicit share grant | **no** | yes | yes | no |
| Same org, `Visibility = Organization` | yes | yes | yes | no |
| Same org, `Visibility = Private` or `PublicLink` | no | no (403) | no (403) | no (403) |
| Other org, no grant | no | no (403) | no (403) | no (403) |
| Anonymous | no | no (401) | only via `/public/documents/{token}/content`, and only while `Visibility = PublicLink` | no (401) |

Two rules that look like bugs and are not:

- **A share grant does not widen listing.** A grantee can fetch a document if they hold its id, but
  it never appears in their `GET /documents`. There is no "shared with me" view in v1 — see Open
  questions in file `04`.
- **`Visibility` and share grants are independent axes.** Narrowing visibility to `Private` does not
  revoke grants; widening it to `Organization` does not delete them.

**403 vs 404 leaks existence.** The contract distinguishes them on every `/documents/{id}` route, so
a caller can tell "exists but not yours" from "does not exist". That is the contract's choice,
accepted deliberately; do not collapse 403 into 404 without changing the contract first.

## Result and error mapping

Same shape as Auth: a `Result<T>` struct plus a `DocumentError` enum in
`TPXSoft.Documents.Domain/Common/`, mapped to HTTP in exactly one place
(`Api/Contracts/DocumentErrorMapper.cs`). `shared/TPXSoft.Shared.Kernel` still does not exist and is
not created for this — it gets built when a third module needs the same types, not speculatively
(the same reasoning already written on `AuthError`).

Starting set, extended by the individual feature files:

| `DocumentError` | HTTP | Message |
|---|---|---|
| `ValidationFailed` | 400 | Validation failed. |
| `Forbidden` | 403 | Caller is not allowed to perform this action on this document. |
| `NotFound` | 404 | No document with this id. |
| `FolderNotFound` | 404 | No folder with this id. |
| `FolderNotEmpty` | 409 | Folder is not empty. |
| `ShareAlreadyExists` | 409 | This user already has a share grant for this document. |
| `CycleDetected` | 400 | A folder cannot be moved into its own descendant. |

Every error body is `{"message": "..."}` (the `Error` schema). Messages stay generic on purpose:
they must not disclose whether a resource the caller cannot see exists.

## Configuration

Bound and validated with `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`,
like `JwtOptions` in Auth.

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:DocumentsDb` | — | Placeholder already in `appsettings.json`. |
| `Documents:MaxUploadBytes` | `26214400` (25 MiB) | Enforced in the handler *and* as the multipart body limit. |
| `Documents:ApplyMigrationsAtStartup` | `false` (`true` in Development) | Mirrors Auth. |

## Cross-cutting implementation notes

- **Auth wiring.** Every route except `/public/documents/{token}/content` gets
  `.RequireAuthorization()`. The Api project validates the same JWT Auth issues — same issuer,
  audience, and signing key, `MapInboundClaims = false` so `sub` is not rewritten to a claim-type
  URI. This module issues nothing. Reuse the shape of Auth's `ClaimsPrincipalExtensions.GetUserId()`
  as a Documents-local copy; the two Api projects do not reference each other.
- **`sizeBytes` is `long`** — `int64` in the contract, `bigint` in Postgres.
- **Timestamps** are `DateTimeOffset` in UTC, `timestamptz` in Postgres, always sourced from
  `TimeProvider` so tests can freeze them.
- **PATCH is tri-state.** `UpdateDocumentRequest.folderId` and `UpdateFolderRequest.parentFolderId`
  are nullable *and* optional: absent means "leave it alone", explicit `null` means "move to root".
  A plain `Guid?` property cannot tell those apart. Use a small `Patch<T>` struct with a
  `JsonConverter`, in `Api/Contracts/`:

  ```csharp
  public readonly struct Patch<T>
  {
      public bool IsSet { get; }
      public T? Value { get; }
      // The converter sets IsSet = true whenever the property is present in the payload,
      // including when its value is explicitly null.
  }
  ```

  Getting this wrong silently moves every renamed document to the root. It is the most likely bug in
  this module, and it gets an explicit test in files `03` and `07`.
- **Uploaded bytes are hostile input.** The file name, the content type, and the bytes themselves
  all come from the caller and are all echoed back to some other user later. The sanitization rules
  live in file `01`; the response-header rules live in file `05`. Neither set is optional.

## Verify

```bash
tpx verify documents
```

```bash
tpx test documents --integration
```

Integration tests need a live Docker daemon (Testcontainers). Read "Environment notes" in
[`modules/auth/CLAUDE.md`](../../auth/CLAUDE.md) before treating a red integration run as a real
regression.
