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
/// <b>RC1.S6 update (2026-07-23 — §-anchor OCR escalation ladder):</b> on this real-corpus family
/// every CONDUSEF §-heading is raster/image-rendered — the text layer alone found 0 of 23
/// detectable anchors on all 8 real credit-card statements (RC1.S6 probe). A new escalation stage
/// (<c>SectionAnchorOcrEscalationStage</c>) renders + OCRs each page when the text layer finds
/// fewer than 2 present sections, re-scanning recognized text for the same anchor table. Measured
/// against the full 16-statement corpus (<c>real-corpus-baseline-2026-07.md</c>, regenerated
/// 2026-07-23 post-RC1.S6):
/// <list type="bullet">
///   <item><description><b>CL-32</b> (§11 "Compara tu tarjeta", reworked to consult
///   <c>StatementModel.Sections</c> before the raw-text fallback) flips <b>Fail→Pass on all 8</b>
///   real B/C credit-card statements — OCR recovers "COMPARA TU TARJETA" on page 1 of every one,
///   exactly as the RC1.S6 probe found.</description></item>
///   <item><description><b>LAW-§26-NOTAS</b> flips <b>InsufficientData→Pass on all 8</b> — §26's
///   heading is OCR-recovered, and (unchanged) the rule scans the real text-layer
///   <c>NormalizedFullText</c> for the 13 verbatim notas, which genuinely are present as normal
///   body text on this family.</description></item>
///   <item><description><b>LAW-§27-GLOSARIO stays InsufficientData on all 8</b> — §27's heading
///   is OCR-recovered (raster-rendered region), and the 15 verbatim glosario terms are genuinely
///   NOT found in the real text layer of any of the 8 statements.</description></item>
///   <item><description><b>LAW-§11-URLS</b> and <b>LAW-§17-LEGENDS</b> (previously untracked —
///   both gate on section presence the same way, then scan real full-text) are tracked here as
///   <b>InsufficientData on all 8</b> for the mirror reason: their §11/§17 headings are now
///   OCR-detected, but their mandated verbatim content is genuinely absent from the real text
///   layer.</description></item>
///   <item><description><b>2026-07-23 same-day correction (RC1-residuals adversarial-review
///   BLOCKER fix):</b> the RC1.S6 measurement above originally recorded LAW-§27-GLOSARIO,
///   LAW-§11-URLS, and LAW-§17-LEGENDS as <b>Fail</b> on all 8 statements. Adversarial review
///   found this was a FALSE Critical Fail: an OCR-detected section heading means the section
///   region is a rendered image, and a text-layer content scan proves nothing about that
///   region's actual content — it can only observe that the mandated content is not in the
///   *selectable* text. An S1 probe of the OCR'd page text found the CONDUSEF URLs / §17
///   legends / §27 glossary terms ARE legible on the rendered pages. The three rules
///   (<c>Section11ComparaUrlsRule</c>, <c>Section17LegendsRule</c>, <c>Section27GlosarioRule</c>)
///   were corrected to abstain (InsufficientData) rather than Fail whenever their host section's
///   presence <c>Source</c> is <c>SectionDetectionSource.Ocr</c> and the text-layer content scan
///   comes up empty; a positive text-layer match still Passes regardless of presence source.
///   Content-level OCR verification does not exist yet — abstaining is the honest outcome until
///   it does. Pinned values below reflect this corrected behavior
///   (InsufficientData, not Fail).</description></item>
///   <item><description><b>LAW-§24-QUEJAS</b> (also newly added to the tracked subset here — it
///   was previously measured but undisclosed) moved <b>InsufficientData→Pass on all 8</b> via
///   the same OCR section-detection path: its host heading is OCR-recovered, and — unlike
///   §11/§17/§27 — the invariant CONDUSEF quejas legend genuinely IS present in the real text
///   layer of every statement, the same "content genuinely present" shape as
///   LAW-§26-NOTAS. This rule was also given the same OCR-source guard for consistency (its
///   Fail branch has the identical shape as §11/§17/§27), but the guard is not exercised on this
///   corpus because the content is always found.</description></item>
///   <item><description><b>LAW-§23-ABONO-LINK</b> and <b>LAW-§23-STATUS</b> were inspected for
///   the same OCR-gated false-Fail risk and found NOT to need the guard. LAW-§23-STATUS's Fail
///   branch requires a non-empty <c>DetectedSection.SectionText</c>, which
///   <c>SectionAnchorOcrEscalationStage</c> always leaves empty for an OCR-upgraded section (no
///   word geometry exists to slice a span from) — so the rule's existing empty-SectionText guard
///   already abstains before it can ever reach Fail on an OCR-sourced §23. LAW-§23-ABONO-LINK
///   does not scan text-layer content at all — its Fail branch cross-references structured
///   <c>DisputeRows</c>/<c>Movements</c> extraction, entirely independent of heading
///   presence-source. Both move on the real corpus to a per-statement mix (5 Pass / 3
///   InsufficientData) driven by dispute-row/movement extraction availability, not by
///   OCR-detection — deliberately NOT folded into the uniform <c>StandardCreditCardChecks</c>
///   block below.</description></item>
///   <item><description><b>BankTierVerdict</b> flips <b>Yellow→Green</b> wherever CL-32 was the
///   only bank-tier fail: B-2026-03/04/05/06, C-2026-03/04/05, and the <c>defect-good</c> /
///   <c>defect-bad-math-cl21</c> specimens. C-2026-02 (independent fiscal-legend residual,
///   CL-46/50/51/52) and <c>defect-bad-font-cl35</c> (independent CL-35 font defect) keep a
///   non-empty bank-tier fail set from their OTHER residuals and stay Yellow.</description></item>
///   <item><description>Everything else in the tracked subset — LAW-SEC-PRESENCE (still Fail:
///   §3/§4/§14/§15/etc. remain genuinely reworded/absent even after OCR — the ladder recovers
///   18–20 of 27 anchors per document, not all of them), LAW-SEC-ORDER-GAP (still
///   InsufficientData: OCR-sourced sections carry a page-only <c>FieldLocator</c>, deliberately
///   never a fabricated bounding box, so <c>SectionOrderAndGapRule</c>'s
///   <c>Locator.Bottom.HasValue</c> geometry gate still excludes them), CL-18/21/31/42/46/48/50/
///   51/52/53/LAW-§16-OTRASLINEAS/LAW-TYPO-MINSIZE, and the overall Signal/CondusefTierVerdict
///   (still Red — LAW-SEC-PRESENCE and LAW-TYPO-MINSIZE alone keep the Condusef tier Red on
///   every real credit-card statement, regardless of CL-32 or the now-abstaining
///   §11/§17/§27) — are UNCHANGED by RC1.S6 or by the 2026-07-23 correction.</description></item>
/// </list>
/// See <c>docs/qa/calibration/real-corpus-baseline-2026-07.md</c> §"Per-check aggregate" for the
/// full measured Fail/Abstain/Pass counts this update is pinned from.
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
            // RC1.S6 (2026-07-23): §-anchor OCR escalation recovers the §26/§27 headings via
            // real Tesseract OCR on this raster-headed real-corpus family. §26's verbatim notas
            // ARE genuinely present in the real text layer (Pass). §27's verbatim glosario terms
            // are genuinely NOT found in the text layer — corrected same-day (2026-07-23,
            // adversarial-review BLOCKER fix) to InsufficientData: an OCR-detected section is a
            // rendered image, so a text-layer miss cannot prove content absence — see class
            // remarks above.
            ["LAW-§26-NOTAS"] = "Pass",
            ["LAW-§27-GLOSARIO"] = "InsufficientData",
            // RC1.S6 additions — previously untracked; both mirror LAW-§27-GLOSARIO's shape
            // exactly (host section's heading is OCR-detected, mandated verbatim content is
            // genuinely absent from the real text layer). Corrected same-day (2026-07-23) to
            // InsufficientData for the identical reason.
            ["LAW-§11-URLS"] = "InsufficientData",
            ["LAW-§17-LEGENDS"] = "InsufficientData",
            // Newly added to the tracked subset (previously measured but undisclosed): §24's
            // heading is OCR-detected and its invariant CONDUSEF quejas legend genuinely IS
            // present in the real text layer — the same "content genuinely present" shape as
            // LAW-§26-NOTAS.
            ["LAW-§24-QUEJAS"] = "Pass",
            // RC1.S6 addition: CL-32 (§11 "Compara tu tarjeta") flips Fail→Pass on this family —
            // OCR recovers the heading on page 1 of every real B/C credit-card statement.
            ["CL-32"] = "Pass",
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

            // Account B (Visa credit card) — RC1.S6: BankTierVerdict Yellow→Green (CL-32 was the
            // sole bank-tier fail; now Pass via OCR escalation). Standard check block (updated).
            ["B-2026-03"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["B-2026-04"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["B-2026-05"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["B-2026-06"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),

            // Account C (Mastercard credit card) — same RC1.S6 BankTierVerdict flip except
            // C-2026-02, whose independent fiscal-legend residual (CL-46/50/51/52 Fail, see
            // CMinus0202FiscalResidualChecks doc-comment above) keeps a non-empty bank-tier fail
            // set regardless of CL-32 — stays Yellow.
            ["C-2026-02"] = new StatementPin("Red", "Yellow", "Red", CMinus0202FiscalResidualChecks),
            ["C-2026-03"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["C-2026-04"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["C-2026-05"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),

            // Defect specimens — byte-identical to the synthetic demo E2E fixtures (RC1.S3
            // triage: defect-good == demo good.pdf == real statement B-2026-04). Same standard
            // check block (RC1.S6-updated); defect-bad-font-cl35 additionally fails CL-35 (outside
            // the pinned subset — CL-35 is not one of the RC1.S5 tracked checks), which alone keeps
            // its bank-tier fail set non-empty — stays Yellow, unlike its 2 siblings below.
            ["defect-good"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
            ["defect-bad-math-cl21"] = new StatementPin("Red", "Green", "Red", StandardCreditCardChecks),
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
