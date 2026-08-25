namespace TPXSoft.Documents.Mcp;

/// <summary>Locates the repo root the same way tools/tpx/Modules.cs does (walk up looking for
/// PLAN.md), since this server can be launched from any working directory by an MCP client.</summary>
internal static class RepoPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ContractPath => Path.Combine(RepoRoot, "contracts", "documents.v1.yaml");

    public static string ModuleDir => Path.Combine(RepoRoot, "modules", "documents");

    public static string InfrastructureProject =>
        Path.Combine(ModuleDir, "src", "TPXSoft.Documents.Infrastructure", "TPXSoft.Documents.Infrastructure.csproj");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PLAN.md")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
