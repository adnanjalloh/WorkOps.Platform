using System.Reflection;
using WorkOps.Contracts.Common;
using WorkOps.Domain.Common;
using WorkOps.Domain.Tenancy;

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
    public void Application_does_not_depend_on_api_contracts_or_infrastructure()
    {
        var references = ReferencedWorkOpsAssemblies(typeof(Application.AssemblyMarker).Assembly);

        CollectionAssert.DoesNotContain(references, "WorkOps.Api");
        CollectionAssert.DoesNotContain(references, "WorkOps.Contracts");
        CollectionAssert.DoesNotContain(references, "WorkOps.Infrastructure");
    }

    [TestMethod]
    public void Request_string_properties_declare_a_sanitization_policy()
    {
        var unclassifiedProperties = typeof(Contracts.AssemblyMarker).Assembly
            .GetTypes()
            .Where(static type => type.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(static property => property.PropertyType == typeof(string))
            .Where(static property =>
                property.GetCustomAttribute<SanitizeAsAttribute>() is null &&
                property.GetCustomAttribute<SkipSanitizationAttribute>() is null)
            .Select(static property => $"{property.DeclaringType?.FullName}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), unclassifiedProperties);
    }

    [TestMethod]
    public void Tenant_owned_entities_are_marked_for_isolation()
    {
        Assert.IsTrue(typeof(IWorkspaceOwned).IsAssignableFrom(typeof(WorkspaceMembership)));
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
