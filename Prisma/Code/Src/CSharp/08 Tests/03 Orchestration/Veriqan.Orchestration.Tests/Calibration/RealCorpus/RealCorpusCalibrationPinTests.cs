using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.RealCorpus;

/// <summary>
/// RC1.S5 — regression pin: locks in the post-S4 calibrated verdict/check expectations
/// (measured and reported by <see cref="RealCorpusBaselineMeasurementTests"/> in
/// <c>docs/qa/calibration/real-corpus-baseline-2026-07.md</c>) as hard, per-statement
/// assertions over the real-corpus index. <see cref="RealCorpusBaselineMeasurementTests"/> only
/// MEASURES and never asserts on outcomes (RC1.S2 program rule); this class is the calibration
/// gate on top of that measurement — any future extractor/rule/reference-data change that moves
/// one of these pins must be a conscious, evidence-driven decision (the same discipline RC1.S4
/// applied to fix these checks), not a silent regression.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ground truth:</b> every pinned value below was read directly from the out-of-repo
/// <c>real-corpus-baseline.json</c> produced by <see cref="RealCorpusBaselineMeasurementTests"/>
/// immediately after RC1.S4's final commit (<c>a44eff25</c>, "S4.b/B3 — LAW-SEC heading-shape
/// gate"), i.e. the fully-calibrated state RC1.S1-S4 converged on. Do not hand-adjust a pin
/// without re-running the measurement test and reading the regenerated JSON/committed Markdown —
/// see <c>docs/planning-artifacts/epic-veriqan-real-corpus-calibration-2026-07-22.md</c> (RC1.S4
/// row) and <c>docs/qa/calibration/real-corpus-baseline-2026-07.md</c>.
/// </para>
/// <para>
/// <b>Documented, corpus-bounded residual risks (from the RC1.S4.b adversarial gate — "COMPLETE
/// WITH GAPS, safe to pin"):</b> a pin in this class failing in the future may be an instance of
/// one of these three KNOWN, accepted allowlist limits rather than a genuine regression. Triage
/// against this list BEFORE assuming a code change broke calibration:
/// <list type="number">
///   <item><description><b>CL-18 fee-prefix allowlist</b> — <c>MovementClassifier</c>'s bank-fee
///   prefix anchoring (RC1.S4.b/B4) covers only the 5 fee phrasings observed in this corpus. An
///   uncovered real bank-fee phrasing (cash-advance, replacement-card, FX-commission wording,
///   etc.) on a NEW statement will Critical-Fail CL-18, mirroring the original CL-18 bug this
///   story fixed (a merchant name containing "COMISION" was misclassified as a bank fee).</description></item>
///   <item><description><b>CL-50/51/52 fiscal-legend 2-variant allowlist</b> — the fast-path
///   (RC1.S4.b/B2) recognizes the retired synthetic legend plus the real SAT "…DE UN CFDI" legend
///   seen in this corpus. A third legend wording on a NEW statement will fall through to the
///   pre-S4.b Fail behavior (same risk shape as CL-18) — see the live C-2026-02 residual below,
///   whose fiscal block carries neither legend variant in its text layer.</description></item>
///   <item><description><b>CL-42 null-ChargeDate + prior-period blind spot</b> — when ChargeDate
///   is not extracted AND OperationDate falls before the statement's period start (a legitimate
///   prior-period mis-billing pattern), CL-42 currently passes silently rather than abstaining.
///   Not exercised anywhere in this corpus (no statement hits the combination), so no pin below
///   encodes it directly; see the optional synthetic pin in <c>Veriqan.Validation.Tests</c>
///   (<c>Cl42ChargeDateRule</c> calibration tests) for a reproduction of this exact blind spot.</description></item>
/// </list>
/// A future pin failure that traces to one of these three should be triaged as "known allowlist
/// gap hit by new data" (extend the allowlist / harden the rule) rather than reflexively reverted.
/// </para>
/// <para>
/// <b>Structure:</b> one <see cref="StatementPin"/> per corpus-index.json entry (16 total — see
/// <see cref="Pins"/>), pinning the overall <c>Signal</c>/<c>BankTierVerdict</c>/
/// <c>CondusefTierVerdict</c> plus, for the 12 findings-bearing statements (B/C credit-card
/// series + the 3 non-scanned defect specimens — the 4 checking-account "A" statements and the
/// "scanned" defect honestly abstain at ExtractionGap with zero findings, per RC1.S3 class (d)),
/// the EXACT per-check verdict (Pass/Fail/InsufficientData — never "≥") for the RC1-calibrated
/// check subset named in the epic's S5 story: CL-18, CL-21, CL-31, CL-42, CL-46, CL-48, CL-50,
/// CL-51, CL-52, CL-53, LAW-SEC-PRESENCE, LAW-SEC-ORDER-GAP, LAW-§16-OTRASLINEAS,
/// LAW-§26-NOTAS, LAW-§27-GLOSARIO, LAW-TYPO-MINSIZE. A check flipping from Fail to Pass is just
/// as much a sign of an uncalibrated change as the reverse.
/// </para>
/// <para>
/// <b>Discrepancy vs. the S5 story brief (recorded here for traceability):</b> the story brief
/// described LAW-TYPO-MINSIZE as "Fail×4 on B" (implying the C series passes it). The MEASURED
/// state pinned here is Fail×8 — LAW-TYPO-MINSIZE fails on ALL 8 real credit-card statements (B
/// AND C series alike), matching the per-check aggregate table in
/// <c>real-corpus-baseline-2026-07.md</c> (<c>Fail=8, Statements seen=8</c>). This is the
/// owner-ratified genuine finding from RC1.S3/S4.d (anonymizer-surviving footnote-marker spans);
/// nothing in RC1.S4 was product-scoped to B only. Pinned per measured reality, not the brief.
/// </para>
/// </remarks>
[Trait("Category", "RealCorpus")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class RealCorpusCalibrationPinTests
{
    /// <summary>Institution key for the only reference bundle available in this repository —
    /// identical to <see cref="RealCorpusBaselineMeasurementTests"/>, so the measured baseline
    /// this class pins is reproduced exactly, not approximated.</summary>
    private const string Institution = "Demo Bank (Iqubica)";

    /// <summary>Repo-relative path to the reference-bundle root (parent of the institution folder).</summary>
    private const string ReferenceBundleRelativePath = "Prisma/Fixtures/PRP2/demo/reference-bundle";

    // -----------------------------------------------------------------------
    // Pin table
    // -----------------------------------------------------------------------

    public sealed record StatementPin(
        string ExpectedSignal,
        string ExpectedBankTierVerdict,
        string ExpectedCondusefTierVerdict,
        IReadOnlyDictionary<string, string> ExpectedCheckVerdicts);

    /// <summary>
    /// Pinned per-check verdicts shared by every real credit-card statement (B and C series) and
    /// all 3 non-scanned defect specimens — the RC1.S4-calibrated steady state. C-2026-02
    /// overrides CL-46/CL-50/CL-51/CL-52 to Fail (see <see cref="CMinus0202FiscalResidualChecks"/>
    /// and <see cref="Pins"/>) — that one statement's fiscal block genuinely carries no SAT legend
    /// text in its text layer (RC1.S4.c documented residual, same root cause as its CL-46 fail).
    /// </summary>
    /// <remarks>
    /// RC1.S5 footer-aware gap fix (owner ruling 2026-07-23): CL-48 moved Fail→Pass here. A
    /// probe of the 8 real corpus statements found every flagged gap was the same false
    /// positive — trailing whitespace down to the page footer (logo + form-code line), which
    /// the extractor's <c>ComputeMaxVerticalGap</c> was counting as an intra-page violation
    /// because the footer itself counts as content. Excluding footer-only content before
    /// measuring gaps (see <c>PdfPigStatementFieldExtractor.FooterZoneHeightPoints</c>) flips
    /// CL-48 to Pass on all 8 real B/C statements plus the 3 defect fixtures below (they mirror
    /// the demo <c>good.pdf</c> layout, which carries the same footer). No other check, Signal,
    /// or tier verdict moved — re-measured against this same pipeline after the fix.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> StandardCreditCardChecks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CL-18"] = "Pass",
            ["CL-42"] = "Pass",
            ["CL-31"] = "InsufficientData",
            ["CL-53"] = "InsufficientData",
            ["CL-50"] = "Pass",
            ["CL-51"] = "Pass",
            ["CL-52"] = "Pass",
            ["CL-46"] = "Pass",
            ["LAW-SEC-PRESENCE"] = "Fail",
            ["LAW-SEC-ORDER-GAP"] = "InsufficientData",
            ["LAW-§16-OTRASLINEAS"] = "Pass",
            ["LAW-§26-NOTAS"] = "InsufficientData",
            ["LAW-§27-GLOSARIO"] = "InsufficientData",
            ["CL-48"] = "Pass",
            ["LAW-TYPO-MINSIZE"] = "Fail",
            ["CL-21"] = "InsufficientData",
        };

    /// <summary>C-2026-02's fiscal block (CL-46/50/51/52) is the one documented RC1.S4.c
    /// residual: zero occurrences of either fiscal-legend allowlist variant in its text layer.</summary>
    private static readonly IReadOnlyDictionary<string, string> CMinus0202FiscalResidualChecks =
        new Dictionary<string, string>(StandardCreditCardChecks, StringComparer.Ordinal)
        {
            ["CL-46"] = "Fail",
            ["CL-50"] = "Fail",
            ["CL-51"] = "Fail",
            ["CL-52"] = "Fail",
        };

    /// <summary>Used for statements that honestly abstain at ExtractionGap before any check
    /// runs — the checking-account "A" series and the "scanned" (image-only) defect specimen.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoFindings =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The full RC1.S5 pin table — exactly one row per corpus-index.json entry id (16 total).
    /// See the class remarks for provenance and the 3 documented residual-risk allowlists.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, StatementPin> Pins =
        new Dictionary<string, StatementPin>(StringComparer.Ordinal)
        {
            // Account A (checking/Priority) — abstain-safe ExtractionGap, zero findings.
            // RC1.S3 triage class (d): extractor targets the credit-card layout; honest, not a
            // defect (GH#19 — non-credit-card products may legitimately not resolve).
            ["A-2026-02"] = new StatementPin("ExtractionGap", "ExtractionGap", "ExtractionGap", NoFindings),
            ["A-2026-03"] = new StatementPin("ExtractionGap", "ExtractionGap", "ExtractionGap", NoFindings),
            ["A-2026-04"] = new StatementPin("ExtractionGap", "ExtractionGap", "ExtractionGap", NoFindings),
            ["A-2026-05"] = new StatementPin("ExtractionGap", "ExtractionGap", "ExtractionGap", NoFindings),

            // Account B (Visa credit card) — steady-state Red/Yellow/Red, standard check block.
            ["B-2026-03"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["B-2026-04"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["B-2026-05"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["B-2026-06"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),

            // Account C (Mastercard credit card) — same steady state except C-2026-02 (fiscal
            // block residual, see CMinus0202FiscalResidualChecks doc-comment above).
            ["C-2026-02"] = new StatementPin("Red", "Yellow", "Red", CMinus0202FiscalResidualChecks),
            ["C-2026-03"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["C-2026-04"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["C-2026-05"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),

            // Defect specimens — byte-identical to the synthetic demo E2E fixtures (RC1.S3
            // triage: defect-good == demo good.pdf == real statement B-2026-04). Same standard
            // check block; defect-bad-font-cl35 additionally fails CL-35 (outside the pinned
            // subset — CL-35 is not one of the RC1.S5 tracked checks).
            ["defect-good"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["defect-bad-math-cl21"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["defect-bad-font-cl35"] = new StatementPin("Red", "Yellow", "Red", StandardCreditCardChecks),
            ["defect-scanned"] = new StatementPin("ExtractionGap", "ExtractionGap", "ExtractionGap", NoFindings),
        };

    // -----------------------------------------------------------------------
    // Theory data — loud loader (RC1.S5: a zero-row Theory here must be impossible)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Never yields zero rows silently. Absence of the corpus is expressed as one explicit
    /// skip-sentinel row (mirroring <see cref="RealCorpusPiiLeakGateTests"/> — a zero-row
    /// <c>[Theory]</c> silently "passing" is a known trap in this repo). A PRESENT-but-empty
    /// index, or a corpus entry with no pin row, is a real setup/coverage bug — both throw rather
    /// than silently skip or shrink the row count.
    /// </summary>
    public static IEnumerable<object?[]> PinnedEntries()
    {
        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null)
        {
            yield return new object?[] { null, null };
            yield break;
        }

        if (fixture.Entries.Count == 0)
            throw new InvalidOperationException(
                $"Real corpus root '{fixture.RootDir}' is present but corpus-index.json yielded " +
                "zero entries. That is a broken local setup (partial re-index?), not corpus " +
                "absence — fix it or re-run build_corpus_index.py rather than letting this Theory " +
                "silently run zero cases.");

        foreach (var entry in fixture.Entries)
        {
            if (!Pins.TryGetValue(entry.Id, out var pin))
                throw new InvalidOperationException(
                    $"Corpus entry '{entry.Id}' has no pinned expectation in " +
                    $"{nameof(RealCorpusCalibrationPinTests)}.{nameof(Pins)}. Every corpus-index.json " +
                    "entry must be pinned (RC1.S5) — add a pin row before this test class can run.");

            yield return new object?[] { entry, pin };
        }
    }

    [Theory]
    [MemberData(nameof(PinnedEntries))]
    public async Task RealCorpus_Specimen_MatchesCalibratedPin(RealCorpusEntry? entry, StatementPin? pin)
    {
        var ct = TestContext.Current.CancellationToken;

        if (entry is null || pin is null)
        {
            Assert.Skip(
                $"Real corpus not staged locally — set {RealCorpusFixtureLocator.RootEnvVar} to the " +
                "staging root containing corpus-index.json. Expected in CI (no corpus shipped).");
            return;
        }

        var fixture = RealCorpusFixtureLocator.TryLoad();
        fixture.ShouldNotBeNull(
            "Corpus resolved once already while building the Theory data; it must resolve " +
            "identically here (same environment, same process).");

        var repoRoot = ComputeRepoRoot();
        repoRoot.ShouldNotBeNull("Repository root could not be determined from assembly location.");

        var bundleRootDir = Path.Combine(repoRoot!, ReferenceBundleRelativePath);
        Directory.Exists(bundleRootDir).ShouldBeTrue(
            $"Reference bundle root not found at '{bundleRootDir}'.");

        var pdfPath = Path.Combine(fixture!.RootDir, entry.RelativePath);
        File.Exists(pdfPath).ShouldBeTrue($"Indexed file not found on disk: {entry.RelativePath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

        // Same context-key construction as RealCorpusBaselineMeasurementTests — the pin values
        // below were measured through this exact code path, so reproducing it exactly (rather
        // than approximating) is what makes this a faithful regression pin.
        var contextKey = new StatementContextKey(Institution, PeriodLabel: entry.Period ?? entry.DefectKind);

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        services.AddVeriqanReferenceData(opts => opts.RootDirectory = bundleRootDir);

        services.AddVeriqanInMemoryPersistence();
        services.AddSingleton<VeriqanMetrics>();

        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        await using var sp = services.BuildServiceProvider();

        var submission = new StatementSubmission(Pdf: pdfBytes, FileName: entry.Id, ContextKey: contextKey);

        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        result.IsSuccess.ShouldBeTrue(
            $"'{entry.Id}': pipeline returned a Result failure: {result.Error ?? "<none>"}.");

        var outcome = result.Value!;
        var summary = outcome.Summary;

        summary.Signal.ToString().ShouldBe(
            pin.ExpectedSignal,
            $"'{entry.Id}': Signal pin moved. Expected {pin.ExpectedSignal}, got {summary.Signal}. " +
            $"FailCheckIds=[{string.Join(", ", summary.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", summary.InsufficientDataCheckIds)}]. " +
            "See RealCorpusCalibrationPinTests class remarks for the 3 documented residual-risk " +
            "allowlists before assuming this is a genuine regression.");

        summary.BankTierVerdict.ToString().ShouldBe(
            pin.ExpectedBankTierVerdict,
            $"'{entry.Id}': BankTierVerdict pin moved. Expected {pin.ExpectedBankTierVerdict}, " +
            $"got {summary.BankTierVerdict}. BankFailCheckIds=[{string.Join(", ", summary.BankFailCheckIds)}].");

        summary.CondusefTierVerdict.ToString().ShouldBe(
            pin.ExpectedCondusefTierVerdict,
            $"'{entry.Id}': CondusefTierVerdict pin moved. Expected {pin.ExpectedCondusefTierVerdict}, " +
            $"got {summary.CondusefTierVerdict}. CondusefFailCheckIds=[{string.Join(", ", summary.CondusefFailCheckIds)}].");

        if (pin.ExpectedCheckVerdicts.Count == 0)
        {
            outcome.Findings.Count.ShouldBe(
                0,
                $"'{entry.Id}': expected zero findings (honest ExtractionGap abstain before any " +
                $"check runs) but got {outcome.Findings.Count}.");
            return;
        }

        var findingsByCheckId = outcome.Findings
            .GroupBy(f => f.CheckId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Verdict.ToString(), StringComparer.Ordinal);

        foreach (var (checkId, expectedVerdict) in pin.ExpectedCheckVerdicts)
        {
            var hasFinding = findingsByCheckId.TryGetValue(checkId, out var actualVerdict);
            hasFinding.ShouldBeTrue(
                $"'{entry.Id}': pinned check '{checkId}' produced NO finding at all (expected " +
                $"{expectedVerdict}). Present CheckIds=[{string.Join(", ", findingsByCheckId.Keys.OrderBy(k => k, StringComparer.Ordinal))}].");

            actualVerdict.ShouldBe(
                expectedVerdict,
                $"'{entry.Id}': check '{checkId}' pin moved. Expected {expectedVerdict}, got " +
                $"{actualVerdict}. If this traces to one of the 3 documented residual-risk " +
                "allowlists (CL-18 fee-prefix, CL-50/51/52 fiscal-legend, CL-42 null-ChargeDate — " +
                "see class remarks), triage as a known limitation hit by new data, not a blind revert.");
        }
    }

    // -----------------------------------------------------------------------
    // Completeness — RC1.S5: adding a corpus statement without pinning it must fail loudly.
    // -----------------------------------------------------------------------

    [Fact]
    public void Pins_CoverExactlyTheCorpusIndexEntries()
    {
        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null)
        {
            Assert.Skip(
                $"Real corpus not staged locally — set {RealCorpusFixtureLocator.RootEnvVar} to the " +
                "staging root containing corpus-index.json. Expected in CI (no corpus shipped).");
            return;
        }

        var indexIds = fixture.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var pinIds = Pins.Keys.ToHashSet(StringComparer.Ordinal);

        var missing = indexIds.Except(pinIds).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var stale = pinIds.Except(indexIds).OrderBy(x => x, StringComparer.Ordinal).ToList();

        missing.ShouldBeEmpty(
            $"corpus-index.json has {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")} with " +
            $"no pin row in {nameof(RealCorpusCalibrationPinTests)}.{nameof(Pins)}: " +
            $"[{string.Join(", ", missing)}]. Add a pin before this statement can regress silently.");

        stale.ShouldBeEmpty(
            $"{nameof(Pins)} has {stale.Count} stale entr{(stale.Count == 1 ? "y" : "ies")} not " +
            $"present in the current corpus index: [{string.Join(", ", stale)}]. Remove or reconcile.");

        indexIds.Count.ShouldBe(16, $"expected exactly 16 corpus-index.json entries, found {indexIds.Count}.");
    }

    // -----------------------------------------------------------------------
    // Repo-root resolution — mirrors RealCorpusBaselineMeasurementTests.ComputeRepoRoot.
    // -----------------------------------------------------------------------

    private static string? ComputeRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return dir.FullName;

            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) && File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return siblingRepo;

            dir = dir.Parent;
        }

        return null;
    }
}
