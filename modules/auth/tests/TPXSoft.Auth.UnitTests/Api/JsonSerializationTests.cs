using System.Text.Json;
using System.Text.Json.Serialization;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Domain.Common;

namespace TPXSoft.Auth.UnitTests.Api;

/// <summary>Serializes with the exact same JsonSerializerOptions setup as the
/// ConfigureHttpJsonOptions callback in TPXSoft.Auth.Api/Program.cs -- keep the two in sync.</summary>
public sealed class JsonSerializationTests
{
    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Theory]
    [InlineData(Role.Admin, "\"Admin\"")]
    [InlineData(Role.Member, "\"Member\"")]
    public void Role_SerializesToItsName_NotTheNumericEnumValue(Role role, string expectedJson)
    {
        var json = JsonSerializer.Serialize(role, CreateApiJsonOptions());

        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void UserResponse_SerializesToExactlyIdEmailOrgIdOrgNameRoleCreatedAt_NoExtraFields()
    {
        var response = new UserResponse(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email: "user@example.com",
            OrgId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OrgName: "Acme",
            Role: Role.Admin,
            CreatedAt: DateTimeOffset.Parse("2026-01-01T12:34:56Z"));

        var json = JsonSerializer.Serialize(response, CreateApiJsonOptions());
        using var document = JsonDocument.Parse(json);

        var propertyNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "id", "email", "orgId", "orgName", "role", "createdAt" }, propertyNames);

        Assert.Equal("11111111-1111-1111-1111-111111111111", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("user@example.com", document.RootElement.GetProperty("email").GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", document.RootElement.GetProperty("orgId").GetString());
        Assert.Equal("Acme", document.RootElement.GetProperty("orgName").GetString());
        Assert.Equal("Admin", document.RootElement.GetProperty("role").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T12:34:56Z"), document.RootElement.GetProperty("createdAt").GetDateTimeOffset());
    }
}
