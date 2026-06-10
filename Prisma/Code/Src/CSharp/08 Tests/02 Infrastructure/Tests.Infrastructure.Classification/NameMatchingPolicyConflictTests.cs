namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

using ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Behavior tests for <see cref="NameMatchingPolicy"/> AFTER the self-pairing fix
/// (docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md).
/// These assert the INTENDED behavior — distinct names disagree (conflict), the consensus value wins over an
/// outlier, and alias/accent equivalence agrees — which the diagonal defect made impossible. Written ITDD:
/// the conflict/medoid cases are red against the pre-fix code.
/// </summary>
public sealed class NameMatchingPolicyConflictTests
{
    private static NameMatchingPolicy Policy(NameMatchingOptions? options = null)
        => new(new StaticOptionsMonitor<NameMatchingOptions>(options ?? new NameMatchingOptions()),
               Substitute.For<ILogger<NameMatchingPolicy>>());

    private static FieldValue FV(string? value, string sourceType = "DOCX",
        FieldOrigin origin = FieldOrigin.Docx, string? raw = null)
        => new("NombreSolicitante", value, 1.0f, sourceType, origin, raw);

    // ---- SelectBestValueAsync — conflict + winner selection ----

    [Fact]
    public async Task SelectBestValueAsync_ClearlyDifferentNames_ReportsConflictAndLowAgreement()
    {
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("María González") };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasConflict.ShouldBeTrue();                       // distinct names => conflict
        result.Value.AgreementLevel.ShouldBeLessThan(0.80f);           // below ConflictThreshold
    }

    [Fact]
    public async Task SelectBestValueAsync_OutlierAmongConsensus_PicksConsensusAndFlagsConflict()
    {
        // The outlier is FIRST; the consensus (two agreeing sources) must still win, proving the winner is the
        // medoid (most agreement with others), not just values[0].
        var values = new List<FieldValue>
        {
            FV("María González", "XML", FieldOrigin.Xml),
            FV("Juan Pérez", "PDF", FieldOrigin.PdfOcr),
            FV("Juan Pérez", "DOCX", FieldOrigin.Docx),
        };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedValue.ShouldBe("Juan Pérez");             // consensus wins over the first/outlier
        result.Value.HasConflict.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectBestValueAsync_DistantNickname_NoAlias_ReportsConflict()
    {
        // No alias entry for Guillermo/Memo -> fuzzy score is low -> conflict (proves the fuzzy path is live).
        var values = new List<FieldValue> { FV("Guillermo"), FV("Memo") };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.Value!.HasConflict.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectBestValueAsync_AliasNames_AgreeNoConflict()
    {
        // Default aliases include CRISTIAN|CHRISTIAN -> ScorePair returns 1.0 via the alias map.
        var values = new List<FieldValue> { FV("Cristian"), FV("Christian") };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.Value!.HasConflict.ShouldBeFalse();
        result.Value.AgreementLevel.ShouldBe(1.0f);
    }

    [Fact]
    public async Task SelectBestValueAsync_AccentOnlyDifference_AgreeNoConflict()
    {
        // Accent normalization makes "José"/"Jose" identical -> 1.0, no conflict.
        var values = new List<FieldValue> { FV("José"), FV("Jose") };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.Value!.HasConflict.ShouldBeFalse();
        result.Value.AgreementLevel.ShouldBe(1.0f);
    }

    [Fact]
    public async Task SelectBestValueAsync_SingleValue_TriviallyAgrees()
    {
        var result = await Policy().SelectBestValueAsync("NombreSolicitante", new List<FieldValue> { FV("Juan Pérez") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasConflict.ShouldBeFalse();
        result.Value.AgreementLevel.ShouldBe(1.0f);
        result.Value.MatchedValue.ShouldBe("Juan Pérez");
    }

    // ---- CalculateAgreementLevelAsync — weakest-pair semantics ----

    [Fact]
    public async Task CalculateAgreementLevelAsync_DifferentNames_ReturnsLowScore()
    {
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("María González") };

        var result = await Policy().CalculateAgreementLevelAsync(values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeLessThan(0.80f);
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_OneOutlierAmongAgreeing_ReturnsLowScore()
    {
        // Overall agreement is the WEAKEST pair: two identical + one different -> low (not 1.0).
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("Juan Pérez"), FV("María González") };

        var result = await Policy().CalculateAgreementLevelAsync(values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeLessThan(0.80f);
    }

    // ---- HasConflictAsync — real disagreement ----

    [Fact]
    public async Task HasConflictAsync_DifferentNames_ReportsConflict()
    {
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("María González") };

        var result = await Policy().HasConflictAsync(values, threshold: 0.8f);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }
}
