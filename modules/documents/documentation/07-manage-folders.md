# 07 — Manage folders

Read [`README.md`](README.md) first, and file `02` for how documents relate to folders. This file
covers the folder tree itself. Build it before the document features — `folderId` is an input to
upload.

## Endpoints

| Endpoint | operationId | Access |
|---|---|---|
| `POST /folders` | `createFolder` | authenticated; owner is the caller |
| `GET /folders` | `listFolders` | caller's folders only |
| `GET /folders/{id}` | `getFolder` | owner only |
| `PATCH /folders/{id}` | `updateFolder` | owner only |
| `DELETE /folders/{id}` | `deleteFolder` | owner only, empty only |
| `GET /folders/{id}/children` | `listFolderChildren` | owner only |

**Every folder route is owner-only.** Folders are private organizational structure; there is no
folder sharing, no org-visible folder, and no `Visibility` column on `Folder`. Visibility lives on
documents (file `04`).

## The tree

`Folder.ParentFolderId` is a self-referencing nullable FK. `null` means the folder sits at the
owner's root. Root is not a row — there is no synthetic root folder per user, and no
`00000000-…` sentinel.

Depth is unbounded and unvalidated in v1. Nothing recursive runs on read (children are always one
level), so a deep tree costs nothing server-side; the only recursion is the cycle check on move.

Sibling names are **not** unique. `createFolder` defines no `409`, so two folders named `Reports`
may share a parent — the same rule documents follow (file `01`). Clients disambiguate by id.

## `POST /folders` — create

```jsonc
{ "name": "Q3 Reports", "parentFolderId": "0d4d…" }   // parentFolderId optional/nullable
```

| Rule | On failure |
|---|---|
| `name` present, non-empty after trimming | 400 |
| `name` ≤ 255 characters, no control characters | 400 |
| `parentFolderId`, when non-null, exists | 404 |
| `parentFolderId`, when non-null, is owned by the caller | 404 |

Note the last row: `createFolder` defines `400`, `401`, and `404` — but **no `403`**. So a parent
owned by someone else is reported as `404`, matching the contract's own wording ("parentFolderId
does not exist") and leaking nothing. The other folder routes do define `403` and use it.

Folder names are display strings, not path segments — `..` and `/` are legal characters in a name
and need no stripping, because no code ever concatenates them into a path. Control characters are
still rejected (they break clients and logs).

`OwnerUserId` comes from `sub`; there is no way to create a folder for someone else.

## `GET /folders` — list

Optional `parentFolderId` query parameter. Always scoped to `owner_user_id = sub`.

- `parentFolderId=X` → direct children of X, one level.
- Absent → **all** of the caller's folders, flat, every level. Clients build the tree from the
  `parentFolderId` values. That is the intended way to render a sidebar in one request.
- A parent that does not exist or is not the caller's → `200 []`, same reasoning as `GET /documents`
  in file `02` (the route defines no `403`/`404`).
- "Root folders only" is not expressible for the same reason as in file `02` — an explicit null
  cannot be sent through a query parameter. Listing everything and filtering `parentFolderId == null`
  client-side is the v1 answer.

Ordering: `name ASC, id ASC`.

## `GET /folders/{id}` and `GET /folders/{id}/children`

`getFolder` returns the folder; `403` if it exists but belongs to someone else, `404` if unknown.

`listFolderChildren` returns `FolderChildren`:

```jsonc
{ "folders": [ /* direct subfolders */ ], "documents": [ /* documents directly inside */ ] }
```

One level only, and **owner-only** — unlike `GET /documents?folderId=`, which any caller may issue
against their own visible set. Practical consequence: the `documents` array here is the owner's own
documents in that folder, including `Private` and `PublicLink` ones, because only the owner can ask.

Two queries, not a join. Do not `Include` document content.

## `PATCH /folders/{id}` — rename and/or move

```jsonc
{ "name": "Archive", "parentFolderId": null }   // both optional; null = move to root
```

The tri-state rule from `README.md` applies exactly as in file `03`: `{"name": "x"}` must **not**
move the folder to root. Bind through `Patch<T>` and branch on `IsSet`. This is the same bug in a
second place, and it needs its own test.

| Rule | On failure |
|---|---|
| Folder exists | 404 |
| Caller is the owner | 403 |
| When `name` is set: non-empty, ≤ 255, no control characters | 400 |
| When `parentFolderId` is set and non-null: exists | 404 |
| When `parentFolderId` is set and non-null: owned by the caller | 403 |
| New parent is not the folder itself | 400 |
| New parent is not a descendant of the folder | 400 |

### Cycle detection

Moving a folder into its own subtree would orphan that subtree into a detached ring: it would
disappear from every listing (nothing links back to root) while still existing. The contract calls
this out explicitly as a `400` case.

Walk **upward** from the proposed new parent to root and fail if the folder being moved is
encountered. Upward is O(depth) with a bounded loop; a downward walk is O(subtree). Add a hard
iteration cap (say 256) that throws rather than looping forever if data ever does become cyclic:

```sql
WITH RECURSIVE ancestors(id, parent_folder_id, depth) AS (
    SELECT id, parent_folder_id, 0 FROM folders WHERE id = @newParentId
    UNION ALL
    SELECT f.id, f.parent_folder_id, a.depth + 1
    FROM folders f JOIN ancestors a ON f.id = a.parent_folder_id
    WHERE a.depth < 256
)
SELECT EXISTS (SELECT 1 FROM ancestors WHERE id = @folderBeingMovedId);
```

