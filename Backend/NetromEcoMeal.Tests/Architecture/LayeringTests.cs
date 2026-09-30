using System.Reflection;
using NetromEcoMeal.Database;
using NetromEcoMeal.Services;

namespace NetromEcoMeal.Tests.Architecture;

// Blazor-circuit dependencies (IJSRuntime, AuthenticationStateProvider, ...) were moved out of
// BusinessLogic and DataAccess behind ICurrentUser and IPackageStockNotifier. This guards the
// boundary: a plain Web API host must be able to reference these two projects without Blazor.
public class LayeringTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Microsoft.AspNetCore.Components",
        "Microsoft.JSInterop",
    ];

    [Fact]
    public void BusinessLogic_DoesNotReferenceBlazorComponentsOrJsInterop()
    {
        AssertNoForbiddenReferences(typeof(AuthService).Assembly);
    }

    [Fact]
    public void DataAccess_DoesNotReferenceBlazorComponentsOrJsInterop()
    {
        AssertNoForbiddenReferences(typeof(EcoMealDbContext).Assembly);
    }

    private static void AssertNoForbiddenReferences(Assembly assembly)
    {
        var offending = assembly.GetReferencedAssemblies()
            .Where(referenced => ForbiddenAssemblyPrefixes.Any(prefix =>
                referenced.Name is not null && referenced.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(referenced => referenced.Name)
            .ToList();

        Assert.True(offending.Count == 0,
            $"{assembly.GetName().Name} references forbidden assemblies: {string.Join(", ", offending)}");
    }
}
