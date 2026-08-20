namespace Tpx;

/// <summary>Locates the repo root and discovers modules under modules/, as laid out in PLAN.md.</summary>
internal static class Modules
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ModulesDir => Path.Combine(RepoRoot, "modules");

    public static string ContractsDir => Path.Combine(RepoRoot, "contracts");

    public static IEnumerable<string> ListModules()
    {
        if (!Directory.Exists(ModulesDir))
            yield break;

        foreach (var dir in Directory.GetDirectories(ModulesDir))
            yield return Path.GetFileName(dir);
    }

    public static string? FindSolution(string module)
    {
        var moduleDir = Path.Combine(ModulesDir, module);
        return Directory.Exists(moduleDir)
            ? Directory.GetFiles(moduleDir, "*.sln").FirstOrDefault()
            : null;
    }

    public static string? FindContract(string module)
    {
        return Directory.Exists(ContractsDir)
            ? Directory.GetFiles(ContractsDir, $"{module}.v*.yaml").FirstOrDefault()
            : null;
    }

    // Walks up from the current directory looking for PLAN.md, the one file every
    // worktree checks out at its own root. Falls back to cwd if not found (e.g. tpx
    // run outside the repo).
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