`true` → `CycleDetected` → `400`. `self == newParent` is caught by the same query (depth 0), but
check it first anyway for a clearer failure.

A move never touches the documents inside the folder — their `folder_id` still points at the same
folder, which now hangs elsewhere. Nothing cascades and no visibility changes.

`UpdatedAt` refreshes on any successful change.

## `DELETE /folders/{id}` — delete, empty only

| Outcome | Status |
|---|---|
| Deleted | `204` |
| Contains any subfolder or any document | `409` |
| Exists, not the caller's | `403` |
| Unknown | `404` |

"Empty" means **no direct children of either kind**. Check both before deleting:

```sql
SELECT EXISTS (SELECT 1 FROM folders    WHERE parent_folder_id = @id)
    OR EXISTS (SELECT 1 FROM documents  WHERE folder_id        = @id);
```

The database backs this up regardless of the service check — `documents.folder_id` and
`folders.parent_folder_id` are both `ON DELETE RESTRICT`, so a race that slips past the check
surfaces as a foreign-key violation. Catch Npgsql `SqlState == "23503"` and map it to
`FolderNotEmpty` → `409` rather than letting it become a 500.

No recursive delete in v1: emptying a tree is the client's job, leaf-first. Like
`DELETE /documents/{id}` (file `03`) and unlike share revocation (file `04`), this is **not**
idempotent — a repeat returns `404`.

## Persistence

```
folders
  id               uuid PK
  owner_user_id    uuid not null
  parent_folder_id uuid null, FK -> folders(id) ON DELETE RESTRICT
  name             varchar(255) not null
  created_at       timestamptz not null
  updated_at       timestamptz not null

  index (owner_user_id, parent_folder_id)
```

The composite index serves both `GET /folders` (with and without a parent) and the emptiness check.
No unique index on `(owner_user_id, parent_folder_id, name)` — duplicates are legal, as above.

## Errors

| Case | Status | Body |
|---|---|---|
| Empty name, cycle, self-parent | 400 | `{"message": "Validation failed."}` / `{"message": "A folder cannot be moved into its own descendant."}` |
| Folder exists but is not the caller's; target parent not the caller's (PATCH only) | 403 | `{"message": "Caller is not the owner."}` |
| Unknown folder; unknown parent | 404 | `{"message": "No folder with this id."}` |
| Non-empty on delete | 409 | `{"message": "Folder is not empty."}` |
| Missing/invalid token | 401 | `{"message": "Missing or invalid access token."}` |

Watch the asymmetry: a foreign parent is `404` on `createFolder` (no `403` defined there) and `403`
on `updateFolder`. It looks inconsistent because it is — it follows each operation's declared
response set, which is the contract's decision, not the handler's.

## Tests

Unit:

- `Folder.Create` sets `CreatedAt == UpdatedAt` from a frozen `TimeProvider`.
- `Rename` rejects empty/whitespace and refreshes `UpdatedAt`.
- Cycle check: moving A under A, under its child, and under its grandchild all fail; moving A under
  an unrelated sibling succeeds.
- Depth cap terminates on a synthetic cyclic chain instead of hanging.

Integration:

- Create at root → `201`, `parentFolderId` null. Create under an owned folder → `201`.
- Create under another user's folder → `404`.
- Create with `""` or whitespace-only name → `400`.
- Two folders with the same name under the same parent → both `201`.
- `GET /folders` returns every level flat; with `parentFolderId` returns one level.
- `GET /folders?parentFolderId=` another user's folder → `200 []`.
- `GET /folders/{id}` on another user's folder → `403`; unknown → `404`.
- `GET /folders/{id}/children` returns direct subfolders and direct documents, and excludes
  grandchildren.
- **`PATCH {"name": "x"}` on a nested folder leaves `parentFolderId` unchanged** (tri-state
  regression test).
- `PATCH {"parentFolderId": null}` moves a nested folder to root.
- `PATCH` moving a folder into its own child → `400`; into itself → `400`.
- `DELETE` an empty folder → `204`; one containing a document → `409`; one containing a subfolder →
  `409`; a second `DELETE` → `404`; another user's → `403`.
- After deleting a folder, documents that were moved out of it beforehand are unaffected.

## Open questions

- **No recursive delete.** Deleting a populated tree is N client calls. A `?recursive=true` flag
  would need to cascade into documents — which means deleting content and revoking public links as a
  side effect of a folder operation. That is a decision, not a flag; hence the deliberate `409`.
- **No "root only" listing.** Same OpenAPI limitation as file `02`; a `rootOnly=true` parameter
  would fix both.
- **No path resolution endpoint.** Rendering a breadcrumb means walking `parentFolderId` client-side
  or fetching the flat list. A `GET /folders/{id}/path` returning the ancestor chain would be cheap
  (the recursive CTE above already exists) and is the most likely first addition here.
- **No folder sharing.** Stated in file `02` as the reason an org-visible document can point at an
  invisible folder. Solving it means either a `FolderShare` entity or cascading visibility to
  children — both v2.
- **No move-with-contents guarantees under concurrency.** Moving a folder while another session
  uploads into it is safe (the `folder_id` does not change), but there is no locking anywhere in
  this module. Fine at v1 scale.
