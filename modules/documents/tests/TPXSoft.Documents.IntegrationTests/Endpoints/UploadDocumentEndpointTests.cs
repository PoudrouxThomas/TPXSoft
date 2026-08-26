using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real POST /documents endpoint over HTTP against a real Postgres database.
/// Test list per documentation/01-upload-document.md's "Tests -> Integration" section.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UploadDocumentEndpointTests : DocumentsIntegrationTestBase
{
    // Mirrors Program.cs's ConfigureHttpJsonOptions -- the server serializes Visibility as a
    // string via JsonStringEnumConverter, but HttpContent.ReadFromJsonAsync's default options
    // don't know that unless told explicitly.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public UploadDocumentEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task UploadDocument_NoFolderId_ReturnsCreatedWithNullFolderId_AndBytesRoundTrip()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        var bytes = "hello world"u8.ToArray();

        using var response = await client.PostAsync("/documents", BuildForm("hello.txt", "text/plain", bytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.NotNull(body);
        Assert.Null(body!.FolderId);
        Assert.Equal("hello.txt", body.FileName);
        Assert.Equal("text/plain", body.ContentType);
        Assert.Equal(bytes.Length, body.SizeBytes);

        // Content bytes round-trip byte-for-byte -- there is no GET /documents/{id}/content yet
        // (feature 05), so read the document_contents row directly instead.
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == body.Id);
        Assert.Equal(bytes, content.Bytes);
    }

    [Fact]
    public async Task UploadDocument_IntoOwnedFolder_ReturnsCreatedWithFolderIdSet()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        var folder = await CreateFolderAsync(client, "Reports");

        using var response = await client.PostAsync(
            "/documents", BuildForm("report.pdf", "application/pdf", [1, 2, 3], folder.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal(folder.Id, body!.FolderId);
    }

    [Fact]
    public async Task UploadDocument_IntoAnotherUsersFolder_Returns400()
    {
        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid());
        var bobsFolder = await CreateFolderAsync(bobClient, "Bob's folder");

        using var aliceClient = CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await aliceClient.PostAsync(
            "/documents", BuildForm("report.pdf", "application/pdf", [1, 2, 3], bobsFolder.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_EmptyFilePart_Returns400()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.PostAsync("/documents", BuildForm("empty.txt", "text/plain", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_OneByteOverMaxUploadBytes_Returns400_NotAnUnhandled500()
    {
        // TestTokens/DocumentsWebApplicationFactory configures Documents:MaxUploadBytes to the
        // small value below for this test alone -- the production default (25 MiB) would make
        // this test upload tens of megabytes for no reason.
        await using var factory = new DocumentsWebApplicationFactory(ConnectionString, maxUploadBytes: 16);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.IssueFor(Guid.NewGuid()));

        using var response = await client.PostAsync("/documents", BuildForm("report.pdf", "application/pdf", new byte[17]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_NoAuthorizationHeader_Returns401()
    {
        using var client = Factory.CreateClient();

        using var response = await client.PostAsync("/documents", BuildForm("report.pdf", "application/pdf", [1, 2, 3]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_TwoUploadsSameFileNameSameFolder_BothSucceedWithDifferentIds()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        using var firstResponse = await client.PostAsync("/documents", BuildForm("report.pdf", "application/pdf", [1]));
        using var secondResponse = await client.PostAsync("/documents", BuildForm("report.pdf", "application/pdf", [2]));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.NotEqual(first!.Id, second!.Id);
    }

    private static MultipartFormDataContent BuildForm(string fileName, string contentType, byte[] bytes, Guid? folderId = null)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        if (folderId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "folderId");
        }

        return form;
    }

    private static async Task<FolderResponse> CreateFolderAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/folders", new { name, parentFolderId = (Guid?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderResponse>())!;
    }
}
