using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.Api.Endpoints;

/// <summary>
/// Shared response-building for both content-download routes (authenticated and public-link) --
/// documentation/05-preview-and-download.md's header rules apply identically to both: the same
/// Content-Disposition (always `attachment`, never the client-controlled `contentType` echoed as
/// `inline`), `X-Content-Type-Options: nosniff`, and `Cache-Control: private, no-store`. Only the
/// authorization path leading here differs between the two callers.
/// </summary>
internal static class DocumentContentResults
{
    public static IResult Build(HttpResponse response, DocumentDownload download)
    {
        var document = download.Document;

        // Results.File's own fileDownloadName parameter is deliberately not used here -- it would
        // set its own Content-Disposition header via ContentDispositionHeaderValue, which this
        // module's own ContentDispositionHeaderBuilder (with its explicit CR/LF re-check) then
        // could not safely override without risking two conflicting header writes.
        response.Headers.ContentDisposition = ContentDispositionHeaderBuilder.BuildAttachment(document.FileName);
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers.CacheControl = "private, no-store";

        return Results.File(download.Content, document.ContentType);
    }
}
