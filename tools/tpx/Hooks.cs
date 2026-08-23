using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tpx;

/// <summary>Bodies for the Claude Code hooks wired in .claude/settings.json. C# instead of the
/// old pwsh scripts so hooks fire the same on Windows and Linux (cloud sessions have no pwsh).</summary>
internal static class Hooks
{
    public static int Dispatch(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("tpx hook: expected 'block-generated', 'verify-on-save', or 'stop-verify'");
            return 1;
        }

        return rest[0] switch
        {
            "block-generated" => BlockGenerated(),
            "verify-on-save" => VerifyOnSave(),
            "stop-verify" => Commands.Dispatch(["verify", "--affected"]),
            var name => Unknown(name),
        };
    }

    private static int Unknown(string name)
    {
        Console.Error.WriteLine($"tpx hook: unknown hook '{name}'");
        return 1;
    }

    // PreToolUse (Edit/Write). Blocks edits under **/clients/** or **/generated/**.
    // Contract-first rule: change contracts/<module>.vN.yaml and run `tpx gen` instead.
    private static int BlockGenerated()
    {
        var filePath = ReadFilePath();
        if (string.IsNullOrEmpty(filePath))
            return 0;

        var normalized = filePath.Replace('\\', '/');
        if (Regex.IsMatch(normalized, "(^|/)clients/") || Regex.IsMatch(normalized, "(^|/)generated/"))
        {
            Console.Error.WriteLine($"Blocked: '{filePath}' is generated. Edit the contract in contracts/ and run 'tpx gen' instead of hand-editing generated output.");
            return 2;
        }

        return 0;
    }

    // PostToolUse (Edit/Write). Fast per-file check: build the touched .NET project, or
    // type-check the touched Angular project. Direct dotnet/tsc calls on purpose -- this is
    // the fast per-save loop, distinct from `tpx verify --affected` at Stop.
    private static int VerifyOnSave()
    {
        var filePath = ReadFilePath();
        if (string.IsNullOrEmpty(filePath))
            return 0;

        if (filePath.EndsWith(".cs", StringComparison.Ordinal))
        {
            var projDir = FindAncestor(filePath, "*.csproj");
            if (projDir is not null)
            {
                var (exitCode, output) = Shell.Capture("dotnet", "build --nologo -v quiet", projDir);
                if (exitCode != 0)
                {
                    Console.Error.WriteLine(output);
                    return 2;
                }
            }
        }
        else if (filePath.EndsWith(".ts", StringComparison.Ordinal))
        {
            var projDir = FindAncestor(filePath, "tsconfig.json");
            if (projDir is not null)
            {
                var (exitCode, output) = Shell.Capture("npx", "tsc --noEmit", projDir);
                if (exitCode != 0)
                {
                    Console.Error.WriteLine(output);
                    return 2;
                }
            }
        }

        return 0;
    }

    private static string? ReadFilePath()
    {
        var input = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(input))
            return null;

        using var doc = JsonDocument.Parse(input);
        return doc.RootElement.TryGetProperty("tool_input", out var toolInput)
            && toolInput.TryGetProperty("file_path", out var filePath)
            ? filePath.GetString()
            : null;
    }

    private static string? FindAncestor(string startPath, string pattern)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(startPath));
        while (dir is not null && Directory.Exists(dir))
        {
            if (Directory.GetFiles(dir, pattern).Length > 0)
                return dir;

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir)
                break;
            dir = parent;
        }

        return null;
    }
}
