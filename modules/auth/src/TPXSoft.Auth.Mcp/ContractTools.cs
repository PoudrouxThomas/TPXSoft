using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using YamlDotNet.RepresentationModel;

namespace TPXSoft.Auth.Mcp;

/// <summary>
/// Structural queries against contracts/auth.v1.yaml -- the contract, not the .cs source, is the
/// answer for "what does this module look like", per PLAN.md §0.7 / modules/auth/CLAUDE.md.
/// </summary>
[McpServerToolType]
public static class ContractTools
{
    [McpServerTool(Name = "get_openapi"), Description("Returns the raw contents of contracts/auth.v1.yaml.")]
    public static string GetOpenApi()
    {
        if (!File.Exists(RepoPaths.ContractPath))
            return "No contract found at contracts/auth.v1.yaml.";

        return File.ReadAllText(RepoPaths.ContractPath);
    }

    [McpServerTool(Name = "list_endpoints"), Description("Lists every path/method/operationId/summary defined in contracts/auth.v1.yaml.")]
    public static string ListEndpoints()
    {
        var root = LoadContract();
        if (root is null)
            return "No contract found at contracts/auth.v1.yaml.";

        var paths = root.Children.TryGetValue(new YamlScalarNode("paths"), out var pathsNode)
            ? pathsNode as YamlMappingNode
            : null;

        if (paths is null || paths.Children.Count == 0)
            return "No paths defined in the contract yet.";

        var endpoints = new List<object>();
        foreach (var (pathKey, pathValue) in paths.Children)
        {
            var path = ((YamlScalarNode)pathKey).Value!;
            if (pathValue is not YamlMappingNode methods)
                continue;

            foreach (var (methodKey, methodValue) in methods.Children)
            {
                var method = ((YamlScalarNode)methodKey).Value!;
                var op = (YamlMappingNode)methodValue;
                endpoints.Add(new
                {
                    method = method.ToUpperInvariant(),
                    path,
                    operationId = ScalarOrNull(op, "operationId"),
                    summary = ScalarOrNull(op, "summary"),
                });
            }
        }

        return JsonSerializer.Serialize(endpoints, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "describe_entity"), Description("Describes a schema from contracts/auth.v1.yaml's components.schemas (e.g. 'User', 'TokenPair', 'Role').")]
    public static string DescribeEntity([Description("Schema name, e.g. User, TokenPair, RegisterRequest, Role")] string name)
    {
        var root = LoadContract();
        if (root is null)
            return "No contract found at contracts/auth.v1.yaml.";

        var schemas = GetPath(root, "components", "schemas") as YamlMappingNode;
        if (schemas is null)
            return "No components.schemas defined in the contract.";

        var match = schemas.Children.Keys
            .OfType<YamlScalarNode>()
            .FirstOrDefault(k => string.Equals(k.Value, name, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var known = string.Join(", ", schemas.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value));
            return $"No schema named '{name}' in contracts/auth.v1.yaml. Known schemas: {known}";
        }

        var schema = (YamlMappingNode)schemas.Children[match];
        return SerializeYamlNode(schema);
    }

    [McpServerTool(Name = "find_consumers"), Description("Greps shared/clients/**, other modules' src/**, and apps/**/src/** for references to an entity or field name, to find what would break if it changed.")]
    public static string FindConsumers([Description("Entity or field name to search for, e.g. User, orgId, refreshToken")] string entityOrField)
    {
        var repoRoot = RepoPaths.RepoRoot;
        var searchRoots = new List<string>();

        var sharedClients = Path.Combine(repoRoot, "shared", "clients");
        if (Directory.Exists(sharedClients))
            searchRoots.Add(sharedClients);

        var modulesDir = Path.Combine(repoRoot, "modules");
        if (Directory.Exists(modulesDir))
        {
            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                if (Path.GetFileName(moduleDir) == "auth")
                    continue; // auth's own source referencing itself isn't a "consumer"

                var src = Path.Combine(moduleDir, "src");
                if (Directory.Exists(src))
                    searchRoots.Add(src);
            }
        }

        var appsDir = Path.Combine(repoRoot, "apps");
        if (Directory.Exists(appsDir))
        {
            foreach (var appDir in Directory.GetDirectories(appsDir))
            foreach (var projectDir in Directory.GetDirectories(appDir))
            {
                var src = Path.Combine(projectDir, "src");
                if (Directory.Exists(src))
                    searchRoots.Add(src);
            }
        }

        if (searchRoots.Count == 0)
            return "No shared/clients/, no other modules, and no apps/ exist yet -- Auth is the first module, so it has no consumers to find.";

        var matches = new List<string>();
        foreach (var root in searchRoots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!IsSearchableSourceFile(file))
                    continue;

                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }

                if (content.Contains(entityOrField, StringComparison.OrdinalIgnoreCase))
                    matches.Add(Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));
            }
        }

        return matches.Count == 0
            ? $"No references to '{entityOrField}' found under shared/clients/ or other modules' src/."
            : string.Join('\n', matches);
    }

    private static readonly string[] SourceExtensions = { ".cs", ".ts" };

    private static bool IsSearchableSourceFile(string path)
    {
        if (!SourceExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return false;

        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !segments.Any(s => string.Equals(s, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static YamlMappingNode? LoadContract()
    {
        if (!File.Exists(RepoPaths.ContractPath))
            return null;

        using var reader = new StreamReader(RepoPaths.ContractPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return yaml.Documents.Count > 0 ? yaml.Documents[0].RootNode as YamlMappingNode : null;
    }

    private static YamlNode? GetPath(YamlMappingNode root, params string[] keys)
    {
        YamlNode current = root;
        foreach (var key in keys)
        {
            if (current is not YamlMappingNode map || !map.Children.TryGetValue(new YamlScalarNode(key), out var next))
                return null;
            current = next;
        }

        return current;
    }

    private static string? ScalarOrNull(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;

    private static string SerializeYamlNode(YamlNode node)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder);
        new YamlStream(new YamlDocument(node)).Save(writer, assignAnchors: false);
        return builder.ToString();
    }
}
