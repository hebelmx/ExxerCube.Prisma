namespace ExxerCube.Prisma.Tests.Architecture;

/// <summary>
/// Architectural constraint tests for Hexagonal Architecture (Ports and Adapters) pattern.
/// These tests enforce architectural rules and prevent violations using NetArchTest.
///
/// Key Rules Enforced:
/// 1. Ports (Interfaces) → Domain Layer ONLY
/// 2. Adapters (Implementations) → Infrastructure Layer ONLY
/// 3. Application Layer → Orchestration ONLY (uses Ports, does NOT implement them)
/// 4. Dependency Flow: Infrastructure → Domain ← Application
/// 5. No cross-Infrastructure dependencies
/// 6. No class type duplication across layers
/// </summary>
public sealed class HexagonalArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ExxerCube.Prisma.Domain.Entities.FileMetadata).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ExxerCube.Prisma.Application.Services.DocumentIngestionService).Assembly;

    private static readonly Assembly[] InfrastructureAssemblies = new[]
    {
        typeof(ExxerCube.Prisma.Infrastructure.Database.EntityFramework.PrismaDbContext).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.Classification.MatchingPolicyService).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.Extraction.XmlMetadataExtractor).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.Export.DigitalPdfSigner).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.FileStorage.FileSystemDownloadStorageAdapter).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.BrowserAutomation.PlaywrightBrowserAutomationAdapter).Assembly,
        typeof(ExxerCube.Prisma.Infrastructure.FileSystem.FileSystemLoader).Assembly,
    };

    // Rule 1: Ports (Interfaces) → Domain Layer ONLY

    [Fact]
    public void All_Interfaces_Should_Be_In_Domain_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Interfaces")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"All interfaces must be in Domain.Interfaces namespace. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_Layer_Should_Not_Contain_Interfaces()
    {
        var interfaces = Types.InAssembly(ApplicationAssembly)
            .That()
            .AreInterfaces()
            .GetTypes()
            .ToList();

        interfaces.ShouldBeEmpty(
            $"Application layer must not contain interfaces. Found: {string.Join(", ", interfaces.Select(t => t.FullName))}");
    }

    [Fact]
    public void Infrastructure_Layers_Should_Not_Contain_Interfaces()
    {
        var violations = new List<string>();

        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            var interfaces = Types.InAssembly(infrastructureAssembly)
                .That()
                .AreInterfaces()
                .GetTypes()
                .ToList();

            if (interfaces.Any())
            {
                var assemblyName = infrastructureAssembly.GetName().Name;
                var failingTypes = string.Join(", ", interfaces.Select(t => t.FullName));
                violations.Add($"{assemblyName}: {failingTypes}");
            }
        }

        violations.ShouldBeEmpty(
            $"Infrastructure layers must not contain interfaces. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 2: Adapters (Implementations) → Infrastructure Layer ONLY

    [Fact]
    public void Domain_Interfaces_Should_Only_Be_Implemented_In_Infrastructure()
    {
        var domainInterfaces = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .And()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Interfaces")
            .GetTypes()
            .ToList();

        var violations = new List<string>();

        foreach (var domainInterface in domainInterfaces)
        {
            // Check Application layer
            var appImplementations = Types.InAssembly(ApplicationAssembly)
                .That()
                .ImplementInterface(domainInterface)
                .GetTypes()
                .ToList();

            if (appImplementations.Any())
            {
                violations.Add(
                    $"Application layer implements {domainInterface.Name}: {string.Join(", ", appImplementations.Select(t => t.FullName))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Domain interfaces must only be implemented in Infrastructure layer. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Application_Services_Should_Not_Implement_Domain_Interfaces()
    {
        var domainInterfaces = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .And()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Interfaces")
            .GetTypes()
            .ToList();

        var violations = new List<string>();

        foreach (var domainInterface in domainInterfaces)
        {
            var appServices = Types.InAssembly(ApplicationAssembly)
                .That()
                .AreClasses()
                .And()
                .ResideInNamespace("ExxerCube.Prisma.Application.Services")
                .And()
                .ImplementInterface(domainInterface)
                .GetTypes()
                .ToList();

            if (appServices.Any())
            {
                violations.Add(
                    $"Application services implement {domainInterface.Name}: {string.Join(", ", appServices.Select(t => t.FullName))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Application services must not implement Domain interfaces. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 3: Dependency Flow - Infrastructure → Domain ← Application

    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("ExxerCube.Prisma.Application")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain must not depend on Application. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var infrastructureNamespaces = new[]
        {
            "ExxerCube.Prisma.Infrastructure.Database",
            "ExxerCube.Prisma.Infrastructure.Classification",
            "ExxerCube.Prisma.Infrastructure.Extraction",
            "ExxerCube.Prisma.Infrastructure.Export",
            "ExxerCube.Prisma.Infrastructure.FileStorage",
            "ExxerCube.Prisma.Infrastructure.BrowserAutomation",
            "ExxerCube.Prisma.Infrastructure.FileSystem",
        };

        var violations = new List<string>();

        foreach (var infrastructureNamespace in infrastructureNamespaces)
        {
            var result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOn(infrastructureNamespace)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>());
                violations.Add($"{infrastructureNamespace}: {failingTypes}");
            }
        }

        violations.ShouldBeEmpty(
            $"Domain must not depend on Infrastructure. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var infrastructureNamespaces = new[]
        {
            "ExxerCube.Prisma.Infrastructure.Database",
            "ExxerCube.Prisma.Infrastructure.Classification",
            "ExxerCube.Prisma.Infrastructure.Extraction",
            "ExxerCube.Prisma.Infrastructure.Export",
            "ExxerCube.Prisma.Infrastructure.FileStorage",
            "ExxerCube.Prisma.Infrastructure.BrowserAutomation",
            "ExxerCube.Prisma.Infrastructure.FileSystem",
        };

        var violations = new List<string>();

        foreach (var infrastructureNamespace in infrastructureNamespaces)
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .ShouldNot()
                .HaveDependencyOn(infrastructureNamespace)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>());
                violations.Add($"{infrastructureNamespace}: {failingTypes}");
            }
        }

        violations.ShouldBeEmpty(
            $"Application must not depend on Infrastructure. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Application_Should_Depend_On_Domain()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .HaveDependencyOn("ExxerCube.Prisma.Domain")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Application must depend on Domain. This test validates that Application uses Domain types.");
    }

    [Fact]
    public void Infrastructure_Should_Depend_On_Domain()
    {
        var violations = new List<string>();

        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            var result = Types.InAssembly(infrastructureAssembly)
                .Should()
                .HaveDependencyOn("ExxerCube.Prisma.Domain")
                .GetResult();

            if (!result.IsSuccessful)
            {
                var assemblyName = infrastructureAssembly.GetName().Name;
                violations.Add($"{assemblyName} does not depend on Domain");
            }
        }

        violations.ShouldBeEmpty(
            $"All Infrastructure layers must depend on Domain. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 4: No Cross-Infrastructure Dependencies

    [Fact]
    public void Infrastructure_Projects_Should_Not_Depend_On_Each_Other()
    {
        var infrastructureNamespaces = new[]
        {
            "ExxerCube.Prisma.Infrastructure.Database",
            "ExxerCube.Prisma.Infrastructure.Classification",
            "ExxerCube.Prisma.Infrastructure.Extraction",
            "ExxerCube.Prisma.Infrastructure.Export",
            "ExxerCube.Prisma.Infrastructure.FileStorage",
            "ExxerCube.Prisma.Infrastructure.BrowserAutomation",
            "ExxerCube.Prisma.Infrastructure.FileSystem",
        };

        var violations = new List<string>();

        for (int i = 0; i < InfrastructureAssemblies.Length; i++)
        {
            for (int j = 0; j < infrastructureNamespaces.Length; j++)
            {
                var sourceAssembly = InfrastructureAssemblies[i];
                var sourceNamespace = infrastructureNamespaces[i];
                var targetNamespace = infrastructureNamespaces[j];

                if (sourceNamespace == targetNamespace) continue; // Skip self

                var result = Types.InAssembly(sourceAssembly)
                    .ShouldNot()
                    .HaveDependencyOn(targetNamespace)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    var sourceName = sourceAssembly.GetName().Name;
                    var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>());
                    violations.Add($"{sourceName} → {targetNamespace}: {failingTypes}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Infrastructure projects must not depend on each other. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 5: No Class Type Duplication

    [Fact]
    public void No_Duplicate_Class_Names_Across_Layers()
    {
        var allTypes = new Dictionary<string, List<string>>();

        // Collect all types from Domain
        foreach (var type in Types.InAssembly(DomainAssembly).GetTypes())
        {
            if (type.IsClass && !type.IsNested)
            {
                var key = type.Name;
                if (!allTypes.ContainsKey(key))
                {
                    allTypes[key] = new List<string>();
                }
                allTypes[key].Add($"Domain: {type.FullName}");
            }
        }

        // Collect all types from Application
        foreach (var type in Types.InAssembly(ApplicationAssembly).GetTypes())
        {
            if (type.IsClass && !type.IsNested)
            {
                var key = type.Name;
                if (!allTypes.ContainsKey(key))
                {
                    allTypes[key] = new List<string>();
                }
                allTypes[key].Add($"Application: {type.FullName}");
            }
        }

        // Collect all types from Infrastructure
        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            foreach (var type in Types.InAssembly(infrastructureAssembly).GetTypes())
            {
                if (type.IsClass && !type.IsNested)
                {
                    var key = type.Name;
                    if (!allTypes.ContainsKey(key))
                    {
                        allTypes[key] = new List<string>();
                    }
                    var assemblyName = infrastructureAssembly.GetName().Name;
                    allTypes[key].Add($"Infrastructure ({assemblyName}): {type.FullName}");
                }
            }
        }

        // Find duplicates across different layers
        var duplicates = allTypes
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp =>
            {
                var layers = kvp.Value.Select(v => v.Split(':')[0]).Distinct().ToList();
                return layers.Count > 1; // Duplicate across different layers
            })
            .ToList();

        duplicates.ShouldBeEmpty(
            $"No class types should be duplicated across layers. Duplicates found: {string.Join("; ", duplicates.Select(d => $"{d.Key} in {string.Join(", ", d.Value)}"))}");
    }

    [Fact]
    public void No_Duplicate_Interface_Names_Across_Layers()
    {
        var allInterfaces = new Dictionary<string, List<string>>();

        // Collect all interfaces from Domain
        foreach (var type in Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .GetTypes())
        {
            var key = type.Name;
            if (!allInterfaces.ContainsKey(key))
            {
                allInterfaces[key] = new List<string>();
            }
            allInterfaces[key].Add($"Domain: {type.FullName}");
        }

        // Collect all interfaces from Application (should be none, but check anyway)
        foreach (var type in Types.InAssembly(ApplicationAssembly)
            .That()
            .AreInterfaces()
            .GetTypes())
        {
            var key = type.Name;
            if (!allInterfaces.ContainsKey(key))
            {
                allInterfaces[key] = new List<string>();
            }
            allInterfaces[key].Add($"Application: {type.FullName}");
        }

        // Collect all interfaces from Infrastructure (should be none, but check anyway)
        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            foreach (var type in Types.InAssembly(infrastructureAssembly)
                .That()
                .AreInterfaces()
                .GetTypes())
            {
                var key = type.Name;
                if (!allInterfaces.ContainsKey(key))
                {
                    allInterfaces[key] = new List<string>();
                }
                var assemblyName = infrastructureAssembly.GetName().Name;
                allInterfaces[key].Add($"Infrastructure ({assemblyName}): {type.FullName}");
            }
        }

        // Find duplicates across different layers
        var duplicates = allInterfaces
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp =>
            {
                var layers = kvp.Value.Select(v => v.Split(':')[0]).Distinct().ToList();
                return layers.Count > 1; // Duplicate across different layers
            })
            .ToList();

        duplicates.ShouldBeEmpty(
            $"No interface types should be duplicated across layers. Duplicates found: {string.Join("; ", duplicates.Select(d => $"{d.Key} in {string.Join(", ", d.Value)}"))}");
    }

    //

    // Rule 6: EF Core Violations

    [Fact]
    public void Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Application must not reference EntityFrameworkCore. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_Entities_Should_Not_Have_EF_Core_Attributes()
    {
        var efCoreAttributes = new[]
        {
            "Microsoft.EntityFrameworkCore.KeyAttribute",
            "Microsoft.EntityFrameworkCore.RequiredAttribute",
            "Microsoft.EntityFrameworkCore.ForeignKeyAttribute",
            "Microsoft.EntityFrameworkCore.ColumnAttribute",
            "Microsoft.EntityFrameworkCore.TableAttribute",
        };

        var violations = new List<string>();

        foreach (var attributeName in efCoreAttributes)
        {
            var result = Types.InAssembly(DomainAssembly)
                .That()
                .AreClasses()
                .And()
                .ResideInNamespace("ExxerCube.Prisma.Domain.Entities")
                .ShouldNot()
                .HaveDependencyOn(attributeName)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>());
                violations.Add($"{attributeName}: {failingTypes}");
            }
        }

        violations.ShouldBeEmpty(
            $"Domain entities must not have EF Core attributes. Violations: {string.Join("; ", violations)}");
    }

    //
}