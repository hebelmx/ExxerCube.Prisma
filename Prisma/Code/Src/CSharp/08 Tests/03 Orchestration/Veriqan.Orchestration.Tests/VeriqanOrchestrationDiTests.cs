using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Tests that verify the DI registrations in <see cref="VeriqanOrchestrationExtensions"/>
/// produce exactly one descriptor per service — guarding against double-registration bugs
/// that cause orphaned singletons.
/// </summary>
public sealed class VeriqanOrchestrationDiTests
{
    /// <summary>
    /// <see cref="VeriqanOrchestrationExtensions.AddVeriqan"/> called without a connection
    /// string falls through to <see cref="VeriqanOrchestrationExtensions.AddVeriqanInMemoryPersistence"/>.
    /// Both code paths previously registered <see cref="IVerificationResultStore"/> and
    /// <see cref="IReprocessAuditRepository"/> — an unconditional <c>AddSingleton</c> inside
    /// <c>AddVeriqan</c> PLUS a <c>TryAddSingleton</c> inside <c>AddVeriqanInMemoryPersistence</c>.
    /// The fix changes the outer registrations to <c>TryAddSingleton</c> so only one descriptor
    /// is ever present regardless of which code path runs.
    /// </summary>
    [Fact]
    public void AddVeriqan_NoConnectionString_RegistersVerificationResultStoreSinglyOnly()
    {
        // Arrange — build a configuration with NO "ConnectionStrings:VeriqanDb" so the
        // in-memory persistence path is taken.
        var config = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddVeriqan(config);

        // Assert — exactly one descriptor for IVerificationResultStore.
        var resultStoreDescriptors = services
            .Where(d => d.ServiceType == typeof(IVerificationResultStore))
            .ToList();

        resultStoreDescriptors.Count.ShouldBe(1,
            "IVerificationResultStore must be registered exactly once. " +
            "A second AddSingleton after AddVeriqanInMemoryPersistence creates an orphaned singleton.");

        // Assert — exactly one descriptor for IReprocessAuditRepository.
        var auditRepoDescriptors = services
            .Where(d => d.ServiceType == typeof(IReprocessAuditRepository))
            .ToList();

        auditRepoDescriptors.Count.ShouldBe(1,
            "IReprocessAuditRepository must be registered exactly once. " +
            "A second AddSingleton after AddVeriqanInMemoryPersistence creates an orphaned singleton.");
    }

    /// <summary>
    /// <see cref="VeriqanOrchestrationExtensions.AddVeriqan"/> must bind the CSV reference-data
    /// root from the <c>Veriqan:CsvReferenceData:RootDirectory</c> configuration key (the same
    /// key the docker-compose env var <c>Veriqan__CsvReferenceData__RootDirectory</c> maps to).
    /// Regression guard: previously the root was registered with an empty options action, so the
    /// configured value was silently ignored and the worker readiness probe failed
    /// <c>csvReferenceDataRoot</c>.
    /// </summary>
    [Fact]
    public void AddVeriqan_BindsCsvReferenceDataRootFromConfiguration()
    {
        // Arrange — configuration supplying only the CSV root directory.
        const string expectedRoot = "/data/veriqan/reference-bundles";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veriqan:CsvReferenceData:RootDirectory"] = expectedRoot,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddVeriqan(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CsvReferenceDataOptions>>();

        // Assert — the configured root is bound onto the options.
        options.Value.RootDirectory.ShouldBe(expectedRoot,
            "AddVeriqan must bind CsvReferenceDataOptions.RootDirectory from " +
            "Veriqan:CsvReferenceData:RootDirectory so the worker readiness probe can find the bundle mount.");
    }

    /// <summary>
    /// Calling <see cref="VeriqanOrchestrationExtensions.AddVeriqanInMemoryPersistence"/> directly
    /// (as the E2E tests do) must also register each interface exactly once.
    /// </summary>
    [Fact]
    public void AddVeriqanInMemoryPersistence_RegistersBothInterfacesSinglyOnly()
    {
        var services = new ServiceCollection();

        // Act
        services.AddVeriqanInMemoryPersistence();

        var resultStoreDescriptors = services
            .Where(d => d.ServiceType == typeof(IVerificationResultStore))
            .ToList();

        resultStoreDescriptors.Count.ShouldBe(1,
            "IVerificationResultStore must appear once when AddVeriqanInMemoryPersistence is called.");

        var auditRepoDescriptors = services
            .Where(d => d.ServiceType == typeof(IReprocessAuditRepository))
            .ToList();

        auditRepoDescriptors.Count.ShouldBe(1,
            "IReprocessAuditRepository must appear once when AddVeriqanInMemoryPersistence is called.");
    }
}
