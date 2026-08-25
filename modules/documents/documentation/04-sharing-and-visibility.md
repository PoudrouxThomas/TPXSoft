# 04 — Sharing: Private, Organization, PublicLink

Read [`README.md`](README.md) first — the access table there is the normative statement of these
rules; this file explains and implements it.

## Two independent axes

Sharing is not one setting. It is a **visibility mode** on the document plus a **set of per-user
grants**, and they do not interact:

```
Visibility (one of three)        DocumentShare rows (zero or more)
  Private                          grantedToUserId -> read access
  Organization                     … persists across every visibility change
  PublicLink
```

- Changing visibility never touches grants. Going `Organization` → `Private` does not revoke the
  three people you shared with; it only stops the *rest* of the org from seeing the document.
- Adding a grant never changes visibility. Sharing a `PublicLink` document with one colleague does
  not make it any more or less public.
- A grant is read-only access, always. There is no editor role in v1.

This is stated in the contract's `Visibility` description and in
[`modules/documents/CLAUDE.md`](../CLAUDE.md); it is the assumption most likely to be "fixed" into a
bug by someone who expects a single sharing switch.

### What each mode means

| Mode | Who can list it | Who can read it | Token |
|---|---|---|---|
| `Private` | owner only | owner + grantees | none |
| `Organization` | owner + everyone in the owner's org | owner + grantees + owner's org | none |
| `PublicLink` | **owner only** | owner + grantees + anyone holding the link | `publicLinkToken` set |

`PublicLink` is the mode people misread. It does **not** make the document discoverable to logged-in
users — a colleague in the same org gets `403` from `GET /documents/{id}` on a `PublicLink`
document. It only enables the unauthenticated
`GET /public/documents/{token}/content` route (file `05`). Public means "whoever holds the URL",
nothing else.

## `PUT /documents/{id}/visibility` — `setDocumentVisibility`

Body: `{"visibility": "Private" | "Organization" | "PublicLink"}`. Owner only.

| Transition | Effect on `PublicLinkToken` |
|---|---|
| → `PublicLink` | **(Re)generates** a fresh token, every time. |
| → `Private` | Cleared to null. |
| → `Organization` | Cleared to null. |

The re-generation rule has a sharp edge worth putting in the UI: setting `PublicLink` on a document
that is *already* `PublicLink` mints a new token and **breaks every link already handed out**. That
is the contract's wording ("(re)generates"), and it doubles as the only way to rotate a leaked link,
since there is no dedicated rotate endpoint. A client that re-sends the current visibility on every
save will silently invalidate links; it should send the request only when the value actually changes.

Going `PublicLink` → anything → `PublicLink` also produces a *different* token. Old links stay dead.

### Token generation and storage

- 32 bytes from `RandomNumberGenerator.GetBytes(32)`, base64url-encoded, no padding — 43
  characters, 256 bits of entropy. Same generator family as Auth's refresh tokens.
- **Stored raw, not hashed.** Auth hashes refresh tokens; this one deliberately does not, because
  `Document.publicLinkToken` is returned to the owner on every subsequent `GET /documents/{id}` so
  the UI can display the link. A hash could not be shown again. The safety argument is the same one
  written on Auth's `RefreshToken`: the value is CSPRNG output, not a guessable secret, and lookup
  must be deterministic.
- Unique partial index on `public_link_token` (see file `01`). Collisions are astronomically
  unlikely; the index is there so a bug cannot produce two documents behind one link.
- The token is serialized **only to the owner**. Strip it in the projection for every other caller
  (file `02`).

### Validation

| Rule | On failure |
|---|---|
| Document exists | 404 |
| Caller is the owner | 403 |
| `visibility` present and one of the three enum values | 400 |

An unknown string like `"public"` is `400`, not a silent fallback. Bind the enum with
`JsonStringEnumConverter` and reject unmapped values explicitly rather than letting it deserialize
to `0`.

`UpdatedAt` refreshes on any successful change.

## Per-user grants

### `POST /documents/{id}/shares` — `shareDocumentWithUser`

Body: `{"userId": "…"}`. Owner only. Returns `201` with the `DocumentShare`.

- `GrantedByUserId` is the caller, not a body field. It exists so a future audit view can answer
  "who shared this".
- **A second grant for the same user is `409`**, not an idempotent `201`. The contract says so; back
  it with a unique index on `(document_id, granted_to_user_id)` and translate the resulting
  `DbUpdateException` (Npgsql `SqlState == "23505"`) into `ShareAlreadyExists`. Do not rely on a
  check-then-insert alone — two concurrent requests would both pass the check.
