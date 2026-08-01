using System.Reflection;
using WorkOps.Contracts.Common;
using WorkOps.Domain.Common;
using WorkOps.Infrastructure.Persistence;

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
            .Where(static property =>
                property.GetCustomAttribute<SanitizeAsAttribute>() is null &&
                property.GetCustomAttribute<SkipSanitizationAttribute>() is null)
            .Select(static property => $"{property.DeclaringType?.FullName}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), unclassifiedProperties);
    }

    [TestMethod]
    public void Every_mapped_tenant_owned_entity_has_a_query_filter()
    {
        using var dbContext = new WorkOpsDbContextFactory().CreateDbContext([]);
        var tenantOwnedEntityTypes = dbContext.Model
            .GetEntityTypes()
            .Where(static entityType => typeof(IWorkspaceOwned).IsAssignableFrom(entityType.ClrType))
            .ToArray();
        var missingFilters = tenantOwnedEntityTypes
            .Where(static entityType => entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(static entityType => entityType.ClrType.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsNotEmpty(tenantOwnedEntityTypes);
        CollectionAssert.AreEqual(Array.Empty<string>(), missingFilters);
    }

    [TestMethod]
    public void Api_endpoints_do_not_access_the_database_context_directly()
    {
        var offenders = typeof(Program).Assembly
            .GetTypes()
            .Where(static type => type.Namespace == "WorkOps.Api.Endpoints")
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(static method => method.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(WorkOpsDbContext)))
            .Select(static method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    [TestMethod]
    public void Public_contracts_do_not_expose_domain_entities()
    {
        var offenders = typeof(Contracts.AssemblyMarker).Assembly
            .GetExportedTypes()
            .SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(static property =>
                property.PropertyType.Namespace?.StartsWith("WorkOps.Domain", StringComparison.Ordinal) == true)
            .Select(static property => $"{property.DeclaringType?.FullName}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    [TestMethod]
    public void Docker_context_excludes_sensitive_local_artifacts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerIgnore = File.ReadAllLines(Path.Combine(repositoryRoot, ".dockerignore"))
            .Select(static line => line.TrimEnd('/'))
            .ToHashSet(StringComparer.Ordinal);
        var requiredPatterns = new[]
        {
            ".env",
            ".env.*",
            "**/secrets.json",
            "*.pem",
            "*.key",
            "*.pfx",
            "*.p12",
            "*.jks",
            ".terraform",
            "*.tfstate",
            "*.tfstate.*",
            "*.tfplan",
            "terraform.tfvars",
            "terraform.tfvars.json",
            "crash.log",
            ".local",
            ".vs",
            ".idea",
            "*.log",
            "logs",
            "artifacts",
        };
        var missingPatterns = requiredPatterns
            .Where(pattern => !dockerIgnore.Contains(pattern))
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), missingPatterns);
    }

    private static string[] ReferencedWorkOpsAssemblies(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name is not null && name.StartsWith("WorkOps.", StringComparison.Ordinal))
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WorkOps.Platform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located.");
    }
}
