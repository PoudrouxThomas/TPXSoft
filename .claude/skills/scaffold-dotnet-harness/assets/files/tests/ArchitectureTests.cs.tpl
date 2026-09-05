using System.Reflection;
using NetArchTest.Rules;
using __NAME__.Domain;
using __NAME__.Infrastructure;

namespace __NAME__.UnitTests;

/// <summary>
/// The architecture rules, as ordinary tests.
///
/// They live in the unit test project deliberately: a rule that runs only in CI is
/// outside the agent's definition of done, which means it does not exist as far as the
/// agent is concerned. This is also the rule an agent is most likely to break and the
/// one least visible in a diff review -- a single `using Microsoft.EntityFrameworkCore`
/// in a handler looks like nothing and undoes the whole layering.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Todo).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_knows_nothing_about_frameworks() =>
        Ensure(
            Types.InAssembly(Domain)
                .Should()
                .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
                .GetResult());

    [Fact]
    public void Domain_does_not_depend_on_outer_layers() =>
        Ensure(
            Types.InAssembly(Domain)
                .Should()
                .NotHaveDependencyOnAny("__NAME__.Infrastructure", "__NAME__.Api")
                .GetResult());

    [Fact]
    public void Api_does_not_touch_EntityFramework_directly() =>
        Ensure(
            Types.InAssembly(Api)
                .Should()
                .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult());

    [Fact]
    public void Infrastructure_does_not_depend_on_the_Api() =>
        Ensure(
            Types.InAssembly(Infrastructure)
                .Should()
                .NotHaveDependencyOn("__NAME__.Api")
                .GetResult());

    [Fact]
    public void Repositories_live_only_in_Infrastructure()
    {
        var strays = Types.InAssemblies([Domain, Api])
            .That()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes();

        Assert.Empty(strays);
    }

    [Fact]
    public void Domain_types_are_sealed() =>
        Ensure(Types.InAssembly(Domain).That().AreClasses().Should().BeSealed().GetResult());

    [Fact]
    public void Types_stay_in_their_assembly_namespace() =>
        Ensure(
            Types.InAssembly(Infrastructure)
                .Should()
                .ResideInNamespaceStartingWith("__NAME__.Infrastructure")
                .GetResult());

    private static void Ensure(TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccessful)
        {
            return;
        }

        var offenders = string.Join(", ", result.FailingTypeNames ?? []);
        Assert.Fail($"Architecture rule violated by: {offenders}");
    }
}
