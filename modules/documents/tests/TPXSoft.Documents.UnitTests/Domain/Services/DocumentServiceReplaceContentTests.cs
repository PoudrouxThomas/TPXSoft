using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises DocumentService.ReplaceContentAsync's orchestration (ownership,
/// validation, sanitization, and the two-write update) against in-memory fakes --
/// documentation/06-update-document-content.md is the spec for every case here.</summary>
public sealed class DocumentServiceReplaceContentTests
{
    private const long MaxUploadBytes = 26_214_400;

    private static readonly byte[] OriginalBytes = [1, 2, 3];

    private static readonly byte[] NewBytes = [9, 9, 9, 9];

    [Fact]
    public async Task ReplaceContentAsync_Owner_ReplacesBytesAndMetadata_NotAppended()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            owner, document.Id, "image/png", NewBytes.Length, NewBytes, MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Equal(NewBytes.Length, result.Value.SizeBytes);
        var storedContent = await builder.DocumentRepository.GetContentAsync(document.Id, CancellationToken.None);
        // Bytes are wholesale replaced, not appended -- the stored array must equal exactly the
        // new bytes, not the original bytes plus the new ones.
        Assert.Equal(NewBytes, storedContent!.Bytes);
        Assert.Equal(1, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ReplaceContentAsync_RefreshesUpdatedAt()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        var createdAt = document.UpdatedAt;
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();
        builder.TimeProvider.Advance(TimeSpan.FromMinutes(5));

        var result = await service.ReplaceContentAsync(
            owner, document.Id, "image/png", NewBytes.Length, NewBytes, MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.UpdatedAt > createdAt);
    }

    [Fact]
    public async Task ReplaceContentAsync_DoesNotMutateFileNameFolderIdVisibilityOrPublicLinkToken()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var document = Document.Create(
            owner, Guid.NewGuid(), folder.Id, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.PublicLink, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            owner, document.Id, "image/png", NewBytes.Length, NewBytes, MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("report.pdf", result.Value.FileName);
        Assert.Equal(folder.Id, result.Value.FolderId);
        Assert.Equal(Visibility.PublicLink, result.Value.Visibility);
    }

    [Fact]
    public async Task ReplaceContentAsync_UnknownDocument_ReturnsNotFound()
    {
        var service = new DocumentServiceTestBuilder().Build();

        var result = await service.ReplaceContentAsync(
            Guid.NewGuid(), Guid.NewGuid(), "image/png", NewBytes.Length, NewBytes, MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotFound, result.Error);
    }

    [Fact]
    public async Task ReplaceContentAsync_NonOwner_ReturnsNotOwner_BeforeValidatingBody()
    {
        // Order of checks matters: load-and-authorize before validating the body, same as
        // UpdateAsync -- a non-owner sending an empty file still gets 403, not 400 (doc 06's
        // "Validation" section: "Authorize before validating the body, same as file 03").
        var builder = new DocumentServiceTestBuilder();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            Guid.NewGuid(), document.Id, "image/png", fileLength: 0, [], MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
    }

    [Fact]
    public async Task ReplaceContentAsync_EmptyFile_ReturnsValidationFailed_AndDoesNotSave()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            owner, document.Id, "image/png", fileLength: 0, [], MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(OriginalBytes.Length, document.SizeBytes);
    }

    [Fact]
    public async Task ReplaceContentAsync_FileOverMaxUploadBytes_ReturnsValidationFailed_AndDoesNotSave()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            owner, document.Id, "image/png", fileLength: 17, new byte[17], maxUploadBytes: 16, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-media-type")]
    public async Task ReplaceContentAsync_BlankOrMalformedContentType_FallsBackToOctetStream(string? rawContentType)
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", OriginalBytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, OriginalBytes));
        var service = builder.Build();

        var result = await service.ReplaceContentAsync(
            owner, document.Id, rawContentType, NewBytes.Length, NewBytes, MaxUploadBytes, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("application/octet-stream", result.Value.ContentType);
    }
}
