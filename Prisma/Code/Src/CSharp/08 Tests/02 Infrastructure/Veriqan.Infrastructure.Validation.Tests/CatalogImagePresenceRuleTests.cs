using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for <c>CatalogImagePresenceRule</c> (VERIQAN-E2-S4 / CL-27/CL-30/CL-47 / FR-12).
/// </summary>
/// <remarks>
/// <para>
/// Tests synthesize in-memory <see cref="SixLabors.ImageSharp.Image"/> objects, compute their
/// perceptual hashes using <see cref="PerceptualHash"/> from CoenM.ImageSharp.ImageHash, and
/// stuff the resulting <see cref="ulong"/> values into
/// <see cref="StatementModel.PagePerceptualHashes"/> — no real PDF rendering is required.
/// </para>
/// <para>
/// This approach directly unit-tests the comparison logic that the rule executes.
/// </para>
/// </remarks>
public sealed class CatalogImagePresenceRuleTests
{
    private const string ProductId = "TC-IMG-TEST";

    // -----------------------------------------------------------------------
    // Image + hash synthesis helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a deterministic 64×64 test image with a black rectangle on a white background.
    /// The black rectangle size is controlled by <paramref name="rectSize"/> so that two
    /// images with different values produce perceptually distinct hashes.
    /// </summary>
    private static Image<Rgba32> BuildTestImage(int width, int height, int rectSize)
    {
        var img = new Image<Rgba32>(width, height, new Rgba32(255, 255, 255, 255));
        for (var y = 0; y < rectSize && y < height; y++)
            for (var x = 0; x < rectSize && x < width; x++)
                img[x, y] = new Rgba32(0, 0, 0, 255);
        return img;
    }

    /// <summary>
    /// Computes a CoenM <see cref="PerceptualHash"/> for the supplied image.
    /// </summary>
    private static ulong ComputeHash(Image<Rgba32> image)
    {
        var algo = new PerceptualHash();
        return algo.Hash(image);
    }

    /// <summary>
    /// Formats a <see cref="ulong"/> hash as a 16-character upper-case hexadecimal string
    /// (the canonical <c>ImageRef.PerceptualHash</c> wire format used by this rule).
    /// </summary>
    private static string HashToHex(ulong hash) => hash.ToString("X16");

    // -----------------------------------------------------------------------
    // Bundle + context factories
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product(ImageRef? cardImage = null) =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: cardImage, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle BundleWithCardImage(ImageRef? cardImage) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product(cardImage)],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VecReferenceBundle BundleWithNoImageRefs() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product(cardImage: null)],
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
    /// Builds a minimal <see cref="StatementModel"/> with a supplied page-hash list.
    /// All header fields are set to <see cref="ExtractionStatus.NotExtracted"/>.
    /// </summary>
    private static StatementModel ModelWithPageHashes(IReadOnlyList<ulong> pageHashes)
    {
        var locator = FieldLocator.PageHint(1);
        var missingStr = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            PagePerceptualHashes = pageHashes,
        };
    }

    private static VerificationContext Ctx(VecReferenceBundle bundle, StatementModel? model) =>
        new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    /// <summary>
    /// Resolves <c>CL-27/CL-30/CL-47</c> from the DI-registered rules (Scrutor scan path).
    /// </summary>
    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == "CL-27/CL-30/CL-47")
                return rule;

        throw new InvalidOperationException("CatalogImagePresenceRule (CL-27/CL-30/CL-47) not found in DI.");
    }

    // -----------------------------------------------------------------------
    // Test 1 — matching image → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the page-perceptual-hash list contains a hash that is within the Hamming
    /// threshold of the bundle's <c>cardImage.perceptualHash</c>, the rule must pass.
    /// </summary>
    [Fact]
    public void Evaluate_MatchingPageHash_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;

        // Synthesize a test image, compute its hash, store it as the page hash
        // AND as the bundle reference hash — identical images → Hamming distance = 0.
        using var img = BuildTestImage(64, 64, rectSize: 32);
        var hash = ComputeHash(img);

        var cardImageRef = new ImageRef(
            ImageId: "card-img-1",
            Uri: null,
            Sha256: null,
            PerceptualHash: HashToHex(hash),
            Width: 64,
            Height: 64,
            Description: "Test card image");

        var bundle = BundleWithCardImage(cardImageRef);
        var model = ModelWithPageHashes([hash]);                // page hash == reference hash
        var ctx = Ctx(bundle, model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule failed unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe("CL-27/CL-30/CL-47");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "A page whose hash matches the bundle reference within Hamming threshold must yield Pass.");
    }

    // -----------------------------------------------------------------------
    // Test 2 — no match → Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// When no page hash is within the Hamming threshold of the bundle's catalog image hash,
    /// the rule must fail and name the missing image.
    /// </summary>
    [Fact]
    public void Evaluate_NoMatchingPageHash_ReturnsFail()
    {
        var ct = TestContext.Current.CancellationToken;

        // Two distinct images whose hashes differ by far more than DefaultHammingThreshold bits.
        using var referenceImg = BuildTestImage(64, 64, rectSize: 32); // heavy black fill
        using var pageImg = BuildTestImage(64, 64, rectSize: 0);       // blank white

        var referenceHash = ComputeHash(referenceImg);
        var pageHash = ComputeHash(pageImg);

        // Confirm the images actually produce meaningfully different hashes.
        var distance = System.Numerics.BitOperations.PopCount(referenceHash ^ pageHash);
        distance.ShouldBeGreaterThan(
            ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules.CatalogImagePresenceRule.DefaultHammingThreshold,
            "Test images must differ by more than the threshold to validate the Fail path.");

        var cardImageRef = new ImageRef(
            ImageId: "card-img-missing",
            Uri: null,
            Sha256: null,
            PerceptualHash: HashToHex(referenceHash),
            Width: 64,
            Height: 64,
            Description: "Reference image not present in statement");

        var bundle = BundleWithCardImage(cardImageRef);
        var model = ModelWithPageHashes([pageHash]);     // page hash ≠ reference hash
        var ctx = Ctx(bundle, model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe("CL-27/CL-30/CL-47");
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail,
            "When no page hash matches the catalog image, the rule must return Fail.");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical,
            "An absent catalog image is a Critical finding.");
    }

    // -----------------------------------------------------------------------
    // Test 3 — empty bundle ImageRef section → InsufficientData
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the bundle contains no <see cref="ImageRef"/> entries with a
    /// <c>perceptualHash</c>, the rule must abstain with
    /// <see cref="FindingVerdict.InsufficientData"/>.
    /// This is the abstain-safety discipline: never RED on missing reference data.
    /// </summary>
    [Fact]
    public void Evaluate_BundleHasNoImageRefs_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;

        // Bundle has products but no cardImage / importantMessageImage hashes.
        var bundle = BundleWithNoImageRefs();

        // Even with populated page hashes, the rule must abstain.
        using var img = BuildTestImage(64, 64, rectSize: 16);
        var hash = ComputeHash(img);
        var model = ModelWithPageHashes([hash]);

        var ctx = Ctx(bundle, model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe("CL-27/CL-30/CL-47");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A bundle with no ImageRef perceptualHash entries must produce InsufficientData — " +
            "the rule must never red-flag on missing reference data.");
    }
}
