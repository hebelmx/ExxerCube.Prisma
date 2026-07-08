using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Services;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Tests for <see cref="ProductResolver"/>'s fuzzy-match fallback (E7.S7.2/S7.3 owner ruling 2,
/// 2026-07-08: "fuzzy from day one"). The fallback only runs after the exact-match pass
/// (<see cref="BundleBinderTests"/>) misses every product/alias — these tests target that second
/// pass specifically: recovering from small OCR noise, while still abstaining honestly on a
/// genuinely different product.
/// </summary>
public sealed class ProductResolverFuzzyTests
{
    private static VecReferenceBundle BundleWithCostcoBanamex() => new(
        BundleMetadata: new BundleMetadata("1.0.0", "Demo Bank", null, null, null, null),
        Products:
        [
            new VecProduct(
                ProductId: "TC-COSTCO-BANAMEX",
                ProductName: "Tarjeta de Crédito COSTCO BANAMEX",
                Aliases: ["COSTCO BANAMEX", "Tarjeta de Crédito COSTCO BANAMEX"],
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: null),
        ],
        InterestRates: null,
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        ClientAccounts: null,
        PriorStatements: null,
        ExpectedTransactions: null,
        ToleranceConfig: null,
        ValidationConstants: null);

    // -----------------------------------------------------------------------
    // Fuzzy recovery — small OCR noise still resolves
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_OcrTokenWithExtraWhitespace_FuzzyRecovers()
    {
        var resolver = new ProductResolver();
        var bundle = BundleWithCostcoBanamex();

        // Exact match already handles pure whitespace collapse (Normalise), so use a token with
        // a genuinely different but small edit-distance defect — a doubled letter, as a stray
        // OCR artifact might produce.
        var result = resolver.Resolve("Tarjeta de Creditoo COSTCO BANAMEX", bundle);

        result.IsSuccess.ShouldBeTrue($"Expected fuzzy recovery. Error: {result.Error}");
        result.Value!.ProductId.ShouldBe("TC-COSTCO-BANAMEX");
    }

    [Fact]
    public void Resolve_OcrTokenMissingAccent_FuzzyRecovers()
    {
        var resolver = new ProductResolver();
        var bundle = BundleWithCostcoBanamex();

        var result = resolver.Resolve("Tarjeta de Credito COSTCO BANAMEX", bundle);

        result.IsSuccess.ShouldBeTrue($"Expected fuzzy recovery. Error: {result.Error}");
        result.Value!.ProductId.ShouldBe("TC-COSTCO-BANAMEX");
    }

    // -----------------------------------------------------------------------
    // Honest abstention — never fabricate a match below the threshold
    // -----------------------------------------------------------------------

    /// <summary>
    /// "Alias-starved" regression fixture (design doc §6): a token that does not resemble any
    /// catalog product/alias must still abstain (UnknownProduct) even with fuzzy matching enabled
    /// — the resolver never picks a nearest-guess.
    /// </summary>
    [Fact]
    public void Resolve_UnrelatedToken_StillReturnsUnknownProduct()
    {
        var resolver = new ProductResolver();
        var bundle = BundleWithCostcoBanamex();

        var result = resolver.Resolve("Cuenta de Ahorro Platino", bundle);

        result.IsSuccess.ShouldBeFalse(
            $"Expected UnknownProduct abstention, but resolver matched: {result.Value?.ProductId}");
        BlockedOutcome.TryParse(result.Error, out var outcome).ShouldBeTrue();
        outcome!.Reason.ShouldBe(BlockReason.UnknownProduct);
    }

    /// <summary>
    /// The retired TC-BSSB alias-hack string ("Número de tarjeta 4111...") must NOT fuzzy-match
    /// the new COSTCO BANAMEX product — the two strings share almost no characters in common
    /// (regression guard: proves the neuter + new catalog row don't accidentally cross-resolve).
    /// </summary>
    [Fact]
    public void Resolve_RetiredCardNumberAliasToken_DoesNotFuzzyMatchNewProduct()
    {
        var resolver = new ProductResolver();
        var bundle = BundleWithCostcoBanamex();

        var result = resolver.Resolve("Número de tarjeta 4111000000070001", bundle);

        result.IsSuccess.ShouldBeFalse();
        BlockedOutcome.TryParse(result.Error, out var outcome).ShouldBeTrue();
        outcome!.Reason.ShouldBe(BlockReason.UnknownProduct);
    }

