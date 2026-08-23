using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TPXSoft.Auth.Mcp;

/// <summary>Tools that shell out to the harness's own commands (tpx, dotnet-ef) instead of
/// reimplementing verification or migration inspection.</summary>
[McpServerToolType]
public static class OperationsTools
{
    [McpServerTool(Name = "run_tests"), Description("Runs 'tpx verify auth' (build + unit tests + contract lint). tpx does not support a test-name filter today, so 'filter' is accepted but only echoed back, not applied.")]
    public static async Task<string> RunTests(
        [Description("Optional test name filter -- not currently supported by tpx, echoed back only.")] string? filter = null)
    {
        var (exitCode, output) = await ProcessRunner.RunAsync("tpx", "verify auth", RepoPaths.RepoRoot);

        var note = string.IsNullOrWhiteSpace(filter)
            ? string.Empty
            : $"\n(Note: filter '{filter}' was requested but tpx has no filter flag, so it was not applied -- the full suite ran.)";

        return exitCode == 0
            ? $"tpx verify auth: PASSED\n{output}{note}"
            : $"tpx verify auth: FAILED (exit {exitCode})\n{output}{note}";
    }

    [McpServerTool(Name = "get_migrations_status"), Description("Lists EF Core migrations defined for the Auth module (dotnet-ef migrations list --no-connect -- doesn't require a live database).")]
    public static async Task<string> GetMigrationsStatus()
    {
        if (!File.Exists(RepoPaths.InfrastructureProject))
            return "TPXSoft.Auth.Infrastructure.csproj not found -- nothing to inspect.";

        var (exitCode, output) = await ProcessRunner.RunAsync(
            "dotnet-ef",
            $"migrations list --no-connect -p \"{RepoPaths.InfrastructureProject}\" -s \"{RepoPaths.InfrastructureProject}\"",
            RepoPaths.RepoRoot);

        return exitCode == 0
            ? output
            : $"dotnet-ef migrations list failed (exit {exitCode}) -- is the dotnet-ef tool installed? (dotnet tool install --global dotnet-ef)\n{output}";
    }
}
