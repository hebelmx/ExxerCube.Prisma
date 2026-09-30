// <copyright file="HostingRecordsTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Text.Json;
using ExxerCube.Prisma.QaHarness.Hosting;
using Shouldly;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for the Chunk-2B hosting records (<see cref="ApplicationStartupResult"/>,
/// <see cref="HostingOptions"/>, <see cref="HostingMode"/>).
/// No Docker, no live processes, no I/O — just type construction and JSON round-trip.
/// </summary>
public sealed class HostingRecordsTests
{
    /// <summary>
    /// Verifies that <see cref="ApplicationStartupResult"/> can be constructed with all fields
    /// and that they are readable after construction.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void ApplicationStartupResult_HealthyVariant_FieldsRoundTrip()
    {
        var baseAddress = new Uri("http://localhost:54321");
        var result = new ApplicationStartupResult(
            IsHealthy: true,
            BaseAddress: baseAddress,
            StartupDurationMs: 1234L,
            FailureReason: null);

        result.IsHealthy.ShouldBeTrue();
        result.BaseAddress.ShouldBe(baseAddress);
        result.StartupDurationMs.ShouldBe(1234L);
        result.FailureReason.ShouldBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="ApplicationStartupResult"/> correctly holds a failure state.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void ApplicationStartupResult_FailureVariant_FieldsRoundTrip()
    {
        var result = new ApplicationStartupResult(
            IsHealthy: false,
            BaseAddress: null,
            StartupDurationMs: 42L,
            FailureReason: "Health probe returned 503");

        result.IsHealthy.ShouldBeFalse();
        result.BaseAddress.ShouldBeNull();
        result.StartupDurationMs.ShouldBe(42L);
        result.FailureReason.ShouldBe("Health probe returned 503");
    }

    /// <summary>
    /// Verifies that <see cref="HostingOptions"/> can be constructed with all parameters and
    /// that defaults are applied correctly.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void HostingOptions_Defaults_AreCorrect()
    {
        var options = new HostingOptions(Mode: HostingMode.WebUiOnly);

        options.Mode.ShouldBe(HostingMode.WebUiOnly);
        options.SqlConnectionString.ShouldBeNull();
        options.SharedStoragePath.ShouldBeNull();
        options.SiaraStorageState.ShouldBeNull();
        options.DisableAutonomousWatchLoop.ShouldBeTrue();  // default is true
    }

    /// <summary>
    /// Verifies that all three <see cref="HostingMode"/> values are distinct and have the expected
    /// ordinal positions (guards against accidental enum reordering).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void HostingMode_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<HostingMode>();

        values.ShouldContain(HostingMode.WebUiOnly);
        values.ShouldContain(HostingMode.ThreeProcessPipeline);
        values.ShouldContain(HostingMode.All);
        values.Length.ShouldBe(3);
    }

    /// <summary>
    /// Verifies that <see cref="ApplicationStartupResult"/> serialises to JSON via
    /// <see cref="System.Text.Json.JsonSerializer"/> and that the key properties
    /// survive a round-trip (deserialise → compare).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void ApplicationStartupResult_JsonRoundTrip_PreservesAllFields()
    {
        var original = new ApplicationStartupResult(
            IsHealthy: true,
            BaseAddress: new Uri("http://127.0.0.1:9876"),
            StartupDurationMs: 750L,
            FailureReason: null);

        var json = JsonSerializer.Serialize(original);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("IsHealthy");
        json.ShouldContain("true");
        json.ShouldContain("750");

        var deserialized = JsonSerializer.Deserialize<ApplicationStartupResult>(json);

        deserialized.ShouldNotBeNull();
        deserialized!.IsHealthy.ShouldBeTrue();
        deserialized.StartupDurationMs.ShouldBe(750L);
        deserialized.FailureReason.ShouldBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="HostingOptions"/> with all fields set can also be serialised
    /// and that the key properties survive a round-trip.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void HostingOptions_JsonRoundTrip_PreservesAllFields()
    {
        var options = new HostingOptions(
            Mode: HostingMode.ThreeProcessPipeline,
            SqlConnectionString: "Server=localhost;Database=Prisma;",
            SharedStoragePath: @"C:\temp\shared",
            SiaraStorageState: @"C:\temp\state.json",
            DisableAutonomousWatchLoop: false);

        // Use JsonStringEnumConverter so the enum name ("ThreeProcessPipeline") appears in JSON
        // rather than the raw integer value.
        var serOpts = new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(options, serOpts);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("ThreeProcessPipeline");

        var deserialized = JsonSerializer.Deserialize<HostingOptions>(json, serOpts);

        deserialized.ShouldNotBeNull();
        deserialized!.Mode.ShouldBe(HostingMode.ThreeProcessPipeline);
        deserialized.SqlConnectionString.ShouldBe("Server=localhost;Database=Prisma;");
        deserialized.DisableAutonomousWatchLoop.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="PrismaWebUiHostController"/> implements <see cref="IApplicationHostController"/>
    /// and can be instantiated without external dependencies (null logger path).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task PrismaWebUiHostController_ImplementsInterface_InstantiatesClean()
    {
        await using var controller = new PrismaWebUiHostController();

        // Before StartAsync: Services and BaseAddress are null.
        controller.Services.ShouldBeNull();
        controller.BaseAddress.ShouldBeNull();

        // Verify it is assignable to the interface.
        IApplicationHostController iface = controller;
        iface.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="ThreeProcessHostController"/> implements <see cref="IApplicationHostController"/>
    /// and can be instantiated without external dependencies (null logger path).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task ThreeProcessHostController_ImplementsInterface_InstantiatesClean()
    {
        await using var controller = new ThreeProcessHostController();

        // Before StartAsync: Services and BaseAddress are null.
        controller.Services.ShouldBeNull();
        controller.BaseAddress.ShouldBeNull();

        IApplicationHostController iface = controller;
        iface.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="PrismaWebUiHostController.StartAsync"/> returns a failure result
    /// when called with a <see cref="HostingMode"/> that is not applicable to the Web UI controller,
    /// without starting any host.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task PrismaWebUiHostController_StartAsync_RejectsWrongMode()
    {
        await using var controller = new PrismaWebUiHostController();
        var options = new HostingOptions(Mode: HostingMode.ThreeProcessPipeline);

        var result = await controller.StartAsync(options, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="ThreeProcessHostController.StartAsync"/> returns a failure result
    /// when called with a <see cref="HostingMode"/> that is not applicable to the three-process controller.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task ThreeProcessHostController_StartAsync_RejectsWrongMode()
    {
        await using var controller = new ThreeProcessHostController();
        var options = new HostingOptions(Mode: HostingMode.WebUiOnly);

        var result = await controller.StartAsync(options, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }
}
