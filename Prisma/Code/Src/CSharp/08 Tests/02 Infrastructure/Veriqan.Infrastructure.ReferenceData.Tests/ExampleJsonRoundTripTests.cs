using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Verifies that the shipped example JSON file round-trips through the C# model
/// and validates against the JSON Schema contract.
/// </summary>
public sealed class ExampleJsonRoundTripTests
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    private static string ExampleJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "vec-reference-bundle.example.json");

    [Fact]
    public void ExampleJson_Deserializes_ToVecReferenceBundle()
    {
        // Arrange
        var json = File.ReadAllText(ExampleJsonPath);

        // Act
        var bundle = JsonSerializer.Deserialize<VecReferenceBundle>(json, DeserializeOptions);

        // Assert
        bundle.ShouldNotBeNull();
        bundle.BundleMetadata.ShouldNotBeNull();
        bundle.BundleMetadata.SchemaVersion.ShouldBe("1.0.0");
        bundle.BundleMetadata.Institution.ShouldBe("Demo Bank (Iqubica)");
        bundle.Products.ShouldNotBeNull();
        bundle.Products!.Count.ShouldBe(3);
        bundle.InterestRates.ShouldNotBeNull();
        bundle.ToleranceConfig.ShouldNotBeNull();
        bundle.ValidationConstants.ShouldNotBeNull();
    }

    [Fact]
    public void ExampleJson_RoundTrip_PreservesData()
    {
        var json = File.ReadAllText(ExampleJsonPath);
        var bundle = JsonSerializer.Deserialize<VecReferenceBundle>(json, DeserializeOptions);
        bundle.ShouldNotBeNull();

        // Re-serialize
        var reSerializedJson = JsonSerializer.Serialize(bundle, SerializeOptions);
        var roundTripped = JsonSerializer.Deserialize<VecReferenceBundle>(reSerializedJson, DeserializeOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped!.BundleMetadata.Institution.ShouldBe(bundle.BundleMetadata.Institution);
        roundTripped.BundleMetadata.SchemaVersion.ShouldBe(bundle.BundleMetadata.SchemaVersion);
        roundTripped.Products!.Count.ShouldBe(bundle.Products!.Count);
        roundTripped.InterestRates!.Count.ShouldBe(bundle.InterestRates!.Count);
    }

    [Fact]
    public void ExampleJson_PassesSchemaValidation()
    {
        var json = File.ReadAllText(ExampleJsonPath);
        var bundle = JsonSerializer.Deserialize<VecReferenceBundle>(json, DeserializeOptions);
        bundle.ShouldNotBeNull();

        var validator = new ReferenceBundleSchemaValidator();
        var result = validator.Validate(bundle!);

        result.IsSuccess.ShouldBeTrue(
            $"Example JSON should pass schema validation but got: {result.Error}");
    }

    [Fact]
    public void ExampleJson_ProductAliases_RoundTrip()
    {
        var json = File.ReadAllText(ExampleJsonPath);
        var bundle = JsonSerializer.Deserialize<VecReferenceBundle>(json, DeserializeOptions);

        var nlProduct = bundle!.Products!.FirstOrDefault(p => p.ProductId == "TC-NL");
        nlProduct.ShouldNotBeNull();
        nlProduct!.Aliases.ShouldNotBeNull();
        nlProduct.Aliases!.ShouldContain("NL");
    }
}
