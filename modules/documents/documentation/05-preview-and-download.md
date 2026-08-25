# 05 — Preview or download a document

Read [`README.md`](README.md) first. This is the only feature that serves attacker-supplied bytes
back to a browser, so the header rules below are requirements, not suggestions.

## Endpoints

- `GET /documents/{id}/content` — `downloadDocumentContent`. Bearer token required.
- `GET /public/documents/{token}/content` — `downloadPublicDocumentContent`. **Unauthenticated** —
  the only such route in the contract.

Both return `200` with `application/octet-stream` in the contract's schema and the document's stored
`contentType` as the actual `Content-Type` header.

## Preview vs download

The contract has **one** content route and no way to ask for inline rendering. That is the gap in
this feature: "preview" and "download" are the same request today, and the difference lives entirely
in the `Content-Disposition` header the server chooses.

**v1 behavior: always `attachment`.** Every response from both routes carries

```
Content-Disposition: attachment; filename="report.pdf"; filename*=UTF-8''report.pdf
X-Content-Type-Options: nosniff
Cache-Control: private, no-store
```

A client that wants to *preview* fetches the same URL with its bearer token, reads the response as a
`Blob`, and renders it from an object URL (`<img>`, `<embed>`, `pdf.js`, a text viewer). This works
for every type, keeps the bytes out of a top-level navigation, and — because the response is
`attachment` — a hostile HTML or SVG upload cannot execute in the application's origin even if the
user pastes the URL into the address bar.

The alternative (`?disposition=inline`) is the real preview feature and needs a contract change; it
is spelled out under Open questions with the conditions that make it safe. Do not add `inline`
support without those conditions.

### Why this matters

`Document.ContentType` is a string the uploader chose (file `01`). If the server echoed it with
`Content-Disposition: inline`, a user could upload `payload.html` declaring `text/html`, share the
public link, and have it execute as a same-origin page — reading the app's cookies and localStorage.
`attachment` plus `nosniff` closes that; so does serving from a separate origin. v1 does the former
because it needs no infrastructure.

### Filename encoding

Use both `filename=` (ASCII-folded, quotes and backslashes escaped) and `filename*=UTF-8''…`
(percent-encoded, RFC 5987) so non-ASCII names survive. A raw CR or LF in the value is a response-
splitting bug — that is what the control-character stripping in file `01` prevents, and it is
re-checked here rather than trusted, because rows predating a sanitization fix would still be in the
database.

## Authenticated route — `GET /documents/{id}/content`

Authorization is exactly `DocumentAccess.Evaluate` from file `02`: owner, grantee, or same-org on an
`Organization` document. `Read` is enough — content access is not owner-only.

| Outcome | Status |
|---|---|
| Access `Read` or `Owner` | `200` + bytes |
| Document exists, access `None` | `403` |
| Unknown id | `404` |
| No/invalid token | `401` |

A `PublicLink` document is **not** readable here by a non-owner. Public access goes through the
token route and nowhere else.

## Public route — `GET /public/documents/{token}/content`

The one anonymous route. Rules:

1. Look the document up **by token**, never by id. The route has no id in it, and the caller must
   never learn one.
2. Serve only if `Visibility == PublicLink`. If the owner has since switched to `Private` or
   `Organization`, the token column is null and the lookup finds nothing anyway — but assert the
   visibility explicitly rather than relying on that, so a bug that leaves a stale token does not
   become a leak.
3. **`404` for every failure.** Unknown token, revoked link, deleted document, malformed token — all
   `404` with `{"message": "No document with this token."}`. The contract defines no other status
   for this route, and any distinction here is an oracle for probing tokens.
4. Compare the token in constant time if the lookup is ever done in memory. With a database index
   lookup this is moot, which is another reason to keep it a plain indexed query.
5. Do not log the token. It *is* the credential. Scrub it from request-path logging for this route.
6. Consider a rate limit keyed on the remote address (ASP.NET's built-in
   `AddRateLimiter`/`RequireRateLimiting`) — the only unauthenticated route in the module is the one
   worth brute-force protecting, even though 256-bit tokens make success implausible.

