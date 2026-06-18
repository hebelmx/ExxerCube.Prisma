using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
