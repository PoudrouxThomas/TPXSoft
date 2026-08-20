using System.Diagnostics;
using Tpx;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "verify" => HandleVerify(args[1..]),
    "test" => HandleTest(args[1..]),
    "contract" => HandleContract(args[1..]),
    "gen" => Contracts.Gen(),
    "worktree" => HandleWorktree(args[1..]),
    "-h" or "--help" or "help" => Help(),
    _ => Unknown(args[0]),
};

static int HandleVerify(string[] rest)
{
    if (rest.Length == 0)
    {
        Console.Error.WriteLine("tpx verify: expected a module name, '--affected', or 'boundaries'");
        return 1;
    }

    return rest[0] switch
    {
        "--affected" => VerifyAffected(),
        "boundaries" => VerifyBoundaries(),
        var module => VerifyModule(module),
    };
}

static int VerifyModule(string module)
{
    var sln = Modules.FindSolution(module);
    if (sln is null)
    {
        Console.Error.WriteLine($"tpx verify: no solution found under modules/{module}/*.sln");
        return 1;
    }

    var stopwatch = Stopwatch.StartNew();

    Console.WriteLine($"tpx verify {module}: dotnet build");
    if (Shell.Run("dotnet", $"build \"{sln}\" -warnaserror") != 0)
        return 1;

    Console.WriteLine($"tpx verify {module}: dotnet test (unit only)");
    if (Shell.Run("dotnet", $"test \"{sln}\" --filter \"Category!=Integration\"") != 0)
        return 1;

    var contract = Modules.FindContract(module);
    if (contract is not null)
    {
        Console.WriteLine($"tpx verify {module}: contract lint");
        if (!Contracts.Lint(contract))
            return 1;
    }

    stopwatch.Stop();
    var seconds = stopwatch.Elapsed.TotalSeconds;
    Console.WriteLine($"tpx verify {module}: green in {seconds:F1}s");

    if (seconds > 60)
        Console.Error.WriteLine($"tpx verify {module}: WARNING exceeded the 60s target ({seconds:F1}s) — fix the loop before writing more feature code");

    return 0;
}

static int VerifyAffected()
{
    var (exitCode, output) = Shell.Capture("git", "diff --name-only main...HEAD");
    if (exitCode != 0)
        (exitCode, output) = Shell.Capture("git", "diff --name-only main");

    if (exitCode != 0)
    {
        Console.Error.WriteLine("tpx verify --affected: 'git diff' against main failed — is there a main branch to diff against?");
        return 1;
    }

    var affectedModules = output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(path => path.Replace('\\', '/'))
        .Where(path => path.StartsWith("modules/", StringComparison.Ordinal))
        .Select(path => path.Split('/')[1])
        .Distinct()
        .ToList();

    if (affectedModules.Count == 0)
    {
        Console.WriteLine("tpx verify --affected: no changed files under modules/ — nothing to verify");
        return 0;
    }

    foreach (var module in affectedModules)
    {
        Console.WriteLine($"tpx verify --affected: '{module}' changed");
        var result = VerifyModule(module);
        if (result != 0)
            return result;
    }

    return 0;
}

static int VerifyBoundaries()
{
    var violations = Boundaries.Check();
    if (violations.Count == 0)
    {
        Console.WriteLine("tpx verify boundaries: clean — no module references another module's .Domain or .Infrastructure directly");
        return 0;
    }

    foreach (var violation in violations)
        Console.Error.WriteLine($"tpx verify boundaries: {violation.Project} references {violation.ReferencedPath}");

    return 1;
}

static int HandleTest(string[] rest)
{
    if (rest.Length < 2 || rest[1] != "--integration")
    {
        Console.Error.WriteLine("tpx test: expected '<module> --integration'");
        return 1;
    }

    var module = rest[0];
    var testsDir = Path.Combine(Modules.ModulesDir, module, "tests");
    var integrationProject = Directory.Exists(testsDir)
        ? Directory.GetFiles(testsDir, "*IntegrationTests.csproj", SearchOption.AllDirectories).FirstOrDefault()
        : null;

    if (integrationProject is null)
    {
        Console.Error.WriteLine($"tpx test {module} --integration: no *.IntegrationTests.csproj found under modules/{module}/tests/");
        return 1;
    }

    Console.WriteLine($"tpx test {module} --integration: dotnet test (Testcontainers will spin up its own Postgres)");
    return Shell.Run("dotnet", $"test \"{integrationProject}\"");
}

static int HandleContract(string[] rest)
{
    if (rest.Length == 0 || rest[0] != "lint")
    {
        Console.Error.WriteLine("tpx contract: expected 'lint'");
        return 1;
    }

    return Contracts.LintAll();
}

static int HandleWorktree(string[] rest)
{
    if (rest.Length < 2 || rest[0] != "new")
    {
        Console.Error.WriteLine("tpx worktree: expected 'new <module>/<feature>'");
        return 1;
    }

    return Worktrees.New(rest[1]);
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"tpx: unknown command '{command}'");
    PrintUsage();
    return 1;
}

static int Help()
{
    PrintUsage();
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("""
        tpx — TPXSoft verification loop CLI

        Usage:
          tpx verify <module>              build + unit tests + contract lint for one module
          tpx verify --affected            map git diff (vs main) to modules, verify each
          tpx verify boundaries             fail if a module references another module's .Domain/.Infrastructure
          tpx test <module> --integration  Testcontainers integration tests against real Postgres
          tpx contract lint                validate every contracts/*.yaml + breaking-change diff vs main
          tpx gen                          regenerate shared/clients from contracts/ (NSwag + ng-openapi-gen)
          tpx worktree new <module>/<feature>  git worktree + allocated Postgres port + COMPOSE_PROJECT_NAME
        """);
}
