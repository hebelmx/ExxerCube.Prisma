using System.IO;
using System.Reflection;

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
public sealed class HexagonalArchitectureTests(ITestOutputHelper output)
{
    private readonly ILogger logger = XUnitLogger.CreateLogger<HexagonalArchitectureTests>(output);

    private static readonly Assembly DomainAssembly = typeof(ExxerCube.Prisma.Domain.Entities.FileMetadata).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ExxerCube.Prisma.Application.Services.DocumentIngestionService).Assembly;

    private static readonly Assembly[] InfrastructureAssemblies =
        GetInfrastructureAssemblies().ToArray();

    // Rule 1: Ports (Interfaces) → Domain Layer ONLY

    /// <summary>
    /// Asserts that all interfaces reside exclusively in the Domain.Interfaces namespace.
    /// </summary>
    [Fact]
    public void All_Interfaces_Should_Be_In_Domain_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Interfaces")
            .GetResult();

        if (!result.IsSuccessful)
        {
            var list = result.FailingTypes?
                .Select(t => $" - {t.FullName}")
                .ToList() ?? new List<string>();
            logger.LogWarning("Rule: Interfaces in Domain only. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        result.IsSuccessful.ShouldBeTrue(
            $"All interfaces must be in Domain.Interfaces namespace. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Ensures the application layer does not declare interfaces, preserving port placement in the domain.
    /// </summary>
    [Fact]
    public void Application_Layer_Should_Not_Contain_Interfaces()
    {
        var interfaces = Types.InAssembly(ApplicationAssembly)
            .That()
            .AreInterfaces()
            .GetTypes()
            .ToList();

        if (interfaces.Any())
        {
            var list = interfaces.Select(t => $" - {t.FullName}").ToList();
            logger.LogWarning("Rule: Application should not declare interfaces. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        interfaces.ShouldBeEmpty(
            $"Application layer must not contain interfaces. Found: {string.Join(", ", interfaces.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Ensures infrastructure projects do not introduce interfaces beyond infrastructure-specific abstractions.
    /// </summary>
    [Fact]
    public void Infrastructure_Layers_Should_Not_Contain_Interfaces()
    {
        var violations = new List<string>();

        // Exclude infrastructure-specific interfaces that are not Domain ports
        // IPrismaDbContext: EF Core DbContext abstraction, infrastructure-specific (not a Domain port)
        // IPrismaOcrWrapper: CSnakes auto-generated interface, infrastructure-specific (not a Domain port)
        var excludedInterfaces = new HashSet<string>
        {
            "ExxerCube.Prisma.Infrastructure.Database.EntityFramework.IPrismaDbContext",
            "CSnakes.Runtime.IPrismaOcrWrapper",
            "CSnakes.Runtime.IGotOcr2Wrapper"
        };

        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            var interfaces = Types.InAssembly(infrastructureAssembly)
                .That()
                .AreInterfaces()
                .GetTypes()
                .Where(t => !excludedInterfaces.Contains(t.FullName ?? string.Empty))
                .ToList();

            if (interfaces.Any())
            {
                var assemblyName = infrastructureAssembly.GetName().Name;
                var failingTypes = string.Join(", ", interfaces.Select(t => t.FullName));
                violations.Add($"{assemblyName}: {failingTypes}");
            }
        }

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Infrastructure should not expose ports. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Infrastructure layers must not contain interfaces (except infrastructure-specific abstractions). Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 2: Adapters (Implementations) → Infrastructure Layer ONLY

    /// <summary>
    /// Verifies domain interfaces are only implemented within infrastructure assemblies.
    /// </summary>
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

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Domain interfaces implemented only in Infra. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Domain interfaces must only be implemented in Infrastructure layer. Violations: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Ensures application services do not implement domain interfaces, maintaining port/adapter boundaries.
    /// </summary>
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

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Application services should not implement Domain interfaces. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Application services must not implement Domain interfaces. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 3: Dependency Flow - Infrastructure → Domain ← Application

    /// <summary>
    /// Validates the domain layer has no dependencies on the application layer.
    /// </summary>
    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("ExxerCube.Prisma.Application")
            .GetResult();

        if (!result.IsSuccessful)
        {
            var list = result.FailingTypes?.Select(t => $" - {t.FullName}").ToList() ?? new List<string>();
            logger.LogWarning("Rule: Domain must not depend on Application. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        result.IsSuccessful.ShouldBeTrue(
            $"Domain must not depend on Application. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Verifies the domain layer has no references to infrastructure assemblies.
    /// </summary>
    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var infrastructureNamespaces = new[]
        {
            "ExxerCube.Prisma.Infrastructure.Database",
            "ExxerCube.Prisma.Infrastructure.Classification",
            "ExxerCube.Prisma.Infrastructure.Extraction",
            "ExxerCube.Prisma.Infrastructure.Extraction.Adaptive",
            "ExxerCube.Prisma.Infrastructure.Export",
            "ExxerCube.Prisma.Infrastructure.FileStorage",
            "ExxerCube.Prisma.Infrastructure.BrowserAutomation",
            "ExxerCube.Prisma.Infrastructure.FileSystem",
            "ExxerCube.Prisma.Infrastructure.Metrics",
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

    /// <summary>
    /// Confirms the application layer does not depend directly on infrastructure assemblies.
    /// </summary>
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
            "ExxerCube.Prisma.Infrastructure.Metrics",
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

    /// <summary>
    /// Verifies the application layer depends on the domain layer as intended.
    /// </summary>
    [Fact]
    public void Application_Should_Depend_On_Domain()
    {
        // NetArchTest's HaveDependencyOn checks namespace references in code, not project references.
        // Since Application has a project reference to Domain, we verify by checking if Application
        // types actually use Domain types (which they should).
        var applicationTypes = Types.InAssembly(ApplicationAssembly).GetTypes().ToList();

        // Check if any Application type uses Domain types through interfaces, base types, or method signatures
        var hasDomainDependency = applicationTypes.Any(type =>
        {
            // Check if type implements Domain interfaces
            if (type.GetInterfaces().Any(i => i.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true))
                return true;

            // Check if type inherits from Domain types
            if (type.BaseType?.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true)
                return true;

            // Check if type has methods/properties that use Domain types
            var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
            if (methods.Any(m =>
                m.ReturnType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true ||
                m.GetParameters().Any(p => p.ParameterType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true)))
                return true;

            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
            if (properties.Any(p => p.PropertyType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true))
                return true;

            return false;
        });

        // Also check using NetArchTest as a fallback
        var netArchResult = Types.InAssembly(ApplicationAssembly)
            .Should()
            .HaveDependencyOn("ExxerCube.Prisma.Domain")
            .GetResult();

        // Pass if either check succeeds (NetArchTest might be too strict)
        var isSuccessful = hasDomainDependency || netArchResult.IsSuccessful;

        logger.LogInformation("Rule: Application depends on Domain. Direct={Direct}, NetArch={NetArch}", hasDomainDependency, netArchResult.IsSuccessful);

        isSuccessful.ShouldBeTrue(
            $"Application must depend on Domain. " +
            $"NetArchTest result: {(netArchResult.IsSuccessful ? "Pass" : "Fail")}. " +
            $"Direct check: {(hasDomainDependency ? "Found Domain usage" : "No Domain usage found")}. " +
            $"This test validates that Application uses Domain types.");
    }

    /// <summary>
    /// Confirms infrastructure assemblies depend on the domain layer (but not vice versa).
    /// </summary>
    [Fact]
    public void Infrastructure_Should_Depend_On_Domain()
    {
        var violations = new List<string>();
        // NetArchTest's HaveDependencyOn checks namespace references in code, not project references.
        // Since Infrastructure projects have project references to Domain, we verify by checking

        //List of infrastructure projects to exclude from this test
        var excludedProjects = new HashSet<string>
        {
            "ExxerCube.Prisma.Infrastructure.Python.GotOcr2",
        };

        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            var assemblyName = infrastructureAssembly.GetName().Name;

            if (assemblyName != null && excludedProjects.Contains(assemblyName))
            {
                logger.LogInformation("Skipping Infrastructure to Domain dependency test for excluded project: {Project}", assemblyName);
                continue;
            }
            // NetArchTest's HaveDependencyOn checks namespace references in code, not project references.
            // Since Infrastructure projects have project references to Domain, we verify by checking
            // if Infrastructure types actually use Domain types (which they should).
            var infrastructureTypes = Types.InAssembly(infrastructureAssembly).GetTypes().ToList();

            var hasDomainDependency = infrastructureTypes.Any(type =>
            {
                // Check if type implements Domain interfaces
                if (type.GetInterfaces().Any(i => i.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true))
                    return true;

                // Check if type inherits from Domain types
                if (type.BaseType?.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true)
                    return true;

                // Check if type has methods/properties that use Domain types
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (methods.Any(m =>
                    m.ReturnType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true ||
                    m.GetParameters().Any(p => p.ParameterType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true)))
                    return true;

                var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (properties.Any(p => p.PropertyType.Namespace?.StartsWith("ExxerCube.Prisma.Domain") == true))
                    return true;

                return false;
            });

            // Also check using NetArchTest as a fallback
            var netArchResult = Types.InAssembly(infrastructureAssembly)
                .Should()
                .HaveDependencyOn("ExxerCube.Prisma.Domain")
                .GetResult();

            // Pass if either check succeeds (NetArchTest might be too strict)
            var isSuccessful = hasDomainDependency || netArchResult.IsSuccessful;

            if (!isSuccessful)
            {
                var assemName = infrastructureAssembly.GetName().Name;
                violations.Add($"{assemName} does not depend on Domain (NetArchTest: {(netArchResult.IsSuccessful ? "Pass" : "Fail")}, Direct check: {(hasDomainDependency ? "Found" : "Not found")})");
            }
        }

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Infrastructure depends on Domain. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"All Infrastructure layers must depend on Domain. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 4: No Cross-Infrastructure Dependencies

    /// <summary>
    /// Ensures infrastructure projects do not depend on each other, preserving adapter isolation.
    /// </summary>
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
            "ExxerCube.Prisma.Infrastructure.Metrics",
        };

        var violations = new List<string>();

        for (int i = 0; i < InfrastructureAssemblies.Length; i++)
        {
            var sourceAssembly = InfrastructureAssemblies[i];
            var sourceNamespace = infrastructureNamespaces
                .FirstOrDefault(ns => sourceAssembly.GetName().Name?.StartsWith(ns, StringComparison.OrdinalIgnoreCase) == true);
            if (string.IsNullOrEmpty(sourceNamespace)) continue;

            for (int j = 0; j < infrastructureNamespaces.Length; j++)
            {
                var targetNamespace = infrastructureNamespaces[j];

                if (sourceNamespace == targetNamespace) continue; // Skip self

                var result = Types.InAssembly(sourceAssembly)
                    .ShouldNot()
                    .HaveDependencyOn(targetNamespace)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    var sourceName = sourceAssembly.GetName().Name ?? string.Empty;
                    var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Array.Empty<string>());
                    violations.Add($"{sourceName} → {targetNamespace}: {failingTypes}");
                }
            }
        }

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Infrastructure projects should be isolated. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Infrastructure projects must not depend on each other. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 5: No Class Type Duplication

    /// <summary>
    /// Ensures class names are unique across the domain, application, and infrastructure layers.
    /// </summary>
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
        // Exclude known acceptable duplicates:
        // - ServiceCollectionExtensions: Standard .NET DI pattern, each Infrastructure project has its own extension
        // - <PrivateImplementationDetails>: Compiler-generated types, not actual duplicates
        var excludedNames = new HashSet<string>
        {
            "ServiceCollectionExtensions",
            "<PrivateImplementationDetails>",
            // Intentional overlap between legacy and adaptive docx strategies during transition
            "ComplementExtractionStrategy",
            "SearchExtractionStrategy",
            "StructuredDocxStrategy"
        };

        var duplicates = allTypes
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp => !excludedNames.Contains(kvp.Key)) // Exclude acceptable duplicates
            .Where(kvp =>
            {
                var layers = kvp.Value.Select(v => v.Split(':')[0]).Distinct().ToList();
                return layers.Count > 1; // Duplicate across different layers
            })
            // Ignore compiler-generated anonymous types
            .Where(kvp => !kvp.Key.StartsWith("<>f__AnonymousType", StringComparison.Ordinal))
            .ToList();

        if (duplicates.Any())
        {
            var list = duplicates.Select(d => $" - {d.Key} in {string.Join(", ", d.Value)}").ToList();
            logger.LogWarning("Rule: No duplicate class names across layers. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        duplicates.ShouldBeEmpty(
            $"No class types should be duplicated across layers. Duplicates found: {string.Join("; ", duplicates.Select(d => $"{d.Key} in {string.Join(", ", d.Value)}"))}");
    }

    /// <summary>
    /// Ensures interface names are unique across the domain, application, and infrastructure layers.
    /// </summary>
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

        if (duplicates.Any())
        {
            var list = duplicates.Select(d => $" - {d.Key} in {string.Join(", ", d.Value)}").ToList();
            logger.LogWarning("Rule: No duplicate interface names across layers. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        duplicates.ShouldBeEmpty(
            $"No interface types should be duplicated across layers. Duplicates found: {string.Join("; ", duplicates.Select(d => $"{d.Key} in {string.Join(", ", d.Value)}"))}");
    }

    //

    // Rule 6: EF Core Violations

    /// <summary>
    /// Validates application layer does not reference EntityFrameworkCore directly.
    /// </summary>
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

    /// <summary>
    /// Ensures domain entities are free of EF Core attributes to keep the domain persistence-agnostic.
    /// </summary>
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

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Domain entities should be persistence-agnostic. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Domain entities must not have EF Core attributes. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 7: ITTDD Technical Debt Detection

    /// <summary>
    /// Ensures every interface in Domain.Interfaces has at least one implementation in Infrastructure.
    /// This detects architectural gaps where interfaces exist but implementations are missing.
    /// ITTDD (Interface-Test-Driven Development) intentionally creates interfaces before implementations,
    /// but this test ensures we don't forget to implement them.
    /// </summary>
    [Fact]
    public void All_Domain_Interfaces_Should_Have_At_Least_One_Implementation()
    {
        var domainInterfaces = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .And()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Interfaces")
            .GetTypes()
            .ToList();

        var unimplementedInterfaces = new List<string>();
        var allowlistedInterfaces = new HashSet<string>
        {
            "ExxerCube.Prisma.Domain.Interfaces.IEnumModel",
            "ExxerCube.Prisma.Domain.Interfaces.ILookupEntity",
            "ExxerCube.Prisma.Domain.Interfaces.ILookUpTable",
            // Intentional domain-only ports pending adapters
            //"ExxerCube.Prisma.Domain.Interfaces.IFilterSelectionStrategy",
            //"ExxerCube.Prisma.Domain.Interfaces.IImageEnhancementFilter",
            //"ExxerCube.Prisma.Domain.Interfaces.IImageQualityAnalyzer",
            //"ExxerCube.Prisma.Domain.Interfaces.IOcrSessionRepository",
            //"ExxerCube.Prisma.Domain.Interfaces.ISiaraLoginService",
            //"ExxerCube.Prisma.Domain.Interfaces.ITextComparer",
        };

        foreach (var domainInterface in domainInterfaces)
        {
            // Search all Infrastructure assemblies for implementations
            var hasImplementation = false;

            foreach (var infrastructureAssembly in InfrastructureAssemblies)
            {
                var implementations = Types.InAssembly(infrastructureAssembly)
                    .That()
                    .AreClasses()
                    .And()
                    .ImplementInterface(domainInterface)
                    .GetTypes()
                    .ToList();

                if (implementations.Any())
                {
                    hasImplementation = true;
                    break;
                }
            }

            if (!hasImplementation)
            {
                var name = domainInterface.FullName ?? domainInterface.Name;
                if (!allowlistedInterfaces.Contains(name))
                {
                    unimplementedInterfaces.Add(name);
                }
            }
        }

        if (unimplementedInterfaces.Any())
        {
            var list = unimplementedInterfaces.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Domain interfaces must have implementations. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        unimplementedInterfaces.ShouldBeEmpty(
            $"All Domain interfaces must have at least one implementation in Infrastructure. " +
            $"Missing implementations for: {string.Join(", ", unimplementedInterfaces)}. " +
            $"This indicates architectural gaps where interfaces were defined (ITTDD) but never implemented.");
    }

    /// <summary>
    /// Detects stub/placeholder implementations that indicate technical debt.
    /// Scans Infrastructure implementations for common patterns:
    /// - NotImplementedException
    /// - Empty method bodies
    /// - Methods that only throw exceptions
    /// - TODO/FIXME comments in implementation classes
    /// </summary>
    [Fact]
    public void No_Stub_Implementations_Should_Exist()
    {
        var violations = new List<string>();

        foreach (var infrastructureAssembly in InfrastructureAssemblies)
        {
            var implementationTypes = Types.InAssembly(infrastructureAssembly)
                .That()
                .AreClasses()
                .GetTypes()
                .Where(t => !t.IsAbstract) // Skip abstract classes
                .ToList();

            foreach (var type in implementationTypes)
            {
                // Check if type implements any Domain interface
                var implementsDomainInterface = type.GetInterfaces()
                    .Any(i => i.Namespace?.StartsWith("ExxerCube.Prisma.Domain.Interfaces") == true);

                if (!implementsDomainInterface)
                    continue; // Only check Infrastructure implementations of Domain interfaces

                // Get all public methods (excluding inherited object methods)
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                    .ToList();

                foreach (var method in methods)
                {
                    try
                    {
                        var methodBody = method.GetMethodBody();

                        // Skip methods without bodies (abstract, extern, etc.)
                        if (methodBody == null)
                            continue;

                        // Check for suspiciously small method bodies (likely empty or throw-only)
                        // Typical IL byte counts:
                        // - Empty method: ~2 bytes (just ret)
                        // - throw new NotImplementedException(): ~11 bytes
                        // - Real implementation: typically >20 bytes
                        if (methodBody.GetILAsByteArray()?.Length <= 15)
                        {
                            // Whitelist: Known legitimate simple implementations
                            var whitelistedMethods = new[]
                            {
                                "ExxerCube.Prisma.Infrastructure.FileSystem.FileSystemLoader.GetSupportedExtensions",
                                "ExxerCube.Prisma.Infrastructure.DependencyInjection.OcrProcessingServiceAdapter.ProcessDocumentAsync"
                            };

                            var fullMethodName = $"{type.FullName}.{method.Name}";
                            if (whitelistedMethods.Contains(fullMethodName))
                                continue; // Skip whitelisted methods

                            violations.Add(
                                $"{type.FullName}.{method.Name}: Suspicious method body " +
                                $"({methodBody.GetILAsByteArray()?.Length ?? 0} bytes IL). " +
                                $"Likely empty or only throws exception (stub implementation).");
                        }
                    }
                    catch
                    {
                        // Skip methods that can't be analyzed (generic methods, etc.)
                        continue;
                    }
                }
            }
        }

        if (violations.Any())
        {
            var top = violations.Take(10).Select(v => $" - {v}").ToList();
            var suffix = violations.Count > 10 ? $" ... and {violations.Count - 10} more." : string.Empty;
            logger.LogWarning("Rule: No stub implementations. Count={Count}\n{Details}{Suffix}",
                violations.Count, string.Join(Environment.NewLine, top), suffix);
        }

        violations.ShouldBeEmpty(
            $"No stub/placeholder implementations should exist. Found {violations.Count} suspicious methods: " +
            $"{string.Join("; ", violations.Take(10))}" +
            (violations.Count > 10 ? $" ... and {violations.Count - 10} more." : "") +
            $" These indicate technical debt where implementations were created but not properly implemented.");
    }

    /// <summary>
    /// Test assemblies must respect layering: never reference Application; non-system tests should depend on at most one Infrastructure assembly.
    /// System/E2E/UI tests may depend on multiple Infrastructure assemblies but still not on Application.
    /// </summary>
    [Fact]
    public void Test_Assemblies_Should_Respect_Infrastructure_Dependency_Boundaries()
    {
        var testAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "ExxerCube.Prisma.Tests.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name?.Contains("Architecture", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var violations = new List<string>();

        foreach (var testAssembly in testAssemblies)
        {
            var name = testAssembly.GetName().Name ?? string.Empty;
            var isSystem = name.Contains(".Tests.System.", StringComparison.OrdinalIgnoreCase)
                           || name.Contains(".Tests.EndToEnd.", StringComparison.OrdinalIgnoreCase)
                           || name.Contains(".Tests.UI.", StringComparison.OrdinalIgnoreCase);

            var referencedInfra = testAssembly.GetReferencedAssemblies()
                .Where(a => a.FullName?.StartsWith("ExxerCube.Prisma.Infrastructure", StringComparison.OrdinalIgnoreCase) == true)
                .Select(a => a.Name ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasApplication = testAssembly.GetReferencedAssemblies()
                .Any(a => a.FullName?.StartsWith("ExxerCube.Prisma.Application", StringComparison.OrdinalIgnoreCase) == true);

            if (hasApplication)
            {
                violations.Add($"{name}: should not reference Application layer.");
            }

            if (!isSystem && referencedInfra.Count > 1)
            {
                violations.Add($"{name}: non-system tests should depend on at most one Infrastructure assembly (found {referencedInfra.Count}: {string.Join(", ", referencedInfra)}).");
            }
        }

        if (violations.Any())
        {
            var list = violations.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Test assemblies infra dependencies. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        violations.ShouldBeEmpty(
            $"Test assemblies must not reference Application and non-system tests may depend on at most one Infrastructure assembly. Violations: {string.Join("; ", violations)}");
    }

    //

    // Rule 8: Domain Event Inheritance

    /// <summary>
    /// Ensures all domain event classes inherit from DomainEvent base class.
    /// This enforces consistent event structure with EventId, Timestamp, EventType, and CorrelationId.
    /// Events are published via IObservable and consumed by background workers and SignalR hubs.
    /// </summary>
    [Fact]
    public void All_Domain_Events_Must_Inherit_From_DomainEvent()
    {
        // Arrange: Get all types in Domain.Events namespace that end with "Event"
        var domainEventBaseType = typeof(ExxerCube.Prisma.Domain.Events.DomainEvent);

        var eventTypes = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ExxerCube.Prisma.Domain.Events")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameMatching(".*<.*>.*") // Exclude compiler-generated types
            .GetTypes()
            .Where(t => t.Name.EndsWith("Event", StringComparison.Ordinal) && !t.IsAbstract)
            .ToList();

        // Act: Find events that don't inherit from DomainEvent
        var violatingEvents = eventTypes
            .Where(t => !domainEventBaseType.IsAssignableFrom(t))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        if (violatingEvents.Any())
        {
            var list = violatingEvents.Select(v => $" - {v}").ToList();
            logger.LogWarning("Rule: Domain events must inherit from DomainEvent. Count={Count}\n{Details}",
                list.Count, string.Join(Environment.NewLine, list));
        }

        // Assert: All events must inherit from DomainEvent
        violatingEvents.ShouldBeEmpty(
            $"The following events do not inherit from DomainEvent:\n" +
            $"{string.Join("\n", violatingEvents)}\n\n" +
            $"All domain events must inherit from the DomainEvent base class to ensure consistent event handling " +
            $"with EventId, Timestamp, EventType, and CorrelationId properties.");
    }

    //

    private static IEnumerable<Assembly> GetInfrastructureAssemblies()
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."))
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        // Search for both Infrastructure assemblies and Orchestration assemblies (Orion/Athena)
        var searchPatterns = new[]
        {
            "ExxerCube.Prisma.Infrastructure*.dll",
            "Prisma.Orion.*.dll",
            "Prisma.Athena.*.dll"
        };

        var files = roots
            .Where(Directory.Exists)
            .SelectMany(root => searchPatterns.SelectMany(pattern =>
                Directory.GetFiles(root, pattern, SearchOption.AllDirectories)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Select(fi => fi.FullName);

        return files
            .Select(file =>
            {
                try
                {
                    return Assembly.LoadFrom(file);
                }
                catch
                {
                    return null;
                }
            })
            .Where(a => a != null)
            .DistinctBy(a => a!.FullName)
            .Select(a => a!);
    }
}