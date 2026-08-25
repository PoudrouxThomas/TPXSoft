# 02 — Documents live in virtual folders

Read [`README.md`](README.md) first. File `07` covers creating and editing folders themselves; this
file covers the *relationship* between a document and a folder, and the two read endpoints that
expose it.

## What "virtual" means

A folder is a row, not a directory. Nothing about it touches a filesystem, and a document's storage
location does not change when it moves — only its `folder_id` does. Consequences worth stating
because they drive the rules below:

- A move is a single-column update. It is cheap, and it cannot fail halfway.
- There is no path string anywhere. A document's location is a chain of `parent_folder_id` links
  resolved on demand; nothing stores `/Projects/2026/report.pdf`.
- **A folder tree belongs to exactly one user.** `Folder.OwnerUserId` is not nullable and folders are
  never shared in v1. Sharing operates on documents only (file `04`).
- `folder_id = null` means "the owner's root". Root is not a row; it is the absence of a parent.

The last two combine into the one genuinely surprising behavior in this module:

> An org-visible document owned by Alice and filed in Alice's `Q3 Reports` folder appears in Bob's
> `GET /documents` with `folderId` pointing at a folder Bob gets `403` on. Bob sees the document but
> cannot see, open, or browse its folder.

That is intentional for v1. Folders organize *your own* documents; visibility governs *documents*.
Clients should render an org-visible document from another user's tree as a flat entry, not attempt
to place it in a tree. Flagged again under Open questions.

## Endpoints

### `GET /documents` — `listDocuments`

| Query parameter | Type | Meaning |
|---|---|---|
| `folderId` | uuid, optional | Restrict to documents directly inside this folder. |
| `mine` | boolean, optional | When true, restrict to documents owned by the caller. |

Responses: `200` with an array of `Document`, `401`. There is deliberately no `403`/`404` here — see
"Unresolvable folderId" below.

The base set is the **List** column of the access table in `README.md`:

```
documents owned by sub
  UNION
documents where org_id = orgId AND visibility = 'Organization'
```

Explicit share grants add nothing to this set. `PublicLink` documents belong to their owner's own
listing (the owner still sees their own files) but never appear in anyone else's.

Applied on top:

- `mine=true` → drop the org-visible branch, leaving `owner_user_id = sub`.
- `folderId=X` → `AND folder_id = X`. Direct children only, never recursive.
- `folderId` absent → **no folder filter at all.** This returns everything visible to the caller
  across all folders, which is the "all my documents" view. It does *not* mean "root only". To list
  root, use `GET /folders/{id}/children`'s sibling behavior or pass an explicit filter once the
  contract supports it — see Open questions, because OpenAPI cannot express "the parameter was
  present with value null".

**Unresolvable `folderId`.** A folder that does not exist, or that belongs to another user, yields
an empty array with `200` — not `403`, not `404`. The route has no error responses for it, and
returning `200 []` also avoids probing for which folder ids exist. Same for a folder the caller owns
that is simply empty.

Ordering: `created_at DESC, id DESC`. `id` breaks ties so paging (when it arrives) is stable.

### `GET /documents/{id}` — `getDocument`

Returns one `Document`'s metadata. Never the bytes.

| Outcome | Status |
|---|---|
| Caller is owner, has a share grant, or is same-org on an `Organization` document | `200` |
| Document exists, none of the above | `403` |
| No such document | `404` |
| No or invalid token | `401` |

A `PublicLink` document is **not** readable here by a non-owner, even with a valid token and the
right id. Public means "reachable through the token route", nothing more. Someone holding a public
link gets the bytes from `/public/documents/{token}/content` and never learns the document id.

`publicLinkToken` is serialized **only for the owner**. Returning it to an org-visible reader would
hand them a permanent, unauthenticated link the owner never granted them.

## Access check shape

Both endpoints, plus every route in files `03`–`06`, resolve the same question. Put it in one place
in the Domain layer and call it everywhere:

