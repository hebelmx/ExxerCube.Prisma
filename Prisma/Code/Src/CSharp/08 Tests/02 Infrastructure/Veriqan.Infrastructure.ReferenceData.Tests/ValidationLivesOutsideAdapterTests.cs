using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Confirms that <see cref="ReferenceBundleSchemaValidator"/> is an independent component
/// (not nested inside the adapter) and can be used by any future adapter without
/// duplicating schema-engine wiring (ADR-V4 requirement).
/// </summary>
public sealed class ValidationLivesOutsideAdapterTests
{
    [Fact]
    public void ReferenceBundleSchemaValidator_CanBeInstantiatedIndependently()
    {
        // The validator must be constructable without the adapter
        var validator = new ReferenceBundleSchemaValidator();
        validator.ShouldNotBeNull();
    }

    [Fact]
    public void ReferenceBundleSchemaValidator_AcceptsMinimalValidBundle()
    {
        var validator = new ReferenceBundleSchemaValidator();
        var bundle = new VecReferenceBundle(
            BundleMetadata: new BundleMetadata("1.0.0", "Any Bank", null, null, null, null),
            Products: null, InterestRates: null, MandatoryLegends: null,
            SequentialImages: null, Promotions: null, ClientAccounts: null,
            PriorStatements: null, ExpectedTransactions: null,
            ToleranceConfig: null, ValidationConstants: null
        );

        var result = validator.Validate(bundle);
        result.IsSuccess.ShouldBeTrue($"Minimal valid bundle should pass. Error: {result.Error}");
    }
}