Response headers are the same as the authenticated route, plus no `Set-Cookie` and no auth
challenge. `Cache-Control: private, no-store` still applies: a shared link is not public content in
the CDN sense, and caching it in an intermediary would outlive revocation.

## Serving the bytes

```csharp
var content = await db.DocumentContents
    .AsNoTracking()
    .Where(c => c.DocumentId == id)
    .Select(c => c.Bytes)
    .SingleOrDefaultAsync(ct);

return Results.File(content, document.ContentType, fileDownloadName: document.FileName);
```

`Results.File(byte[], …)` sets `Content-Length` and handles `HEAD` and range requests for free. Set
`Content-Disposition` explicitly afterwards if `fileDownloadName`'s encoding is not what the rules
above require, and always add `X-Content-Type-Options: nosniff`.

The whole file is materialized in memory — the same tradeoff as upload (file `01`), bounded by the
same 25 MiB cap. If that stops being acceptable, the move is `NpgsqlLargeObjectStream` or an
external store, and both content endpoints change together.

`AsNoTracking` and a projection straight to `byte[]`: the content entity is never tracked and never
loaded on any other path.

## Errors

| Case | Status | Body |
|---|---|---|
| Authenticated route, no access | 403 | `{"message": "Caller cannot see this document."}` |
| Authenticated route, unknown id | 404 | `{"message": "No document with this id."}` |
| Authenticated route, no token | 401 | `{"message": "Missing or invalid access token."}` |
| Public route, anything wrong | 404 | `{"message": "No document with this token."}` |

## Tests

Unit:

- Content-Disposition builder: ASCII name quoted correctly; non-ASCII name emits a valid
  `filename*=UTF-8''` form; a name containing `"`/`\` is escaped; a name containing CR or LF is
  rejected outright.

Integration:

- Owner downloads → `200`, bytes identical to the upload, `Content-Type` equals the stored value,
  `Content-Disposition` starts with `attachment`, `X-Content-Type-Options: nosniff` present.
- Grantee downloads a `Private` document → `200`.
- Same-org colleague downloads an `Organization` document → `200`; a `Private` one → `403`.
- Different-org user → `403`. No token → `401`. Unknown id → `404`.
- Same-org colleague on a `PublicLink` document via the authenticated route → `403`.
- Public route with a valid token, **no `Authorization` header** → `200` + bytes.
- Public route after the owner switches to `Private` → `404`.
- Public route after the owner re-sets `PublicLink` (token rotated) with the old token → `404`.
- Public route after the document is deleted → `404`.
- Public route with a garbage token → `404`, and the timing/body is indistinguishable from a valid
  token on a `Private` document.
- Upload an HTML file, download it, assert the response is `attachment` + `nosniff` (the stored-XSS
  regression test).

## Open questions

- **Inline preview needs a contract change.** The intended shape is a query parameter on
  `downloadDocumentContent`: `?disposition=inline|attachment`, default `attachment`. `inline` may be
  honored **only** when the stored content type is on a short allowlist —
  `application/pdf`, `image/png`, `image/jpeg`, `image/gif`, `image/webp`, `text/plain` — and the
  response must still carry `nosniff`. `image/svg+xml` and `text/html` are never inline-able from
  the app's own origin. Anything off the allowlist falls back to `attachment` rather than erroring.
  The same parameter must **not** be added to the public route unless previews move to a separate
  origin first.
- **No thumbnails.** A file list with previews wants a small rendered image per document. That means
  a rendering pipeline (image resize, PDF first-page raster), a `document_thumbnails` table, and a
  background job — a feature of its own, not a flag on this one.
- **No text extraction.** Postgres full-text search over document *contents* (a stated reason for
  choosing Postgres) needs extracted text stored alongside the bytes. Same pipeline as thumbnails.
- **No range/resumable download semantics beyond what `Results.File` gives.** Fine at 25 MiB.
- **No download counter or access log.** "Who opened my public link" is unanswerable. A
  `document_access_events` table would answer it and would also give the public route something to
  rate-limit on more intelligently than raw IP.
