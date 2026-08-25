# 06 — Update a document's content

Read [`README.md`](README.md) first. This feature is deliberately small, and most of its weight is
in what it must *not* change.

## Endpoint

`PUT /documents/{id}/content` — `replaceDocumentContent`, `multipart/form-data` with a single
required `file` part. **Owner only.** Returns `200` with the updated `Document`.

`PUT` and not `PATCH`: the bytes are replaced wholesale. There is no partial or delta update, no
chunked/resumable upload, and no append.

## What changes and what does not

| Field | After a successful replace |
|---|---|
| `id` | unchanged — the whole point of the route |
| `ownerUserId`, `orgId` | unchanged |
| `folderId` | unchanged |
| `fileName` | **unchanged** — see below |
| `contentType` | **updated** from the new part |
| `sizeBytes` | updated |
| `visibility`, `publicLinkToken` | unchanged |
| share grants | unchanged |
| `createdAt` | unchanged |
| `updatedAt` | refreshed |

Two of these are decisions rather than obvious consequences:

- **`fileName` is not taken from the uploaded part.** The contract's `requestBody` carries only
  `file`, and the summary says the document keeps its identity. A user replacing `report.pdf` with a
  local file named `report-final-v3.pdf` keeps the name `report.pdf`. Renaming is `PATCH
  /documents/{id}` (file `03`); a client that wants both does two calls.
- **`contentType` *is* taken from the new part**, because it describes the bytes and would otherwise
  be a lie. Same sanitization as upload: valid media type or `application/octet-stream`, capped at
  128 characters.

Sharing state surviving a replace is what makes this route useful — and is also its sharpest edge:

> Everyone who could read the old bytes can read the new bytes, immediately and without notice. A
> live public link now serves different content. This is the intended behavior; it is worth a
> confirmation prompt in any UI that shows a document is shared.

## Validation

| Rule | On failure |
|---|---|
| Document exists | 404 |
| Caller is the owner | 403 |
| `file` part present | 400 |
| `file` length > 0 | 400 |
| `file` length ≤ `Documents:MaxUploadBytes` | 400 |

Authorize before validating the body, same as file `03`: a non-owner gets `403` regardless of what
they sent.

A grantee cannot replace content. A same-org colleague cannot replace content on an
`Organization` document. Read access is read access — `DocumentAccess.Owner` is required here.

## Implementation

```csharp
// document already loaded and authorized
document.ReplaceContent(newContentType, file.Length, timeProvider);   // metadata, Domain
await contentRepository.ReplaceAsync(document.Id, bytes, ct);          // bytea, Infrastructure
await unitOfWork.SaveChangesAsync(ct);
```

- Both writes go through the **same transaction / unit of work**. A crash between them would leave
  `sizeBytes` describing bytes that are not there; a mismatched `sizeBytes` is not detectable later
  because nothing re-derives it.
- Update the existing `document_contents` row in place (`UPDATE … SET bytes = @b`). Do not
  delete-then-insert: the delete would cascade nothing here, but it churns the row for no benefit.
- Keep `SizeBytes` from the actual byte count written, not from a client-supplied header.
- Streaming/buffering tradeoff is identical to upload (file `01`) — the whole file is buffered,
  bounded by `MaxUploadBytes`, with `FormOptions.MultipartBodyLengthLimit` set to the same value.

### Concurrency

Two owners' sessions replacing the same document race, and the last writer wins silently. There is
no `If-Match`/ETag on this route in the contract and no `rowversion` column on `Document`.

For v1 that is accepted — the owner is a single user, and the realistic conflict is one person with
two tabs. It is listed under Open questions because the fix (an ETag from `updatedAt`, a `412
Precondition Failed`) is cheap and is a contract change.

## No version history

Replacing content **overwrites**. There is no previous version, no restore, no diff. This is stated
in the contract's description and in [`modules/documents/CLAUDE.md`](../CLAUDE.md); it is the single
biggest scope cut in the module and the one a "SharePoint clone" is most obviously missing.

Its absence is why `GOALS.md` Phase 2's line mentions versioning — that item is not satisfied by
this route, and should not be checked off by implementing it.

Implementing versions later means: a `document_versions` table (`document_id`, `version_number`,
`bytes`, `size_bytes`, `content_type`, `created_at`, `created_by`), this route appending instead of
overwriting, `GET /documents/{id}/versions`, and a decision on whether a public link points at "the
current version" or a pinned one. Storage grows linearly with edits, so a retention policy comes
with it. None of that is v1.

## Errors

| Case | Status | Body |
|---|---|---|
| Missing, empty, or oversized file | 400 | `{"message": "Validation failed."}` |
| Caller is not the owner | 403 | `{"message": "Caller is not the owner."}` |
| Unknown document | 404 | `{"message": "No document with this id."}` |
| Missing/invalid token | 401 | `{"message": "Missing or invalid access token."}` |

## Tests

Unit:

- `ReplaceContent` updates `ContentType`, `SizeBytes`, and `UpdatedAt`.
- `ReplaceContent` leaves `FileName`, `FolderId`, `Visibility`, `PublicLinkToken`, and `CreatedAt`
  untouched.
- A blank or malformed new content type falls back to `application/octet-stream`.

Integration:

- Owner replaces content → `200`; a subsequent `GET /documents/{id}/content` returns the **new**
  bytes; `sizeBytes` matches the new length.
- The document keeps its `fileName` even though the uploaded part had a different one.
- Replace on a shared document → the grantee downloads the new bytes with no re-grant.
- Replace on a `PublicLink` document → the **same** token now serves the new bytes (the link is not
  rotated).
- Replace with an empty file → `400`, and the old bytes are still intact.
- Replace one byte over the limit → `400`, old bytes intact.
- Grantee attempts a replace → `403`, bytes unchanged.
- Same-org colleague attempts a replace on an `Organization` document → `403`.
- Unknown id → `404`. No token → `401`.
- Replace twice in a row → the second set of bytes wins and `createdAt` never moved.

## Open questions

- **No version history.** Described above. The largest known gap in the module.
- **No optimistic concurrency.** Add `ETag` on `GET /documents/{id}` (derived from `updatedAt`),
  honor `If-Match` on this route and on `PATCH /documents/{id}`, and define `412` in the contract.
- **No integrity hash.** Storing a SHA-256 of the bytes would let clients verify a download and would
  make "did this actually change?" answerable. It also enables dedupe if storage ever hurts.
- **No virus/malware scanning.** Bytes go in and come back out untouched. Any real deployment wants a
  scanner between upload and availability, which implies a pending state on `Document` that the
  contract has no field for.
