namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Deterministic unit tests for the PURE metric-computation engine
/// (<see cref="LlmExtractionMetrics"/>) used by <see cref="LlmExtractionEvalHarness"/> (S4-A).
/// </summary>
/// <remarks>
/// No live Ollama, no Tesseract, no HTTP, no file I/O — every input here is hand-built. This is the
/// ground-truth verification anchor for the eval harness's metric math (S4-A story 2 / spec D2),
/// since the harness's own live numbers are not reproducible or CI-gated.
/// </remarks>
public sealed class LlmExtractionMetricsTests
{
    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — exact match
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ExactMatch_ReturnsMatched()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "222AAA-44444444442025", EvalTrack.Deterministic, EvalField.AutoridadNombre,
            gold: "CNBV", candidate: "CNBV");

        result.Status.ShouldBe(EvalMatchStatus.Matched);
        result.GoldNormalized.ShouldBe(result.CandidateNormalized);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — whitespace/case variants (AutoridadNombre normalization)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_AuthorityWhitespaceAndCaseVariant_ReturnsMatched()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "222AAA-44444444442025", EvalTrack.LlmText, EvalField.AutoridadNombre,
            gold: "  Comisión Nacional Bancaria y de Valores  ",
            candidate: "comisión nacional   bancaria y de valores");

        result.Status.ShouldBe(EvalMatchStatus.Matched);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — diacritic-folded authority match (M2 normalization)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeAuthority_DiacriticVariant_ReturnsMatched()
    {
        // Realistic Spanish-OCR accent variance: "Comisión" vs "Comision" must not score as a
        // mismatch — NormalizeAuthority folds diacritics on both sides before comparing.
        var result = LlmExtractionMetrics.Evaluate(
            "222AAA-44444444442025", EvalTrack.LlmText, EvalField.AutoridadNombre,
            gold: "Comisión Nacional Bancaria y de Valores",
            candidate: "Comision Nacional Bancaria y de Valores");

        result.Status.ShouldBe(EvalMatchStatus.Matched);
        result.GoldNormalized.ShouldBe(result.CandidateNormalized);
        LlmExtractionMetrics.NormalizeAuthority("Comisión").ShouldBe(LlmExtractionMetrics.NormalizeAuthority("Comision"));
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — format-normalized expediente match (different separators)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ExpedienteDifferentSeparators_ReturnsMatched()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "333BBB-44444444442025", EvalTrack.LlmVision, EvalField.NumeroExpediente,
            gold: "A/AS1-2505-088637-PHM",
            candidate: "a as1 2505 088637 phm");

        result.Status.ShouldBe(EvalMatchStatus.Matched);
        // Groups are re-joined with a single canonical separator (not concatenated bare) so that a
        // genuine boundary shift between groups (see M3 guard test below) does not falsely match.
        result.GoldNormalized.ShouldBe("A-AS1-2505-088637-PHM");
        result.CandidateNormalized.ShouldBe("A-AS1-2505-088637-PHM");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — boundary-shifted expediente (M3 false-match guard)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_BoundaryShiftedExpediente_ReturnsMismatch()
    {
        // "22-2AAA-2025" and "222-AAA-2025" strip to the identical digit/letter soup
        // ("222AAA2025") if separators are simply deleted — a false match masking a real boundary
        // shift between groups. NormalizeCaseReference guards against this by re-joining the
        // original alphanumeric groups with a single canonical separator, which preserves the group
        // boundaries themselves (not just separator style), so these two compare as a mismatch.
        var result = LlmExtractionMetrics.Evaluate(
            "333BBB-44444444442025", EvalTrack.Deterministic, EvalField.NumeroExpediente,
            gold: "22-2AAA-2025",
            candidate: "222-AAA-2025");

        result.Status.ShouldBe(EvalMatchStatus.Mismatch);
        result.GoldNormalized.ShouldNotBe(result.CandidateNormalized);
    }

    [Fact]
    public void Evaluate_OficioDifferentSeparators_ReturnsMatched()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "333BBB-44444444442025", EvalTrack.LlmText, EvalField.NumeroOficio,
            gold: "222/AAA/-4444444444/2025",
            candidate: "222-AAA-4444444444-2025");

        result.Status.ShouldBe(EvalMatchStatus.Matched);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — genuine mismatch
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_GenuineMismatch_ReturnsMismatch()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "333ccc-6666666662025", EvalTrack.Deterministic, EvalField.NumeroOficio,
            gold: "222/AAA/-4444444444/2025",
            candidate: "999/ZZZ/-1111111111/2020");

        result.Status.ShouldBe(EvalMatchStatus.Mismatch);
        result.GoldNormalized.ShouldNotBe(result.CandidateNormalized);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — Missing (null candidate, track was attempted)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_NullCandidate_ReturnsMissing()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "222AAA-44444444442025", EvalTrack.LlmText, EvalField.AutoridadNombre,
            gold: "CNBV", candidate: null);

        result.Status.ShouldBe(EvalMatchStatus.Missing);
        result.CandidateNormalized.ShouldBeNull();
    }

    [Fact]
    public void Evaluate_EmptyCandidate_ReturnsMissing()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "222AAA-44444444442025", EvalTrack.LlmVision, EvalField.NumeroExpediente,
            gold: "A/AS1-2505-088637-PHM", candidate: "   ");

        result.Status.ShouldBe(EvalMatchStatus.Missing);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Evaluate — TrackSkipped
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_SkipReasonProvided_ReturnsTrackSkippedRegardlessOfValues()
    {
        var result = LlmExtractionMetrics.Evaluate(
            "333BBB-44444444442025", EvalTrack.LlmText, EvalField.NumeroExpediente,
            gold: "123-456", candidate: "123-456", // would otherwise match
            skipReason: "tesseract failure, no cached .ocr.txt");

        result.Status.ShouldBe(EvalMatchStatus.TrackSkipped);
        result.SkipReason.ShouldBe("tesseract failure, no cached .ocr.txt");
    }

    // ───────────────────────────────────────────────────────────────────────
    // EvaluateParteCount — headline field, plain int equality
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateParteCount_ExactMatch_ReturnsMatched()
    {
        var result = LlmExtractionMetrics.EvaluateParteCount(
            "222AAA-44444444442025", EvalTrack.LlmVision, gold: 2, candidate: 2);

        result.Status.ShouldBe(EvalMatchStatus.Matched);
    }

    [Fact]
    public void EvaluateParteCount_DifferentCounts_ReturnsMismatch()
    {
        var result = LlmExtractionMetrics.EvaluateParteCount(
            "222AAA-44444444442025", EvalTrack.LlmText, gold: 2, candidate: 3);

        result.Status.ShouldBe(EvalMatchStatus.Mismatch);
    }

    [Fact]
    public void EvaluateParteCount_NullCandidate_ReturnsMissing()
    {
        // The deterministic track never extracts SolicitudPartes today (architecturally out of
        // scope) — this is the honest "no SolicitudPartes regression" baseline, not a defect.
        var result = LlmExtractionMetrics.EvaluateParteCount(
            "222AAA-44444444442025", EvalTrack.Deterministic, gold: 2, candidate: null);

        result.Status.ShouldBe(EvalMatchStatus.Missing);
    }

    [Fact]
    public void EvaluateParteCount_SkipReasonProvided_ReturnsTrackSkipped()
    {
        var result = LlmExtractionMetrics.EvaluateParteCount(
            "333ccc-6666666662025", EvalTrack.LlmVision, gold: 1, candidate: null,
            skipReason: "Ollama unreachable");

        result.Status.ShouldBe(EvalMatchStatus.TrackSkipped);
        result.SkipReason.ShouldBe("Ollama unreachable");
    }

    // ───────────────────────────────────────────────────────────────────────
    // AggregateAccuracy
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void AggregateAccuracy_ComputesPerFieldPerTrackAccuracy_MatchesOverFixturesWithGold()
    {
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.LlmText, EvalField.NumeroExpediente, "A-1", "A-1"), // Matched
            LlmExtractionMetrics.Evaluate("fx2", EvalTrack.LlmText, EvalField.NumeroExpediente, "B-2", "B-9"), // Mismatch
            LlmExtractionMetrics.Evaluate("fx3", EvalTrack.LlmText, EvalField.NumeroExpediente, "C-3", null),  // Missing
        };

        var accuracy = LlmExtractionMetrics.AggregateAccuracy(results);

        var expedienteLlmText = accuracy.Single(a => a.Field == EvalField.NumeroExpediente && a.Track == EvalTrack.LlmText);
        expedienteLlmText.Evaluable.ShouldBe(3);
        expedienteLlmText.Matches.ShouldBe(1);
        expedienteLlmText.Accuracy.ShouldBe(1.0 / 3.0, 0.0001);
    }

    [Fact]
    public void AggregateAccuracy_NullGold_ExcludedFromDenominator()
    {
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.Deterministic, EvalField.AutoridadNombre, gold: null, candidate: "CNBV"),
            LlmExtractionMetrics.Evaluate("fx2", EvalTrack.Deterministic, EvalField.AutoridadNombre, gold: "CNBV", candidate: "CNBV"),
        };

        var accuracy = LlmExtractionMetrics.AggregateAccuracy(results);

        var authority = accuracy.Single(a => a.Field == EvalField.AutoridadNombre && a.Track == EvalTrack.Deterministic);
        authority.Evaluable.ShouldBe(1); // fx1 excluded (no gold to grade against)
        authority.Matches.ShouldBe(1);
        authority.Accuracy.ShouldBe(1.0);
    }

    [Fact]
    public void AggregateAccuracy_TrackSkippedWithRealGold_ExcludedFromDenominator()
    {
        // C3: a TrackSkipped row that DOES have real (non-empty) gold must NOT count in the
        // denominator — the track was never attempted, so it must not be counted as an
        // evaluable-but-wrong row (that would wrongly deflate accuracy). Distinct from the
        // null-gold-exclusion test above: here gold is real, only the Status differs.
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate(
                "fx1", EvalTrack.LlmText, EvalField.AutoridadNombre,
                gold: "CNBV", candidate: null, skipReason: "Ollama unreachable"), // TrackSkipped, real gold
            LlmExtractionMetrics.Evaluate(
                "fx2", EvalTrack.LlmText, EvalField.AutoridadNombre,
                gold: "CNBV", candidate: "CNBV"), // Matched
        };

        var accuracy = LlmExtractionMetrics.AggregateAccuracy(results);

        var authority = accuracy.Single(a => a.Field == EvalField.AutoridadNombre && a.Track == EvalTrack.LlmText);
        authority.Evaluable.ShouldBe(1); // fx1 excluded despite having real gold — it was skipped, not graded
        authority.Matches.ShouldBe(1);
        authority.Accuracy.ShouldBe(1.0);
    }

    [Fact]
    public void AggregateAccuracy_DistinguishesTracksAndFieldsIndependently()
    {
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.Deterministic, EvalField.NumeroExpediente, "A-1", "A-1"),
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.LlmText, EvalField.NumeroExpediente, "A-1", "A-9"),
            LlmExtractionMetrics.EvaluateParteCount("fx1", EvalTrack.LlmVision, gold: 2, candidate: 2),
        };

        var accuracy = LlmExtractionMetrics.AggregateAccuracy(results);

        accuracy.Single(a => a.Track == EvalTrack.Deterministic && a.Field == EvalField.NumeroExpediente).Accuracy.ShouldBe(1.0);
        accuracy.Single(a => a.Track == EvalTrack.LlmText && a.Field == EvalField.NumeroExpediente).Accuracy.ShouldBe(0.0);
        accuracy.Single(a => a.Track == EvalTrack.LlmVision && a.Field == EvalField.ParteCount).Accuracy.ShouldBe(1.0);
    }

    // ───────────────────────────────────────────────────────────────────────
    // AggregateCoverage — TrackSkipped flows into coverage as "not covered"
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void AggregateCoverage_TrackSkippedCountsAsNotCovered()
    {
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.LlmVision, EvalField.NumeroExpediente, "A-1", "A-1"), // Matched -> covered
            LlmExtractionMetrics.Evaluate(
                "fx2", EvalTrack.LlmVision, EvalField.NumeroExpediente, "B-2", null,
                skipReason: "tesseract failure, no cached .ocr.txt"), // TrackSkipped -> not covered
            LlmExtractionMetrics.Evaluate("fx3", EvalTrack.LlmVision, EvalField.NumeroExpediente, "C-3", null), // Missing -> not covered
        };

        var coverage = LlmExtractionMetrics.AggregateCoverage(results);

        var visionCoverage = coverage.Single(c => c.Track == EvalTrack.LlmVision);
        visionCoverage.TotalEvaluations.ShouldBe(3);
        visionCoverage.NonNullCandidates.ShouldBe(1);
        visionCoverage.Coverage.ShouldBe(1.0 / 3.0, 0.0001);
    }

    [Fact]
    public void AggregateCoverage_MismatchStillCountsAsCovered()
    {
        // Coverage measures whether the track PRODUCED a candidate, not whether it was correct.
        var results = new[]
        {
            LlmExtractionMetrics.Evaluate("fx1", EvalTrack.Deterministic, EvalField.NumeroOficio, "222", "999"), // Mismatch -> still covered
        };

        var coverage = LlmExtractionMetrics.AggregateCoverage(results);

        var detCoverage = coverage.Single(c => c.Track == EvalTrack.Deterministic);
        detCoverage.NonNullCandidates.ShouldBe(1);
        detCoverage.Coverage.ShouldBe(1.0);
    }
}
