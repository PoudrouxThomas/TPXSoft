namespace TPXSoft.Auth.Mcp;

/// <summary>Locates the repo root the same way tools/tpx/Modules.cs does (walk up looking for
/// PLAN.md), since this server can be launched from any working directory by an MCP client.</summary>
internal static class RepoPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ContractPath => Path.Combine(RepoRoot, "contracts", "auth.v1.yaml");

    public static string ModuleDir => Path.Combine(RepoRoot, "modules", "auth");

    public static string InfrastructureProject =>
        Path.Combine(ModuleDir, "src", "TPXSoft.Auth.Infrastructure", "TPXSoft.Auth.Infrastructure.csproj");

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
