// <copyright file="ScaffoldingSmokeTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.DependencyInjection;
using ExxerCube.Prisma.QaHarness.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Smoke test that verifies the Chunk-0 scaffold compiles, the DI container wires
/// <see cref="IWorkflowRunner"/>, and the test runner itself is functional.
/// This is a <c>fast</c> test — no Docker, no HTTP, no external processes.
/// </summary>
public sealed class ScaffoldingSmokeTests
{
    /// <summary>
    /// Verifies that <c>AddQaHarness()</c> resolves <see cref="IWorkflowRunner"/>
    /// and that its <see cref="IWorkflowRunner.AvailableWorkflows"/> collection is
    /// non-empty — proving the DI wiring succeeds end-to-end from service registration
    /// through to the workflow catalog.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_ResolvesIWorkflowRunner_WithPopulatedWorkflowCatalog()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
        services.AddQaHarness();

        // Act
        using var sp = services.BuildServiceProvider();
        var runner = sp.GetService<IWorkflowRunner>();

        // Assert — IWorkflowRunner resolved and has the expected six catalog workflows.
        runner.ShouldNotBeNull();
        runner!.AvailableWorkflows.ShouldNotBeEmpty(
            "IWorkflowRunner must expose all registered catalog workflows after AddQaHarness().");
        runner.AvailableWorkflows.Count.ShouldBe(6,
            $"Expected 6 catalog workflows; got {runner.AvailableWorkflows.Count}: " +
            string.Join(", ", runner.AvailableWorkflows.Select(w => w.Name)));
    }
}
