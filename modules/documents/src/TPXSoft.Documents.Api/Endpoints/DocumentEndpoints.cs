using System.Security.Claims;
using Microsoft.Extensions.Options;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.Domain.Services;
using TPXSoft.Documents.Infrastructure.Options;

namespace TPXSoft.Documents.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/documents", UploadDocumentAsync).RequireAuthorization();
        endpoints.MapGet("/documents", ListDocumentsAsync).RequireAuthorization();
        endpoints.MapGet("/documents/{id:guid}", GetDocumentAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Reads the multipart form by hand rather than declaring IFormFile/[FromForm] parameters --
    /// that keeps the whole ReadFormAsync call (and the exception it throws when
    /// FormOptions.MultipartBodyLengthLimit is exceeded) inside this method's own try/catch,
    /// instead of inside minimal API's argument-binding pipeline where it would surface as an
    /// unhandled 500 (documentation/01-upload-document.md's "Streaming vs buffering" section and
    /// its integration test "one byte over MaxUploadBytes -> 400, not 500").
    /// </summary>
    private static async Task<IResult> UploadDocumentAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        DocumentService documentService,
        IOptions<DocumentsOptions> documentsOptions,
        CancellationToken cancellationToken)
    {
        var (userId, orgId, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!request.HasFormContentType)
        {
            return ValidationFailedResult();
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            // Covers FormOptions.MultipartBodyLengthLimit being exceeded and other malformed
            // multipart bodies alike.
            return ValidationFailedResult();
        }

        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return ValidationFailedResult();
        }

        if (file.Length > documentsOptions.Value.MaxUploadBytes)
        {
            return ValidationFailedResult();
        }

        Guid? folderId = null;
        if (form.TryGetValue("folderId", out var folderIdValues))
        {
            var rawFolderId = folderIdValues.ToString();
            if (!string.IsNullOrEmpty(rawFolderId))
            {
                if (!Guid.TryParse(rawFolderId, out var parsedFolderId))
                {
                    return ValidationFailedResult();
                }

                folderId = parsedFolderId;
            }
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var result = await documentService.UploadAsync(
            userId!.Value,
            orgId!.Value,
            folderId,
            file.FileName,
            file.ContentType,
            file.Length,
            buffer.ToArray(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        return Results.Json(ToResponse(result.Value, userId.Value), statusCode: StatusCodes.Status201Created);
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

    private static IResult ValidationFailedResult() => ErrorResult(DocumentError.ValidationFailed);

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
