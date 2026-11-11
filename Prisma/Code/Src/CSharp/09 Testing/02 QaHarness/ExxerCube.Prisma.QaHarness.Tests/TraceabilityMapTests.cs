// <copyright file="TraceabilityMapTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text.Json;
using ExxerCube.Prisma.QaHarness.Traceability;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for <see cref="TraceabilityMap"/>. No Docker required.
/// </summary>
public sealed class TraceabilityMapTests
{
    /// <summary>
    /// An entry added to the map should be retrievable by any of its referenced requirement IDs.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void TraceabilityMap_AddEntry_CanBeRetrievedByRefId()
    {
        // Arrange
        var map = new TraceabilityMap();
        var entry = new TraceabilityEntry(
            SourceId: "LoginWorkflow",
            SourceKind: TraceabilitySourceKind.Workflow,
            Requirements: [new RequirementRef("REQ-A1-01", "SIARA Login", "§A1")],
            Features: [new FeatureRef("F-AUTH", "Authentication")],
            Invariants: [],
            RecordedAt: DateTimeOffset.UtcNow);

        // Act
        map.AddEntry(entry);

        // Assert — retrievable by requirement ID
        var byReq = map.GetEntriesFor("REQ-A1-01");
        byReq.ShouldNotBeEmpty();
        byReq[0].SourceId.ShouldBe("LoginWorkflow");

        // Assert — retrievable by feature ID
        var byFeat = map.GetEntriesFor("F-AUTH");
        byFeat.ShouldNotBeEmpty();
        byFeat[0].SourceId.ShouldBe("LoginWorkflow");

        // Assert — not found for unknown ID
        var notFound = map.GetEntriesFor("REQ-UNKNOWN");
        notFound.ShouldBeEmpty();
    }

    /// <summary>
    /// After multiple entries are added, <see cref="TraceabilityMap.GetAllEntries"/> should
    /// return all of them.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void TraceabilityMap_GetAllEntries_ReturnsAllAdded()
    {
        var map = new TraceabilityMap();

        var e1 = new TraceabilityEntry(
            "Workflow1", TraceabilitySourceKind.Workflow,
            [new RequirementRef("REQ-001", "Req one")],
            [], [], DateTimeOffset.UtcNow);

        var e2 = new TraceabilityEntry(
            "Validator1", TraceabilitySourceKind.Validator,
            [], [new FeatureRef("F-001", "Feature one")], [], DateTimeOffset.UtcNow);

        map.AddEntry(e1);
        map.AddEntry(e2);

        var all = map.GetAllEntries();
        all.Count.ShouldBe(2);
        all.Any(e => e.SourceId == "Workflow1").ShouldBeTrue();
        all.Any(e => e.SourceId == "Validator1").ShouldBeTrue();
    }

    /// <summary>
    /// <see cref="TraceabilityMap.SerializeToJsonAsync"/> should produce valid JSON that can
    /// be parsed back and contains the expected SourceId values.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task TraceabilityMap_SerializeToJson_ProducesValidJson()
    {
        // Arrange
        var map = new TraceabilityMap();
        map.AddEntry(new TraceabilityEntry(
            "HealthCheckWorkflow",
            TraceabilitySourceKind.Workflow,
            [new RequirementRef("REQ-H1", "Health endpoint")],
            [],
            [new InvariantRef("INV-001", "Service must respond to /health/live")],
            DateTimeOffset.UtcNow));

        var tmpPath = Path.Combine(Path.GetTempPath(), $"traceability-{Guid.NewGuid():N}.json");

        try
        {
            // Act
            var result = await map.SerializeToJsonAsync(
                tmpPath, TestContext.Current.CancellationToken);

            // Assert — operation succeeded
            result.IsSuccess.ShouldBeTrue(result.Error ?? "SerializeToJsonAsync failed");

            // Assert — file is valid JSON
            var json = await File.ReadAllTextAsync(tmpPath, TestContext.Current.CancellationToken);
            json.ShouldNotBeNullOrWhiteSpace();

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
            doc.RootElement.GetArrayLength().ShouldBe(1);

            // Assert — entry SourceId round-trips correctly
            var sourceId = doc.RootElement[0].GetProperty("SourceId").GetString();
            sourceId.ShouldBe("HealthCheckWorkflow");
        }
        finally
        {
            try { File.Delete(tmpPath); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// Look-up is case-insensitive (e.g. "req-a1-01" should match "REQ-A1-01").
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void TraceabilityMap_LookupIsCaseInsensitive()
    {
        var map = new TraceabilityMap();
        map.AddEntry(new TraceabilityEntry(
            "TestSource", TraceabilitySourceKind.Manual,
            [new RequirementRef("REQ-CASE", "Case check")],
            [], [], DateTimeOffset.UtcNow));

        var result = map.GetEntriesFor("req-case");
        result.ShouldNotBeEmpty();
    }
}