```csharp
public enum DocumentAccess { None, Read, Owner }

// Owner  -> can modify
// Read   -> can fetch metadata and content, cannot modify
// None   -> 403 (or 404 when the document does not exist at all)
DocumentAccess Evaluate(Document document, Guid callerUserId, Guid callerOrgId, bool hasShareGrant);
```

Rules, in order:

1. `document.OwnerUserId == callerUserId` → `Owner`.
2. `hasShareGrant` → `Read`.
3. `document.Visibility == Organization && document.OrgId == callerOrgId` → `Read`.
4. Otherwise `None`.

Keeping this as a pure function over already-loaded state makes it unit-testable without a database
and makes the share-grant lookup an explicit, visible cost at each call site rather than a hidden
query inside the check.

## Query notes

- The union above is one query with an `OR`, not two round trips:
  `WHERE owner_user_id = @sub OR (org_id = @org AND visibility = 'Organization')`.
- Never `Include` the content navigation in these paths. Project straight to a DTO so
  `document_contents` is not touched; that separation is the whole reason the table is split
  (`README.md`).
- The `(owner_user_id, folder_id)` index serves the owned branch; the partial index on `org_id`
  serves the org branch.

## Errors

| Case | Status | Body |
|---|---|---|
| `GET /documents` with no or invalid token | 401 | `{"message": "Missing or invalid access token."}` |
| `GET /documents` with an unusable `folderId` | 200 | `[]` |
| `GET /documents/{id}`, visible | 200 | `Document` |
| `GET /documents/{id}`, exists but not visible | 403 | `{"message": "Caller cannot see this document."}` |
| `GET /documents/{id}`, unknown id | 404 | `{"message": "No document with this id."}` |

## Tests

Unit:

- `Evaluate` returns `Owner` for the owner regardless of visibility.
- `Evaluate` returns `Read` for a grantee on a `Private` document.
- `Evaluate` returns `Read` for a same-org caller only when visibility is `Organization`.
- `Evaluate` returns `None` for a same-org caller on `PublicLink`.
- `Evaluate` returns `None` for a different org with no grant.

Integration:

- Alice uploads two documents, one org-visible; Bob (same org) lists → sees only the org-visible one.
- Bob with `mine=true` → sees none of Alice's.
- Carol (different org) lists → sees neither.
- Alice's grantee Bob does **not** see the granted document in `GET /documents`, but does get `200`
  from `GET /documents/{id}` on it.
- `GET /documents?folderId=` an id owned by Bob, queried by Alice → `200 []`.
- `GET /documents?folderId=` an owned folder → only that folder's direct children, not a subfolder's.
- `GET /documents/{id}` on a `PublicLink` document by a same-org non-owner → `403`.
- `publicLinkToken` is present for the owner's `GET /documents/{id}` and absent from every other
  caller's view of the same document.

## Open questions

- **"Root only" is not expressible.** `?folderId=` cannot carry an explicit null through OpenAPI's
  query serialization, so there is no way to ask for "documents at my root". Options for v2: a
  `rootOnly=true` boolean, or a sentinel `folderId=00000000-0000-0000-0000-000000000000`. The
  boolean is cleaner; both need a contract change.
- **No pagination.** `listDocuments` and `listFolders` return unbounded arrays. This is fine for
  hundreds of documents and wrong for thousands. Adding `page`/`pageSize` plus an envelope is a
  breaking change to the response schema, so it should land before there are real consumers.
- **No sorting or filtering parameters.** No name search, no content-type filter, no date range.
  Postgres full-text search is one of the stated reasons the project chose Postgres (`CLAUDE.md`),
  so a search endpoint over `file_name` — and later document text — is the natural next feature.
- **Folders are not shareable.** An org-visible document in a private folder is visible while its
  folder is not, which clients have to render around. Sharing a whole folder means either cascading
  visibility to children or a `FolderShare` entity; both are v2 work.
