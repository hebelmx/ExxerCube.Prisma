using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 3.3a — regression coverage for <see cref="ProductCatalogMembershipValidator"/>, the
/// extraction-side mirror of the downstream BundleBinder catalog gate.
/// </summary>
/// <remarks>
/// Resolves the <b>real, production</b> <see cref="IProductResolver"/> via
/// <c>AddVeriqanBinding()</c> — the same DI seam <see cref="SyntheticHeaderOcrProductTests"/> and
/// the T1 snapshot test use — rather than a hand-rolled fake, so this suite actually exercises the
/// FuzzySharp matcher/threshold/ambiguity-margin behavior the validator depends on, not a stand-in
/// for it. <c>ProductResolver</c> itself is <c>internal</c> to <c>Veriqan.Application</c> and this
/// test project has no <c>InternalsVisibleTo</c> grant for it, so DI resolution through the public
/// <see cref="IProductResolver"/> port is the only (and the correct, production-faithful) way to
/// obtain it here.
/// </remarks>
public sealed class ProductCatalogMembershipValidatorTests
{
    private static IProductResolver CreateRealResolver()
    {
        var services = new ServiceCollection();
        services.AddVeriqanBinding();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IProductResolver>();
    }

    private static VecReferenceBundle CatalogWith(params VecProduct[] products) => new(
        BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
        Products: products,
        InterestRates: null,
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        ClientAccounts: null,
        PriorStatements: null,
        ExpectedTransactions: null,
        ToleranceConfig: null,
        ValidationConstants: null);

    private static readonly VecProduct CostcoBanamex = new(
        ProductId: "TC-COSTCO-BANAMEX",
        ProductName: "Tarjeta de Crédito COSTCO BANAMEX",
        Aliases: ["COSTCO BANAMEX"],
        HasRewardsProgram: false,
        CardImage: null,
        ImportantMessageImage: null,
        Tariffs: null);

    [Fact]
    public void IsValid_TokenInCatalog_ReturnsTrue()
    {
        var validator = new ProductCatalogMembershipValidator(CreateRealResolver(), CatalogWith(CostcoBanamex));

        validator.IsValid("Tarjeta de Crédito COSTCO BANAMEX").ShouldBeTrue();
    }

    [Fact]
    public void IsValid_TokenNotInCatalog_ReturnsFalse()
    {
        var validator = new ProductCatalogMembershipValidator(CreateRealResolver(), CatalogWith(CostcoBanamex));

        validator.IsValid("Tarjeta de Crédito Platino Santander").ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NullOrEmptyOrWhitespace_ReturnsFalse(string? token)
    {
        var validator = new ProductCatalogMembershipValidator(CreateRealResolver(), CatalogWith(CostcoBanamex));

        validator.IsValid(token).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NonStringValue_ReturnsFalse()
    {
        var validator = new ProductCatalogMembershipValidator(CreateRealResolver(), CatalogWith(CostcoBanamex));

        // The Product field is always string-typed, but IFieldValidator.IsValid is deliberately
        // non-generic (object?) — a non-string value must be rejected, not throw.
        validator.IsValid(42).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_OcrNoisyButWithinFuzzyThreshold_ReturnsTrue()
    {
        var validator = new ProductCatalogMembershipValidator(CreateRealResolver(), CatalogWith(CostcoBanamex));

        // Small OCR noise (missing accent, one dropped letter) — within ProductResolver's
        // FuzzyScoreThreshold (85) of the catalog's ProductName/alias, so the fuzzy fallback pass
        // (owner ruling 2) must still resolve it, exactly as the downstream binder would.
        validator.IsValid("Tarjeta de Credito COSTC0 BANAMEX").ShouldBeTrue();
    }
}
