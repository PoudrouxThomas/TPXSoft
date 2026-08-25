using System.Diagnostics;

namespace TPXSoft.Documents.Mcp;

/// <summary>Runs an external process and captures combined stdout/stderr, for tools that shell out
/// (run_tests -&gt; tpx, get_migrations_status -&gt; dotnet-ef) instead of reimplementing them.</summary>
internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, $"Failed to start '{fileName} {arguments}': {ex.Message}. Is '{fileName}' on PATH?");
        }

        if (process is null)
            return (-1, $"Failed to start '{fileName} {arguments}'.");

        using var _ = process;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = stderr.Length > 0 ? $"{stdout}\n{stderr}" : stdout;
        return (process.ExitCode, output.Trim());
    }
}
