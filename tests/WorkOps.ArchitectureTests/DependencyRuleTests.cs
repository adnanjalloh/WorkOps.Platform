using System.Reflection;

namespace WorkOps.ArchitectureTests;

[TestClass]
public sealed class DependencyRuleTests
{
    [TestMethod]
    public void Domain_has_no_project_dependencies()
    {
        var references = ReferencedWorkOpsAssemblies(typeof(Domain.AssemblyMarker).Assembly);

        CollectionAssert.AreEqual(Array.Empty<string>(), references);
    }

    [TestMethod]
    public void Application_does_not_depend_on_api_or_infrastructure()
    {
        var references = ReferencedWorkOpsAssemblies(typeof(Application.AssemblyMarker).Assembly);

        CollectionAssert.DoesNotContain(references, "WorkOps.Api");
        CollectionAssert.DoesNotContain(references, "WorkOps.Infrastructure");
    }

    private static string[] ReferencedWorkOpsAssemblies(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name is not null && name.StartsWith("WorkOps.", StringComparison.Ordinal))
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
