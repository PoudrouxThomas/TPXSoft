# 03 — Rename, move, delete a document

Read [`README.md`](README.md) first, especially the `Patch<T>` note — it is the crux of this file.

## Endpoints

- `PATCH /documents/{id}` — `updateDocument`. Renames and/or moves. **Owner only.**
- `DELETE /documents/{id}` — `deleteDocument`. **Owner only.**

Content is replaced through `PUT /documents/{id}/content` (file `06`) and visibility through
`PUT /documents/{id}/visibility` (file `04`). Neither is reachable from this route — a `PATCH` body
carrying `visibility` or `contentType` is ignored, not rejected, because those properties are simply
absent from `UpdateDocumentRequest`.

## Rename and move — `PATCH /documents/{id}`

```jsonc
// UpdateDocumentRequest — both properties optional
{
  "fileName": "Q3 report.pdf",
  "folderId": "0d4d…"   // or null for "move to root"
}
```

### The tri-state rule

| Payload | Meaning |
|---|---|
| `{"fileName": "x"}` | Rename only. Folder untouched. |
| `{"folderId": "…"}` | Move only. Name untouched. |
| `{"folderId": null}` | Move to the caller's root. |
| `{}` | No-op. Still `200` with the unchanged document. |
| `{"fileName": "x", "folderId": null}` | Rename *and* move to root. |

A bound `Guid? FolderId` gives `null` for both row 1 and row 3, which turns every rename into a move
to root. Bind through the `Patch<T>` struct from `README.md` and branch on `IsSet`:

```csharp
if (request.FileName.IsSet) { /* validate + apply */ }
if (request.FolderId.IsSet) { /* validate target (may be null) + apply */ }
```

The same trap exists in `PATCH /folders/{id}` (file `07`). Both need the explicit test listed below.

### Validation

| Rule | On failure |
|---|---|
| Caller is the owner | 403 |
| Document exists | 404 |
| When `fileName` is set: non-empty after the sanitization from file `01` | 400 |
| When `fileName` is set: ≤ 255 characters after sanitization | 400 (truncation is upload-only) |
| When `folderId` is set and non-null: folder exists | 404 |
| When `folderId` is set and non-null: folder is owned by the caller | 403 |

`fileName` sanitization is identical to upload's — same helper, same path-segment stripping and
control-character rules. A rename is a chance to inject a header-breaking name just as much as an
upload is, and the value flows into the same `Content-Disposition` (file `05`).

Unlike upload, a bad `folderId` here can be honest: `updateDocument` defines both `403` and `404`,
so a missing folder is `404` and a foreign folder is `403`.

Duplicate names remain legal; renaming a document to match a sibling is not a conflict.

### On success

- Apply only the set fields.
- Set `UpdatedAt = TimeProvider.GetUtcNow()`. `CreatedAt` never changes.
- `SizeBytes`, `ContentType`, `Visibility`, `PublicLinkToken`, and every share grant are untouched.
  **A move never changes who can see a document** — folders carry no permissions.
- Return `200` with the full updated `Document`.

## Delete — `DELETE /documents/{id}`

Owner only. Returns `204` with no body, `403` for a non-owner, `404` for an unknown id, `401`
unauthenticated.

It is a hard delete: the row is gone, and with it

- the `document_contents` row (`ON DELETE CASCADE`),
- every `document_shares` row for that document (`ON DELETE CASCADE`),
- the public link, if any — the token dies with the row, so outstanding links start returning `404`
  from `/public/documents/{token}/content`.

No recycle bin, no soft delete, no `deleted_at` column in v1. Deleting an org-visible document
removes it from every colleague's list with no notice; deleting one that is shared revokes the
grants silently. Both are consequences worth surfacing in the client's confirmation dialog.

**Delete is not idempotent here.** A second `DELETE` on the same id returns `404`, because the
contract defines `404` for this route. (Contrast `DELETE /documents/{id}/shares/{userId}` in file
`04`, which the contract *does* define as idempotent.) Do not "helpfully" return `204` for an
already-deleted document.

Concurrency: two simultaneous deletes race, and the loser's `SaveChangesAsync` throws
`DbUpdateConcurrencyException`. Catch it and return `404` — the row is gone either way, which is
what the caller needed to know.

## Errors

| Case | Status | Body |
|---|---|---|
| Validation failed (name, empty rename) | 400 | `{"message": "Validation failed."}` |
| Not the owner, or target folder not owned | 403 | `{"message": "Caller is not the owner."}` |
| Unknown document, or unknown target folder | 404 | `{"message": "No document with this id."}` / `{"message": "No folder with this id."}` |
| Missing/invalid token | 401 | `{"message": "Missing or invalid access token."}` |

Order of checks matters: **load and authorize the document first**, then validate the body. A
non-owner sending a malformed payload must get `403`, not `400` — otherwise the response tells them
their payload reached a real document.

## Tests

Unit:

- `Document.Rename` rejects an empty/whitespace name and refreshes `UpdatedAt`.
- `Document.MoveTo(null)` sets `FolderId` to null; `MoveTo(id)` sets it.
- Neither mutates `Visibility`, `PublicLinkToken`, `SizeBytes`, or `CreatedAt`.

Integration:

- **`{"fileName": "new.txt"}` on a document inside a folder leaves `folderId` unchanged.** This is
  the tri-state regression test; it fails against a naive `Guid?` binding.
- `{"folderId": null}` moves a filed document to root.
- `{}` returns `200` and an unchanged document.
- Moving into another user's folder → `403`; into an unknown folder → `404`.
- A non-owner with a share grant sending `PATCH` → `403` (grants are read-only).
- A same-org caller on an `Organization` document sending `PATCH` → `403`.
- Rename to a name already used by a sibling → `200`.
- `DELETE` by the owner → `204`; the row, its content row, and its shares are all gone; a second
  `DELETE` → `404`.
- `DELETE` of a `PublicLink` document → the public token route then returns `404`.
- `DELETE` by a grantee → `403` and the document survives.

## Open questions

- **No soft delete / trash.** A Microsoft-suite clone would be expected to have a recycle bin with a
  retention window. Adding it later means a `deleted_at` column, an `IsDeleted` filter on every
  query (EF global query filter), restore/purge endpoints, and a decision about whether public links
  break immediately or on purge. Worth doing before the module has real data.
- **No move-and-copy.** There is no "duplicate document" operation; a client wanting one must
  download and re-upload, which doubles the bytes over the wire.
- **No bulk operations.** Moving 200 documents into a folder is 200 requests. A `PATCH /documents`
  batch route would need its own partial-success semantics.
- **No audit trail.** Nothing records who deleted what, or when a document moved. `DocumentShare`
  keeps `GrantedByUserId`, so the module already half-cares about provenance; deletions keep nothing.
