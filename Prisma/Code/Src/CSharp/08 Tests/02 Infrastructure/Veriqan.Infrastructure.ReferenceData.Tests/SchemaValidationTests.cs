using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Tests that the schema validator correctly accepts valid bundles and rejects invalid ones.
/// </summary>
public sealed class SchemaValidationTests
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string ExampleJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "vec-reference-bundle.example.json");

    private static VecReferenceBundle LoadExampleBundle()
    {
        var json = File.ReadAllText(ExampleJsonPath);
        return JsonSerializer.Deserialize<VecReferenceBundle>(json, DeserializeOptions)!;
    }

    [Fact]
    public void Validate_ValidBundle_ReturnsSuccess()
    {
        var validator = new ReferenceBundleSchemaValidator();
        var bundle = LoadExampleBundle();

        var result = validator.Validate(bundle);

        result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public void Validate_BundleMissingInterestRates_StillValid_GracefulDegradation()
    {
        // Arrange: remove the TASA section entirely — schema says it's optional
        var fullBundle = LoadExampleBundle();
        var bundleWithoutRates = fullBundle with { InterestRates = null };

        var validator = new ReferenceBundleSchemaValidator();

        // Act
        var result = validator.Validate(bundleWithoutRates);

        // Assert: should PASS because interestRates is optional in the schema
        result.IsSuccess.ShouldBeTrue(
            $"A bundle missing interestRates should still be schema-valid (graceful degradation). Error: {result.Error}");
    }

    [Fact]
    public void Validate_BundleMissingAllOptionalSections_IsValid()
    {
        // Only bundleMetadata is required
        var minimalBundle = new VecReferenceBundle(
            BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
            Products: null,
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null
        );

        var validator = new ReferenceBundleSchemaValidator();
        var result = validator.Validate(minimalBundle);

        result.IsSuccess.ShouldBeTrue(
            $"Minimal bundle with only bundleMetadata should be valid. Error: {result.Error}");
    }
}
