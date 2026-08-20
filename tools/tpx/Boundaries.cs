using System.Text.RegularExpressions;

namespace Tpx;

/// <summary>Enforces that a module only reaches another module through shared/clients or Shared.Kernel
/// (CLAUDE.md "Module boundaries"), by scanning ProjectReference entries in every module's .csproj files.</summary>
internal static partial class Boundaries
{
    public readonly record struct Violation(string Project, string ReferencedPath);

    public static List<Violation> Check()
    {
        var violations = new List<Violation>();
        var modules = Modules.ListModules().ToList();

        foreach (var module in modules)
        {
            var moduleDir = Path.Combine(Modules.ModulesDir, module);
            foreach (var csproj in Directory.GetFiles(moduleDir, "*.csproj", SearchOption.AllDirectories))
            {
                var xml = File.ReadAllText(csproj);
                foreach (Match match in ProjectReferencePattern().Matches(xml))
                {
                    var referencedPath = match.Groups[1].Value.Replace('\\', '/');

                    var crossesIntoOtherModule = modules
                        .Where(other => other != module)
                        .Any(other => referencedPath.Contains($"/modules/{other}/", StringComparison.OrdinalIgnoreCase));

                    var touchesForbiddenLayer =
                        referencedPath.Contains(".Domain", StringComparison.OrdinalIgnoreCase) ||
                        referencedPath.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase);

                    if (crossesIntoOtherModule && touchesForbiddenLayer)
                        violations.Add(new Violation(Path.GetRelativePath(Modules.RepoRoot, csproj), referencedPath));
                }
            }
        }

        return violations;
    }

    [GeneratedRegex(@"<ProjectReference\s+Include=""([^""]+)""")]
    private static partial Regex ProjectReferencePattern();
}
