using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Services;

namespace TPXSoft.Documents.Api.Endpoints;

/// <summary>
/// The one anonymous route in the whole module
/// (documentation/05-preview-and-download.md's "Public route" section) -- deliberately its own
/// file rather than folded into DocumentEndpoints, and deliberately not `.RequireAuthorization()`'d,
/// so the missing auth requirement stays visible at the mapping call site instead of being one
/// easy-to-miss omission among nine `.RequireAuthorization()` calls next door.
/// </summary>
public static class PublicDocumentEndpoints
{
    public static IEndpointRouteBuilder MapPublicDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/public/documents/{token}/content", DownloadPublicDocumentContentAsync);

        return endpoints;
    }

    /// <summary>
    /// Looks the document up by token only (never by id -- the route has no id in it), and returns
    /// the exact same 404 body for every failure mode: unknown token, revoked link, deleted
    /// document, or a token whose document is no longer Visibility.PublicLink. Distinguishing any
    /// of those would be an oracle for probing tokens (documentation 05's "Public route" rule 3).
    /// </summary>
    private static async Task<IResult> DownloadPublicDocumentContentAsync(
        string token, HttpResponse response, DocumentService documentService, CancellationToken cancellationToken)
    {
        var result = await documentService.DownloadByPublicLinkAsync(token, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, message) = result.Error.ToHttp();
            return Results.Json(new ErrorResponse(message), statusCode: statusCode);
        }

        return DocumentContentResults.Build(response, result.Value);
    }
}
