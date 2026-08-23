using System.Text.Json;
using System.Text.Json.Serialization;

namespace TPXSoft.Auth.IntegrationTests.Support;

/// <summary>Mirrors the ConfigureHttpJsonOptions callback in TPXSoft.Auth.Api/Program.cs so
/// response bodies (in particular the Role enum) deserialize the same way a real client would
/// need to configure itself. HttpClient's ReadFromJsonAsync doesn't automatically inherit the
/// server's JsonOptions.</summary>
internal static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Instance = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
