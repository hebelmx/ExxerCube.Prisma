using System.Reflection;
using System.Text.Json;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Events;

/// <summary>
/// Tests that verify the single-<see cref="Confidence"/> value-object design of
/// <see cref="ClassificationCompletedEvent"/> (Story 2.9).
/// </summary>
public class ClassificationCompletedEventConfidenceTests
{
    /// <summary>
    /// Verifies via reflection that <see cref="ClassificationCompletedEvent"/> exposes exactly
    /// one confidence-related public property — <c>Confidence</c> of type
    /// <see cref="ValueObjects.Confidence"/> — and that the removed <c>int Confidence</c> and
    /// <c>double ConfidenceScore</c> properties are no longer present.
    /// </summary>
    [Fact]
    public void ClassificationCompletedEvent_CarriesSingleConfidenceField()
    {
        var type = typeof(ClassificationCompletedEvent);

        // The one allowed confidence property must exist with the correct type.
        var confidenceProperty = type.GetProperty(nameof(ClassificationCompletedEvent.Confidence),
            BindingFlags.Public | BindingFlags.Instance);

        confidenceProperty.ShouldNotBeNull("Expected a public property named 'Confidence' of type Confidence.");
        confidenceProperty!.PropertyType.ShouldBe(typeof(Confidence),
            "ClassificationCompletedEvent.Confidence must be of type Confidence (the VO), not int or double.");

        // No legacy int Confidence or double ConfidenceScore must remain.
        var allPublicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var confidenceProperties = allPublicProperties
            .Where(p => p.Name.Contains("Confidence", StringComparison.OrdinalIgnoreCase))
            .ToList();

        confidenceProperties.Count.ShouldBe(1,
            $"Expected exactly ONE confidence-related public property, found: {string.Join(", ", confidenceProperties.Select(p => $"{p.PropertyType.Name} {p.Name}"))}");

        confidenceProperties[0].Name.ShouldBe(nameof(ClassificationCompletedEvent.Confidence));
        confidenceProperties[0].PropertyType.ShouldBe(typeof(Confidence));
    }

    /// <summary>
    /// Verifies that a <see cref="ClassificationCompletedEvent"/> with a
    /// <see cref="ValueObjects.Confidence"/> value of 0.85 survives a full
    /// System.Text.Json serialize → deserialize round-trip.
    /// </summary>
    [Fact]
    public void ClassificationCompletedEvent_RoundTrips_ThroughJson()
    {
        var original = new ClassificationCompletedEvent
        {
            FileId = Guid.NewGuid(),
            RequirementTypeId = 1,
            RequirementTypeName = "Aseguramiento",
            Confidence = Confidence.FromInt(85),
            RequiresManualReview = false,
            RelationType = "NewRequirement"
        };

        var json = JsonSerializer.Serialize(original);
        json.ShouldNotBeNullOrWhiteSpace();

        var restored = JsonSerializer.Deserialize<ClassificationCompletedEvent>(json);

        restored.ShouldNotBeNull();
        restored!.Confidence.Value.ShouldBe(0.85,
            "Confidence.Value must survive a JSON round-trip unchanged.");
        restored.FileId.ShouldBe(original.FileId);
        restored.RequirementTypeName.ShouldBe(original.RequirementTypeName);
    }
}
