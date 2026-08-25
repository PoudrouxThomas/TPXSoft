using System.Security.Claims;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.Domain.Services;

namespace TPXSoft.Documents.Api.Endpoints;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/folders", CreateFolderAsync).RequireAuthorization();
        endpoints.MapGet("/folders", ListFoldersAsync).RequireAuthorization();
        endpoints.MapGet("/folders/{id:guid}", GetFolderAsync).RequireAuthorization();
        endpoints.MapPatch("/folders/{id:guid}", UpdateFolderAsync).RequireAuthorization();
        endpoints.MapDelete("/folders/{id:guid}", DeleteFolderAsync).RequireAuthorization();
        endpoints.MapGet("/folders/{id:guid}/children", ListFolderChildrenAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> CreateFolderAsync(
        CreateFolderRequest request, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var result = await folderService.CreateAsync(userId.Value, request.Name, request.ParentFolderId, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        return Results.Json(ToResponse(result.Value), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListFoldersAsync(
        Guid? parentFolderId, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var folders = await folderService.ListAsync(userId.Value, parentFolderId, cancellationToken);
        return Results.Ok(folders.Select(ToResponse));
    }

    private static async Task<IResult> GetFolderAsync(
        Guid id, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var result = await folderService.GetAsync(userId.Value, id, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateFolderAsync(
        Guid id, UpdateFolderRequest request, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var result = await folderService.UpdateAsync(
            userId.Value,
            id,
            request.Name.IsSet,
            request.Name.Value,
            request.ParentFolderId.IsSet,
            request.ParentFolderId.Value,
            cancellationToken);

        return result.IsFailure ? ErrorResult(result.Error) : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> DeleteFolderAsync(
        Guid id, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var result = await folderService.DeleteAsync(userId.Value, id, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : Results.NoContent();
    }

    private static async Task<IResult> ListFolderChildrenAsync(
        Guid id, ClaimsPrincipal user, FolderService folderService, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return UnauthorizedResult();
        }

        var result = await folderService.GetChildFoldersAsync(userId.Value, id, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResult(result.Error);
        }

        // "documents" is always empty here -- the Document entity does not exist yet in this
        // module (see FolderChildrenResponse's own doc comment).
        var response = new FolderChildrenResponse(result.Value.Select(ToResponse).ToList(), Array.Empty<object>());
        return Results.Ok(response);
    }

    private static IResult UnauthorizedResult() =>
        Results.Json(new ErrorResponse("Missing or invalid access token."), statusCode: StatusCodes.Status401Unauthorized);

    private static IResult ErrorResult(DocumentError error)
    {
        var (statusCode, message) = error.ToHttp();
        return Results.Json(new ErrorResponse(message), statusCode: statusCode);
    }

    private static FolderResponse ToResponse(Folder folder) =>
        new(folder.Id, folder.OwnerUserId, folder.ParentFolderId, folder.Name, folder.CreatedAt, folder.UpdatedAt);
}