    [Fact]
    public void FuzzyScoreThreshold_IsEightyFive()
    {
        // Documents the calibrated constant so a future change to it is a deliberate, reviewed
        // edit rather than a silent drift.
        ProductResolver.FuzzyScoreThreshold.ShouldBe(85);
    }

    // -----------------------------------------------------------------------
    // Ambiguity guard — abstain rather than nearest-guess between two plausible products
    // (adversarial-review finding #3 / owner ruling 2)
    // -----------------------------------------------------------------------

    private static VecReferenceBundle BundleWithOroAndOros() => new(
        BundleMetadata: new BundleMetadata("1.0.0", "Demo Bank", null, null, null, null),
        Products:
        [
            new VecProduct(
                ProductId: "TC-ORO",
                ProductName: "Tarjeta Oro",
                Aliases: null,
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: null),
            new VecProduct(
                ProductId: "TC-OROS",
                ProductName: "Tarjeta Oros",
                Aliases: null,
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: null),
        ],
        InterestRates: null,
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        ClientAccounts: null,
        PriorStatements: null,
        ExpectedTransactions: null,
        ToleranceConfig: null,
        ValidationConstants: null);

    /// <summary>
    /// Two distinct catalog products ("Tarjeta Oro" / "Tarjeta Oros") both clear
    /// <see cref="ProductResolver.FuzzyScoreThreshold"/> for a single OCR-noise token and land
    /// within <see cref="ProductResolver.FuzzyAmbiguityMargin"/> of each other — measured via
    /// <c>Fuzz.Ratio</c>: token "TARJETA OROO" scores 96 against "TARJETA ORO" and 92 against
    /// "TARJETA OROS" (gap 4 &lt;= margin 5, both &gt;= 85). The literal token "TARJETA OROS" is
    /// deliberately NOT used here — it would exact-match the "Tarjeta Oros" alias in Pass 1 before
    /// fuzzy matching ever runs. The resolver must abstain (BLOCKED UnknownProduct) rather than
    /// nearest-guess between the two plausible products (owner ruling 2 honesty boundary).
    /// </summary>
    [Fact]
    public void Resolve_TwoDistinctProductsTieAboveThreshold_AbstainsAsAmbiguous()
    {
        var resolver = new ProductResolver();
        var bundle = BundleWithOroAndOros();

        var result = resolver.Resolve("Tarjeta Oroo", bundle);

        result.IsSuccess.ShouldBeFalse(
            $"Expected an ambiguity abstention, but resolver matched: {result.Value?.ProductId}");
        BlockedOutcome.TryParse(result.Error, out var outcome).ShouldBeTrue();
        outcome!.Reason.ShouldBe(BlockReason.UnknownProduct);
    }

    /// <summary>
    /// Companion to <see cref="Resolve_TwoDistinctProductsTieAboveThreshold_AbstainsAsAmbiguous"/>:
    /// proves the ambiguity guard doesn't over-abstain. Token "TARJETA ORQ" scores 91 against
    /// "TARJETA ORO" and only 38 against an unrelated second product ("CUENTA DEBITO PLATINO") —
    /// the best clears the threshold and the gap (53) is far past
    /// <see cref="ProductResolver.FuzzyAmbiguityMargin"/>, so the resolver must still resolve.
    /// </summary>
    [Fact]
    public void Resolve_OneProductClearlyDominates_Resolves()
    {
        var resolver = new ProductResolver();
        var bundle = new VecReferenceBundle(
            BundleMetadata: new BundleMetadata("1.0.0", "Demo Bank", null, null, null, null),
            Products:
            [
                new VecProduct(
                    ProductId: "TC-ORO",
                    ProductName: "Tarjeta Oro",
                    Aliases: null,
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: null),
                new VecProduct(
                    ProductId: "TC-PLATINO",
                    ProductName: "Cuenta Debito Platino",
                    Aliases: null,
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: null),
            ],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

        var result = resolver.Resolve("Tarjeta Orq", bundle);

        result.IsSuccess.ShouldBeTrue($"Expected the clear winner to resolve. Error: {result.Error}");
        result.Value!.ProductId.ShouldBe("TC-ORO");
    }
}
