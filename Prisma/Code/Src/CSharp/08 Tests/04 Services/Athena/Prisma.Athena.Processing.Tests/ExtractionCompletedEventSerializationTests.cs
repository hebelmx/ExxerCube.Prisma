using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Pins the System.Text.Json round-trip contract of <see cref="ExtractionCompletedEvent"/>
/// across the Extractor → Reconciliator SignalR edge (MVP-PATH 1.4, ADR-011, GH #6).
/// </summary>
/// <remarks>
/// <see cref="ExtractionCompletedEvent.IsComplete"/> is a plain <c>bool</c> (not a SmartEnum),
/// so it does NOT suffer the SmartEnum serialization defect documented in
/// <see cref="DocumentDownloadedEventSerializationTests"/>.  These tests exist to:
/// <list type="bullet">
///   <item>Pin <see cref="ExtractionCompletedEvent.IsComplete"/> as an explicit part of the
///         cross-process wire contract so any future rename/removal is caught at compile time.</item>
///   <item>Prove that the default JSON options (no custom converter required) faithfully
///         carry <c>IsComplete=false</c> across a round-trip — the most important case for
///         GH #6 (a partial case must not be silently promoted to complete on the wire).</item>
///   <item>Prove the same under the SignalR Web defaults (camelCase + case-insensitive) that
///         the live Extractor → Reconciliator edge uses.</item>
/// </list>
/// </remarks>
public sealed class ExtractionCompletedEventSerializationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ExtractionCompletedEvent CreateEvent(bool isComplete) => new()
    {
        EventId = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow,
        CorrelationId = Guid.NewGuid(),
        FileId = Guid.NewGuid(),
        Path = "2026/06/14/CASE-1/doc.fusion.json",
        FieldsFused = 5,
        ConflictsDetected = 0,
        ClearanceToken = "test-token",
        IsComplete = isComplete,
    };

    // -------------------------------------------------------------------------
    // TC-1: IsComplete=false survives default STJ round-trip
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given an <see cref="ExtractionCompletedEvent"/> with <c>IsComplete = false</c>,
    /// when serialized and deserialized using the default <see cref="JsonSerializerOptions"/>,
    /// then the restored event's <c>IsComplete</c> is still <see langword="false"/>.
    /// This is the critical cross-process wire contract pin for GH #6 partial-case handling:
    /// a missing-companion partial must never be silently promoted to complete on the wire.
    /// </summary>
    [Fact]
    public void RoundTrip_DefaultOptions_IsCompleteFalse_Preserved()
    {
        // Arrange
        var original = CreateEvent(isComplete: false);

        // Act
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ExtractionCompletedEvent>(json);

        // Assert
        restored.ShouldNotBeNull();
        restored!.IsComplete.ShouldBeFalse(
            "IsComplete=false must survive the round-trip — the Reconciliator must see the partial flag");
    }

    // -------------------------------------------------------------------------
    // TC-2: IsComplete=true survives default STJ round-trip
    // -------------------------------------------------------------------------

    /// <summary>
    /// Symmetric counterpart: <c>IsComplete = true</c> (the default) also survives the round-trip.
    /// Ensures the flag is not hardcoded false somewhere in the serialization path.
    /// </summary>
    [Fact]
    public void RoundTrip_DefaultOptions_IsCompleteTrue_Preserved()
    {
        // Arrange
        var original = CreateEvent(isComplete: true);

        // Act
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ExtractionCompletedEvent>(json);

        // Assert
        restored.ShouldNotBeNull();
        restored!.IsComplete.ShouldBeTrue(
            "IsComplete=true must survive the round-trip");
    }

    // -------------------------------------------------------------------------
    // TC-3: IsComplete=false survives the SignalR Web-defaults round-trip (camelCase)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given the Web defaults that SignalR's <c>JsonHubProtocol</c> uses (camelCase + case-insensitive),
    /// when an event with <c>IsComplete = false</c> is serialized and deserialized,
    /// then all key properties are faithfully preserved — including <c>IsComplete</c>,
    /// <c>FileId</c>, <c>Path</c>, and <c>ClearanceToken</c>.
    /// </summary>
    [Fact]
    public void RoundTrip_WebDefaults_IsCompleteFalse_PreservesAllProperties()
    {
        // Arrange — Web defaults (camelCase keys, case-insensitive deserialization)
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var original = CreateEvent(isComplete: false);

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<ExtractionCompletedEvent>(json, options);

        // Assert
        restored.ShouldNotBeNull();
        restored!.IsComplete.ShouldBeFalse(
            "IsComplete=false must survive the SignalR/Web camelCase round-trip");
        restored.FileId.ShouldBe(original.FileId,
            "FileId must be preserved across the wire");
        restored.Path.ShouldBe(original.Path,
            "Path must be preserved across the wire");
        restored.ClearanceToken.ShouldBe(original.ClearanceToken,
            "ClearanceToken must be preserved across the wire");
        restored.FieldsFused.ShouldBe(original.FieldsFused,
            "FieldsFused must be preserved across the wire");
    }
}
