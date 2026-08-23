using System.Diagnostics;

namespace Tpx;

/// <summary>Runs external processes (dotnet, git, docker, nswag, ...) with the repo root as the default working directory.</summary>
internal static class Shell
{
    public static int Run(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = Start(fileName, arguments, workingDirectory, redirect: false);
        process.WaitForExit();
        return process.ExitCode;
    }

    public static (int ExitCode, string Output) Capture(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = Start(fileName, arguments, workingDirectory, redirect: true);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }

    public static bool Exists(string fileName)
    {
        var probe = OperatingSystem.IsWindows() ? "where" : "which";
        var (exitCode, _) = Capture(probe, fileName);
        return exitCode == 0;
    }

    private static Process Start(string fileName, string arguments, string? workingDirectory, bool redirect)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory ?? Modules.RepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
        };

        return Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'.");
    }
}
