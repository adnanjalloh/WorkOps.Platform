using System.Reflection;
using WorkOps.Contracts.Common;
using WorkOps.Domain;
using WorkOps.Domain.Common;
using WorkOps.Domain.Tenancy;
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
    public void Every_tenant_filtered_entity_has_write_boundary_metadata()
    {
        using var dbContext = new WorkOpsDbContextFactory().CreateDbContext([]);
        var tenantScopedEntityTypes = dbContext.Model
            .GetEntityTypes()
            .Where(static entityType =>
                entityType.GetDeclaredQueryFilters().Count > 0 ||
                entityType.ClrType == typeof(Workspace) ||
                typeof(IWorkspaceOwned).IsAssignableFrom(entityType.ClrType))
            .ToArray();
        var missingFilters = tenantScopedEntityTypes
            .Where(static entityType => entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(static entityType => entityType.ClrType.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingResolvers = tenantScopedEntityTypes
            .Where(static entityType =>
                entityType.FindAnnotation(WorkOpsDbContext.TenantIdPropertyAnnotation)?.Value is not string propertyName ||
                entityType.FindProperty(propertyName)?.ClrType != typeof(WorkspaceId))
            .Select(static entityType => entityType.ClrType.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unfilteredResolvers = dbContext.Model
            .GetEntityTypes()
            .Where(static entityType =>
                entityType.FindAnnotation(WorkOpsDbContext.TenantIdPropertyAnnotation) is not null &&
                entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(static entityType => entityType.ClrType.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsNotEmpty(tenantScopedEntityTypes);
        CollectionAssert.Contains(
            tenantScopedEntityTypes.Select(static entityType => entityType.ClrType).ToArray(),
            typeof(Workspace));
        CollectionAssert.AreEqual(Array.Empty<string>(), missingFilters);
        CollectionAssert.AreEqual(Array.Empty<string>(), missingResolvers);
        CollectionAssert.AreEqual(Array.Empty<string>(), unfilteredResolvers);
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
    public void Docker_context_preserves_security_sensitive_gitignore_rules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var gitIgnore = File.ReadAllLines(Path.Combine(repositoryRoot, ".gitignore"))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith('!'))
            .Select(static line => line.TrimEnd('/'))
            .Where(IsSecuritySensitiveIgnorePattern)
            .ToArray();
        var dockerIgnore = File.ReadAllLines(Path.Combine(repositoryRoot, ".dockerignore"))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.TrimEnd('/'))
            .ToHashSet(StringComparer.Ordinal);
        var missingPatterns = gitIgnore
            .Where(pattern => !dockerIgnore.Contains(pattern))
            .ToArray();

        CollectionAssert.Contains(gitIgnore, "**/appsettings.*.local.json");
        CollectionAssert.AreEqual(Array.Empty<string>(), missingPatterns);
    }

    [TestMethod]
    public void Verification_workflows_disable_persisted_checkout_credentials()
    {
        var repositoryRoot = FindRepositoryRoot();
        var offenders = Directory
            .GetFiles(Path.Combine(repositoryRoot, ".github", "workflows"), "*.yml")
            .SelectMany(file => FindCheckoutCredentialOffenders(repositoryRoot, file))
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), offenders);
    }

    private static bool IsSecuritySensitiveIgnorePattern(string pattern) =>
        pattern.Contains(".env", StringComparison.Ordinal) ||
        pattern.Contains("appsettings", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("terraform", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("tfstate", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("tfplan", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains(".local", StringComparison.Ordinal) ||
        pattern is "*.pem" or "*.key" or "*.pfx" or "*.p12" or "*.jks" or "crash.log";

    private static IEnumerable<string> FindCheckoutCredentialOffenders(
        string repositoryRoot,
        string workflowFile)
    {
        var lines = File.ReadAllLines(workflowFile);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("uses: actions/checkout@", StringComparison.Ordinal))
            {
                continue;
            }

            var stepEnd = Array.FindIndex(
                lines,
                index + 1,
                static line => line.StartsWith("      - name:", StringComparison.Ordinal));
            if (stepEnd < 0)
            {
                stepEnd = lines.Length;
            }

            if (!lines[index..stepEnd].Any(
                    static line => line.Trim() == "persist-credentials: false"))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, workflowFile)}:{index + 1}";
            }
        }
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