- Sharing with **yourself** is `400`. The owner already has access, and a self-grant would survive
  an ownership change that v1 cannot perform anyway.
- The `userId` is **not verified to exist.** This module has no users table and no client call into
  Auth. A grant for a random uuid is accepted and simply never matches anyone's `sub`. See Open
  questions — this is the most defensible-looking wart in the module.

### `GET /documents/{id}/shares` — `listDocumentShares`

Owner only (`403` for anyone else, including the grantees themselves). Returns the grants ordered by
`created_at`.

### `DELETE /documents/{id}/shares/{userId}` — `revokeDocumentShare`

Owner only. **Idempotent by contract**: `204` whether or not a grant existed. Deleting a share the
caller never created is not an error, and the response must not reveal whether one was there.

Note the asymmetry with `DELETE /documents/{id}` (file `03`), which returns `404` on a repeat. Both
behaviors are deliberate and taken straight from the contract; Auth's `POST /auth/logout` has the
same idempotent shape and the same "this is documented, not a bug" note.

## Persistence

```
document_shares
  id                 uuid PK
  document_id        uuid not null, FK -> documents(id) ON DELETE CASCADE
  granted_to_user_id uuid not null
  granted_by_user_id uuid not null
  created_at         timestamptz not null

  unique index (document_id, granted_to_user_id)
  index (granted_to_user_id)   -- for the "shared with me" view, once it exists
```

The lookup on every read path is `EXISTS (SELECT 1 FROM document_shares WHERE document_id = @id AND
granted_to_user_id = @sub)`. Run it only when the cheaper checks (owner, org-visible) have already
failed — `DocumentAccess.Evaluate` in file `02` takes the result as a parameter precisely so the
call site controls when that query happens.

## Errors

| Case | Status | Body |
|---|---|---|
| Unknown visibility value, self-share, missing `userId` | 400 | `{"message": "Validation failed."}` |
| Caller is not the owner | 403 | `{"message": "Caller is not the owner."}` |
| Unknown document | 404 | `{"message": "No document with this id."}` |
| Duplicate grant | 409 | `{"message": "This user already has a share grant for this document."}` |

## Tests

Unit:

- `SetVisibility(PublicLink)` generates a token; calling it twice yields two different tokens.
- `SetVisibility(Private)` and `SetVisibility(Organization)` both clear the token.
- Visibility changes never touch the grant collection.
- Generated tokens are 43 characters, URL-safe, and distinct across 1000 draws.

Integration:

- Owner sets `Organization` → a same-org colleague now lists and reads it; a different-org user
  still gets `403`.
- Owner sets `PublicLink` → the token route serves the bytes; a same-org colleague gets `403` from
  `GET /documents/{id}`.
- Owner sets `PublicLink` twice → the first token now returns `404`, the second works.
- Owner sets `Private` after `PublicLink` → the token route returns `404`.
- Grant to Bob, then set `Private` → Bob still gets `200` on `GET /documents/{id}` (grants survive).
- Grant to Bob twice → `409`.
- Grant to self → `400`.
- Bob (grantee) calls `GET /documents/{id}/shares` → `403`.
- Revoke a grant that exists → `204`, Bob then gets `403`. Revoke again → still `204`.
- A non-owner calling `PUT .../visibility` → `403` and the visibility is unchanged.

## Open questions

- **Grantee user ids are never validated.** With no Auth client wired, `POST .../shares` accepts any
  uuid. The fix is `wire-module documents auth` plus a `GET /users/{id}` (or a batch existence
  route) on Auth — neither exists yet. Until then, the UI must supply ids it got from Auth, and a
  typo produces a grant that silently does nothing.
- **No "shared with me" list.** Grants do not widen `GET /documents`, so a grantee needs someone to
  send them the id. The `(granted_to_user_id)` index above is already there for the endpoint that
  should exist: `GET /documents?sharedWithMe=true`. Needs a contract change.
- **`Document.OrgId` is a snapshot.** It is copied from the uploader's `orgId` claim (`README.md`).
  If Auth ever grows org transfers, existing documents keep pointing at the old org and
  org-visibility silently follows the document, not the user.
- **No expiring or password-protected links.** A public link lives until the visibility changes or
  the document is deleted. Adding `expiresAt` to the token, or requiring a passphrase, is the usual
  next request from anyone who has used SharePoint.
- **No notification on share.** Nothing tells Bob a document was shared with him. That belongs to a
  future notifications module, not here.
- **No edit-level grants.** `DocumentShare` has no role column. Adding one later means a `Role`
  column defaulting to `Reader` and a re-read of every `Owner`-only check in files `03`–`06`.
