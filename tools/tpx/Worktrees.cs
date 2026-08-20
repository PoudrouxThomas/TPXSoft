using System.Text.Json;

namespace Tpx;

/// <summary>tpx worktree new: creates a git worktree and allocates it a unique Postgres port
/// offset + COMPOSE_PROJECT_NAME, per PLAN.md 0.5/0.6 (the port-collision problem that kills
/// parallel worktree agents).</summary>
internal static class Worktrees
{
    public static int New(string spec)
    {
        var parts = spec.Split('/', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            Console.Error.WriteLine("tpx worktree new: expected <module>/<feature>, e.g. 'auth/demo'");
            return 1;
        }

        var (module, feature) = (parts[0], parts[1]);
        var name = $"{module}-{feature}";
        var branch = $"{module}/{feature}";

        var worktreesRoot = Path.GetFullPath(Path.Combine(Modules.RepoRoot, "..", $"{Path.GetFileName(Modules.RepoRoot)}.worktrees"));
        var worktreePath = Path.Combine(worktreesRoot, name);

        if (Directory.Exists(worktreePath))
        {
            Console.Error.WriteLine($"tpx worktree new: {worktreePath} already exists");
            return 1;
        }

        Directory.CreateDirectory(worktreesRoot);

        var portOffset = AllocatePortOffset(name);
        var postgresPort = 5432 + portOffset;
        var composeProjectName = $"tpxsoft_{module}_{feature}";

        Console.WriteLine($"tpx worktree new: git worktree add \"{worktreePath}\" -b {branch}");
        var exitCode = Shell.Run("git", $"worktree add \"{worktreePath}\" -b \"{branch}\"");
        if (exitCode != 0)
            return exitCode;

        File.WriteAllLines(Path.Combine(worktreePath, ".env"), new[]
        {
            $"COMPOSE_PROJECT_NAME={composeProjectName}",
            $"POSTGRES_PORT={postgresPort}",
            "POSTGRES_USER=tpxsoft",
            "POSTGRES_PASSWORD=tpxsoft",
            "POSTGRES_DB=tpxsoft",
        });

        Console.WriteLine($"tpx worktree new: ready at {worktreePath}");
        Console.WriteLine($"tpx worktree new: POSTGRES_PORT={postgresPort}, COMPOSE_PROJECT_NAME={composeProjectName}");
        return 0;
    }

    // Allocated offsets are stored in the shared git dir (git rev-parse --git-common-dir),
    // not the worktree's own working copy — every worktree checks out its own PLAN.md,
    // so anything under Modules.RepoRoot would NOT be visible across worktrees.
    private static int AllocatePortOffset(string name)
    {
        var stateFile = StateFilePath();
        Dictionary<string, int> state = File.Exists(stateFile)
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(stateFile)) ?? new()
            : new();

        if (state.TryGetValue(name, out var existing))
            return existing;

        var next = state.Count == 0 ? 1 : state.Values.Max() + 1;
        state[name] = next;
        File.WriteAllText(stateFile, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        return next;
    }

    private static string StateFilePath()
    {
        var (exitCode, output) = Shell.Capture("git", "rev-parse --git-common-dir");
        var gitDir = exitCode == 0 ? output.Trim() : ".git";
        if (!Path.IsPathRooted(gitDir))
            gitDir = Path.Combine(Modules.RepoRoot, gitDir);

        return Path.Combine(gitDir, "tpx-worktree-ports.json");
    }
}
