using System.Reflection;
using NetArchTest.Rules;

namespace Harmonee.Architecture.Tests;

/// <summary>
/// Keeps the modular monolith modular (see docs/handbook/01-architecture-overview.md §4.3).
/// Modules talk to each other only through *.Contracts; domains stay free of frameworks.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] s_modules = ["Families", "Calendar", "Kitchen", "Notes"];

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var module in s_modules)
        {
            foreach (var other in s_modules.Where(o => !string.Equals(o, module, StringComparison.Ordinal)))
            {
                data.Add(module, other);
            }
        }

        return data;
    }

    public static TheoryData<string> Modules() => [.. s_modules];

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Modules_do_not_depend_on_other_modules_internals(string module, string other)
    {
        var result = Types.InAssembly(ModuleAssembly(module))
            .ShouldNot()
            .HaveDependencyOnAny($"Harmonee.{other}.Domain", $"Harmonee.{other}.Features", $"Harmonee.{other}.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_domains_do_not_depend_on_frameworks(string module)
    {
        var result = Types.InAssembly(ModuleAssembly(module))
            .That().ResideInNamespace($"Harmonee.{module}.Domain")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Azure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_internals_are_not_public(string module)
    {
        var result = Types.InAssembly(ModuleAssembly(module))
            .That().ResideInNamespaceStartingWith($"Harmonee.{module}.")
            .And().DoNotResideInNamespace($"Harmonee.{module}.Infrastructure.Migrations") // EF Core generates these as public
            .ShouldNot().BePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    [Fact]
    public void Api_contracts_have_no_dependencies_on_other_harmonee_code()
    {
        var contracts = typeof(Api.Contracts.Common.FieldLimits).Assembly;

        contracts.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("Harmonee", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_web_app_never_references_api_modules_or_the_database()
    {
        var web = typeof(Web.Api.HarmoneeApiClient).Assembly;
        var references = web.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.ShouldNotContain(n => n.StartsWith("Harmonee.Families", StringComparison.Ordinal)
            || n.StartsWith("Harmonee.Calendar", StringComparison.Ordinal)
            || n.StartsWith("Harmonee.Kitchen", StringComparison.Ordinal)
            || n.StartsWith("Harmonee.Notes", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    private static Assembly ModuleAssembly(string module) => Assembly.Load($"Harmonee.{module}");

    private static string Failures(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
