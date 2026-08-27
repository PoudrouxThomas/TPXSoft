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
        // On Windows, PATH-resolved npm CLIs (nswag is a native .exe, but ng-openapi-gen and
        // similar are .cmd shims) can't be launched directly via Process.Start with
        // UseShellExecute=false -- only cmd.exe knows how to resolve a bare command name to
        // its .cmd/.bat/.exe candidates. Route through cmd.exe /c on Windows so both kinds work.
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {fileName} {arguments}")
            : new ProcessStartInfo(fileName, arguments);

        psi.WorkingDirectory = workingDirectory ?? Modules.RepoRoot;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = redirect;
        psi.RedirectStandardError = redirect;

        return Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'.");
    }
}
