// <copyright file="ScaffoldingSmokeTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Smoke tests that verify the Chunk-0 scaffold compiles and the test runner is wired correctly.
/// These tests carry the <c>fast</c> category and require no external dependencies.
/// </summary>
public sealed class ScaffoldingSmokeTests
{
    /// <summary>
    /// Verifies that the test project builds and the MTP runner executes at least one test.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void Scaffolding_Smoke_Passes()
    {
        true.ShouldBeTrue();
    }
}
