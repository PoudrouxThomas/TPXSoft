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
            // Exit 2, not 1: Claude Code only *blocks* a Stop on exit 2, and only then feeds
            // the hook's stderr back to the agent as the reason to keep working. Exit 1 is a
            // non-blocking warning the agent never sees — which made this gate decorative.
            "stop-verify" => Commands.Dispatch(["verify", "--affected"]) == 0 ? 0 : 2,
            var name => Unknown(name),
        };
    }

    private static int Unknown(string name)
    {
        Console.Error.WriteLine($"tpx hook: unknown hook '{name}'");
        return 1;
    }

    // PreToolUse (Edit/Write/Bash). Blocks writes under shared/clients/** or **/generated/**.
    // Contract-first rule: change contracts/<module>.vN.yaml and run `tpx gen` instead.
    private static int BlockGenerated()
    {
        var toolInput = ReadToolInput();
        if (toolInput is null)
            return 0;

        // Edit/Write hand us the path directly.
        var filePath = ReadString(toolInput.Value, "file_path");
        if (!string.IsNullOrEmpty(filePath))
            return IsGeneratedPath(filePath) ? Refuse(filePath) : 0;

        // Bash: the same write, laundered through a shell. Matching only Edit|Write left this
        // guard bypassable by `sed -i`, `cp`, or a heredoc — and an agent told to prefer Bash
        // bypasses it without ever intending to.
        var command = ReadString(toolInput.Value, "command");
        if (string.IsNullOrEmpty(command) || !MutatesFiles(command))
            return 0;

        // Deliberately biased toward blocking: a read piped to a file (`grep x
        // shared/clients/y.cs > out`) trips this too. A false block costs one retry and the
        // message says what to do; a false pass silently corrupts generated output.
        var target = ShellTokens(command).FirstOrDefault(IsGeneratedPath);
        return target is not null ? Refuse(target) : 0;
    }

    private static int Refuse(string path)
    {
        Console.Error.WriteLine($"Blocked: '{path}' is generated. Edit the contract in contracts/ and run 'tpx gen' instead of hand-editing generated output.");
        return 2;
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return Regex.IsMatch(normalized, "(^|/)shared/clients/") || Regex.IsMatch(normalized, "(^|/)generated/");
    }

    // Anything that can create, overwrite, move or delete a file. Reads (cat, grep, head)
    // deliberately absent — inspecting generated output is legitimate.
    private static readonly string[] WriteIndicators =
    [
        ">", "tee", "sed -i", "sed --in-place", "cp ", "mv ", "rm ", "rmdir ",
        "truncate", "dd ", "patch ", "touch ", "install ", "ln ", "chmod ",
    ];

    private static bool MutatesFiles(string command) =>
        WriteIndicators.Any(indicator => command.Contains(indicator, StringComparison.Ordinal));

    private static readonly char[] ShellSeparators = [' ', '\t', '\n', '\r', '>', '<', '|', ';', '&', '\'', '"', '(', ')', '='];

    private static IEnumerable<string> ShellTokens(string command) =>
        command.Split(ShellSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
        var toolInput = ReadToolInput();
        return toolInput is null ? null : ReadString(toolInput.Value, "file_path");
    }

    // The hook payload arrives on stdin once per process, so this is read exactly once.
    // Cloned out of the JsonDocument because the document is disposed on return.
    private static JsonElement? ReadToolInput()
    {
        var input = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(input))
            return null;

        using var doc = JsonDocument.Parse(input);
        return doc.RootElement.TryGetProperty("tool_input", out var toolInput)
            ? toolInput.Clone()
            : null;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
