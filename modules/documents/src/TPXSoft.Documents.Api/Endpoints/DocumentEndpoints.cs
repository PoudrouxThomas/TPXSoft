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
        endpoints.MapPatch("/documents/{id:guid}", UpdateDocumentAsync).RequireAuthorization();
        endpoints.MapDelete("/documents/{id:guid}", DeleteDocumentAsync).RequireAuthorization();
        endpoints.MapPut("/documents/{id:guid}/content", ReplaceDocumentContentAsync).RequireAuthorization();
        endpoints.MapPut("/documents/{id:guid}/visibility", SetDocumentVisibilityAsync).RequireAuthorization();
        endpoints.MapGet("/documents/{id:guid}/shares", ListDocumentSharesAsync).RequireAuthorization();
        endpoints.MapPost("/documents/{id:guid}/shares", ShareDocumentWithUserAsync).RequireAuthorization();
        endpoints.MapDelete("/documents/{id:guid}/shares/{userId:guid}", RevokeDocumentShareAsync).RequireAuthorization();

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

    private static async Task<IResult> UpdateDocumentAsync(
        Guid id, UpdateDocumentRequest request, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.UpdateAsync(
            userId!.Value,
            id,
            request.FileName.IsSet,
            request.FileName.Value,
            request.FolderId.IsSet,
            request.FolderId.Value,
            cancellationToken);

        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value, userId.Value));
    }

    private static async Task<IResult> DeleteDocumentAsync(
        Guid id, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.DeleteAsync(userId!.Value, id, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.NoContent();
    }

    /// <summary>
    /// Reads the multipart form by hand, same shape as UploadDocumentAsync, but defers every
    /// body-related failure (missing part, empty part, oversized part) to DocumentService --
    /// authorize-before-validate-body means a non-owner must get 403 regardless of what they sent
    /// (documentation/06-update-document-content.md's "Validation" section). A structurally
    /// malformed multipart body (not HasFormContentType, or ReadFormAsync throwing
    /// InvalidDataException because FormOptions.MultipartBodyLengthLimit was exceeded) is treated
    /// the same as "no file part" rather than short-circuited early -- it carries no document
    /// state to leak, so deferring it is free and keeps a single code path.
    /// </summary>
    private static async Task<IResult> ReplaceDocumentContentAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        DocumentService documentService,
        IOptions<DocumentsOptions> documentsOptions,
        CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        IFormFile? file = null;
        if (request.HasFormContentType)
        {
            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                file = form.Files["file"];
            }
            catch (InvalidDataException)
            {
                file = null;
            }
        }

        var content = Array.Empty<byte>();
        if (file is { Length: > 0 })
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var result = await documentService.ReplaceContentAsync(
            userId!.Value,
            id,
            file?.ContentType,
            file?.Length ?? 0,
            content,
            documentsOptions.Value.MaxUploadBytes,
            cancellationToken);

        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value, userId.Value));
    }

    private static async Task<IResult> SetDocumentVisibilityAsync(
        Guid id, SetVisibilityRequest request, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.SetVisibilityAsync(userId!.Value, id, request.Visibility, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value, userId.Value));
    }

    private static async Task<IResult> ListDocumentSharesAsync(
        Guid id, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.ListSharesAsync(userId!.Value, id, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(result.Value.Select(ToShareResponse));
    }

    private static async Task<IResult> ShareDocumentWithUserAsync(
        Guid id, ShareDocumentRequest request, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (userId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.ShareAsync(userId!.Value, id, request.UserId, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        return Results.Json(ToShareResponse(result.Value), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RevokeDocumentShareAsync(
        Guid id, Guid userId, ClaimsPrincipal user, DocumentService documentService, CancellationToken cancellationToken)
    {
        var (callerUserId, _, unauthorized) = GetCaller(user);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var result = await documentService.RevokeShareAsync(callerUserId!.Value, id, userId, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.NoContent();
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

    private static DocumentShareResponse ToShareResponse(DocumentShare share) =>
        new(share.Id, share.DocumentId, share.GrantedToUserId, share.GrantedByUserId, share.CreatedAt);
}
