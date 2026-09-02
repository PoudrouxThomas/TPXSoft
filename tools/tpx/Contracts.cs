namespace Tpx;

/// <summary>contract lint (OpenAPI structural check + breaking-change diff vs main) and gen
/// (regenerate shared/clients from contracts/), per PLAN.md 0.1.</summary>
internal static class Contracts
{
    public static int LintAll()
    {
        if (!Directory.Exists(Modules.ContractsDir) || Directory.GetFiles(Modules.ContractsDir, "*.yaml").Length == 0)
        {
            Console.WriteLine("tpx contract lint: no contracts under contracts/*.yaml — nothing to lint");
            return 0;
        }

        var ok = true;
        foreach (var file in Directory.GetFiles(Modules.ContractsDir, "*.yaml"))
            ok &= Lint(file);

        return ok ? 0 : 1;
    }

    public static bool Lint(string contractPath)
    {
        var relPath = Path.GetRelativePath(Modules.RepoRoot, contractPath).Replace('\\', '/');
        var lines = File.ReadAllLines(contractPath);

        var missing = new[] { "openapi:", "info:", "paths:" }
            .Where(key => !lines.Any(line => line.TrimStart().StartsWith(key, StringComparison.Ordinal)))
            .ToList();

        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"tpx contract lint: {relPath} missing top-level key(s): {string.Join(", ", missing)}");
            return false;
        }

        if (!Shell.Exists("oasdiff"))
        {
            Console.Error.WriteLine($"tpx contract lint: {relPath} structurally valid, but 'oasdiff' is not installed — cannot check for breaking changes vs main. Install it (https://github.com/oasdiff/oasdiff#installation) rather than skip this check.");
            return false;
        }

        var (showExitCode, mainContent) = Shell.Capture("git", $"show main:{relPath}");
        if (showExitCode != 0)
        {
            Console.WriteLine($"tpx contract lint: {relPath} structurally valid (no version on main yet — skipping breaking-change check)");
            return true;
        }

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, mainContent);
            var (diffExitCode, diffOutput) = Shell.Capture("oasdiff", $"breaking \"{tempFile}\" \"{contractPath}\" --fail-on ERR");
            if (diffExitCode != 0)
            {
                Console.Error.WriteLine($"tpx contract lint: breaking change in {relPath} vs main:\n{diffOutput}");
                return false;
            }
        }
        finally
        {
            File.Delete(tempFile);
        }

        Console.WriteLine($"tpx contract lint: {relPath} valid, no breaking change vs main");
        return true;
    }

    public static int Gen()
    {
        if (!Directory.Exists(Modules.ContractsDir) || Directory.GetFiles(Modules.ContractsDir, "*.yaml").Length == 0)
        {
            Console.WriteLine("tpx gen: no contracts under contracts/*.yaml — nothing to generate");
            return 0;
        }

        var ranAnything = false;

        foreach (var nswagConfig in Directory.GetFiles(Modules.ModulesDir, "nswag.json", SearchOption.AllDirectories))
        {
            if (!Shell.Exists("nswag"))
            {
                Console.Error.WriteLine($"tpx gen: found {nswagConfig} but 'nswag' CLI is not installed (dotnet tool install -g NSwag.ConsoleCore)");
                continue;
            }

            Console.WriteLine($"tpx gen: nswag run \"{nswagConfig}\"");
            Shell.Run("nswag", $"run \"{nswagConfig}\"");
            ranAnything = true;
        }

        var angularClientsDir = Path.Combine(Modules.RepoRoot, "shared", "clients", "angular");
        if (Directory.Exists(angularClientsDir))
        {
            foreach (var ngConfig in Directory.GetFiles(angularClientsDir, "ng-openapi-gen.json", SearchOption.AllDirectories))
            {
                if (!Shell.Exists("ng-openapi-gen"))
                {
                    Console.Error.WriteLine($"tpx gen: found {ngConfig} but 'ng-openapi-gen' is not installed (npm i -g ng-openapi-gen)");
                    continue;
                }

                Console.WriteLine($"tpx gen: ng-openapi-gen -c \"{ngConfig}\"");
                Shell.Run("ng-openapi-gen", $"-c \"{ngConfig}\"");
                ranAnything = true;
            }
        }

        if (!ranAnything)
            Console.WriteLine("tpx gen: no nswag.json / ng-openapi-gen.json config found yet under modules/ or shared/clients/angular/ — nothing to regenerate");

        return 0;
    }
}
