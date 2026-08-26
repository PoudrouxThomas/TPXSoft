using System.Security.Claims;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.Domain.Services;

namespace TPXSoft.Documents.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/documents", ListDocumentsAsync).RequireAuthorization();
        endpoints.MapGet("/documents/{id:guid}", GetDocumentAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ListDocumentsAsync(
        Guid? folderId, bool? mine, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, orgId, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var documents = await documentService.ListAsync(userId!.Value, orgId!.Value, folderId, mine ?? false, cancellationToken);
        return Results.Ok(documents.Select(d => ToResponse(d, userId.Value)));
    }

    private static async Task<IResult> GetDocumentAsync(
        Guid id, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, orgId, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.GetAsync(userId!.Value, orgId!.Value, id, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value, userId.Value));
    }

    private static (Guid? UserId, Guid? OrgId, IResult? Unauthorized) GetCaller(ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var orgId = user.GetOrgId();
        return userId is null || orgId is null
            ? (userId, orgId, UnauthorizedResult())
            : (userId, orgId, null);
    }

    private static IResult UnauthorizedResult() =>
        Results.Json(new ErrorResponse("Missing or invalid access token."), statusCode: StatusCodes.Status401Unauthorized);

    private static IResult ErrorResult(DocumentError error)
    {
        var (statusCode, message) = error.ToHttp();
        return Results.Json(new ErrorResponse(message), statusCode: statusCode);
    }

    /// <summary>publicLinkToken is serialized only for the document's owner --
    /// documentation/02-virtual-folders.md.</summary>
    internal static DocumentResponse ToResponse(Document document, Guid callerUserId) => new(
        document.Id,
        document.OwnerUserId,
        document.FolderId,
        document.FileName,
        document.ContentType,
        document.SizeBytes,
        document.Visibility,
        document.OwnerUserId == callerUserId ? document.PublicLinkToken : null,
        document.CreatedAt,
        document.UpdatedAt);
}
