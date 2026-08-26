using Microsoft.Extensions.Time.Testing;
using TPXSoft.Documents.Domain.Services;
using TPXSoft.Documents.UnitTests.TestDoubles;

namespace TPXSoft.Documents.UnitTests;

/// <summary>
/// Builds a <see cref="DocumentService"/> wired to fully in-memory fakes for every port it depends
/// on, so each test only names what it varies. Mirrors FolderServiceTestBuilder.
/// </summary>
internal sealed class DocumentServiceTestBuilder
{
    public FakeDocumentRepository DocumentRepository { get; } = new();

    public FakeFolderRepository FolderRepository { get; } = new();

    public FakeDocumentShareRepository DocumentShareRepository { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public DocumentService Build() =>
        new(DocumentRepository, FolderRepository, DocumentShareRepository, UnitOfWork, TimeProvider);
}
