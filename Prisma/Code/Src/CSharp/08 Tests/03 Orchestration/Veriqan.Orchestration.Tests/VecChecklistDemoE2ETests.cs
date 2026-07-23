using System.IO;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
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

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Parameterized end-to-end proof tests for the VEC checklist demo corpus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anti-tautology design (CRITICAL):</b> the reference bundle used by this test is built
/// from <em>neighbour</em> months 2026-03 and 2026-05 — NOT from the SUT month (2026-04 =
/// <c>good.pdf</c>).  This ensures the verifier does not check its own expected values.
/// Bundle files live under <c>Prisma/Fixtures/PRP2/demo/reference-bundle/Demo_Bank_(Iqubica)/</c>.
/// </para>
/// <para>
/// <b>Good-PDF principle (Hard Honesty):</b> <c>good.pdf</c> is a production-quality reference
/// Visa/BSSB statement for Mar-Apr 2026 provided by the owner as the golden master dataset.  It
/// was NOT authored to meet CONDUSEF CL-rules and is NOT assumed to be perfectly compliant.
/// Actual verified verdict (RC1.S5 footer-aware-gap update, 2026-07-23, live run post owner
/// ruling): <b>RED</b> with 3 FailCheckIds — [CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE].
/// (CL-48 previously also failed here — a probe of 8 real corpus statements including this same
/// PDF family proved that reported gap was trailing whitespace down to the page footer, not a
/// real blank-page violation; excluding footer-only content before measuring CL-48's gap
/// [<c>PdfPigStatementFieldExtractor.FooterZoneHeightPoints</c>] flips it to Pass here. The
/// prior "4 FailCheckIds" figure, and the earlier-still "13 structural FailCheckIds" figure from
/// the original 2026-06-27 run, are both stale: RC1.S4.a/b recalibrated CL-31, CL-46,
/// CL-50/51/52/53, LAW-SEC-ORDER-GAP, LAW-§26-NOTAS, and LAW-§27-GLOSARIO against the real
/// Banamex layout family, and RC1.S5 additionally cleared CL-48 — all 10 now Pass or honestly
/// abstain on this PDF.)
/// The Red verdict is a TRUE-POSITIVE: genuine non-compliance with the required checklist sections
/// (LAW-SEC-PRESENCE), "COMPARA TU TARJETA" (CL-32), and sub-floor
/// typography (LAW-TYPO-MINSIZE, owner-ratified — see RC1.S3/S4.d).  No bundle values were
/// fabricated, no findings were suppressed, and no tolerances were relaxed.
/// See <see cref="DemoFixtures"/> remarks for the per-check classification.
/// </para>
/// <para>
/// <b>Extraction coverage (updated):</b> the extractor was calibrated for this PDF layout family
/// in story PRISMA-Ext and now extracts ≥21 of 30 tracked fields (floor = 10).  Header fields
/// (CardNumber, CLABE, RFC, ClientNumber, BranchNumber, ClientName, PeriodStart, PeriodCutDate,
/// DayCountPrinted, PagoMinimo), RESUMEN amounts (CargosRegularesNoMeses, CargosComprasAMeses,
/// MontoIntereses, MontoComisiones, IvaInteresesYComisiones), and NIVEL-DE-USO totals
/// (SaldoCargosRegulares, SaldoCargosAMeses, CreditoDisponible) are all now extracted.
/// AdeudoPeriodoAnterior and PagosYAbonos remain NotExtracted (no label/amount for either row
/// anywhere in the text layer — confirmed via <c>pdftotext</c>).
/// </para>
/// <para>
/// <b>RC1.S4.a update (2026-07-22, real-corpus triage):</b> the Epic-5 "NotExtracted ⇒ implied
/// zero" rule for AdeudoPeriodoAnterior/PagosYAbonos was retired in favor of a <em>grounded</em>
/// implied-zero (<c>Cl21PagoParaNoGenerarInteresesRule</c>) — implied-zero now requires the
/// operand's RESUMEN label to be found somewhere in the text layer. Real-corpus evidence proved
/// the old unconditional rule produced false Fails on real Banamex statements where these rows
/// are non-zero but image-rendered (same NotExtracted signature as a genuinely zero-suppressed
/// row). Neither label is present anywhere in this PDF family's text layer (verified), so CL-21
/// now honestly ABSTAINS (InsufficientData) on this PDF family instead of computing an implied
/// zero — for BOTH good.pdf and bad-math-cl21.pdf. This is a deliberate, evidence-driven
/// consequence of widening honesty (never force a pass on ungrounded data): bad-math-cl21.pdf's
/// injected +$11.00 defect is still caught — CL-22 (<c>SaldoCargosRegulares ==
/// PagoParaNoGenerarIntereses</c>) does not depend on Adeudo/Pagos grounding and still fires
/// Fail/Critical, so the overall RED verdict is preserved. See
/// <c>docs/qa/calibration/real-corpus-triage-2026-07.md</c> and
/// <c>docs/planning-artifacts/epic-veriqan-real-corpus-calibration-2026-07-22.md</c> (RC1.S4.a).
/// </para>
/// <para>
/// <b>Demo corpus</b> — 4 anonymized PDFs under:<br/>
/// <c>Prisma/Fixtures/PRP2/demo/</c> (relative to the repository root)
/// </para>
/// <list type="table">
///   <listheader><term>File</term><description>Actual verdict / notes</description></listheader>
///   <item><term>good.pdf</term><description>RED / LAW-SEC-PRESENCE — structural failures (CL-18 and LAW-§26-NOTAS now Pass post-RC1.S4.a; CL-21 now InsufficientData — ungrounded Adeudo/Pagos, see RC1.S4.a remarks above). 21 fields extracted; floor cleared without bypass.</description></item>
///   <item><term>bad-math-cl21.pdf</term><description>RED / LAW-SEC-PRESENCE + CL-22 — CL-21 now InsufficientData (RC1.S4.a — ungrounded Adeudo/Pagos, same as good.pdf); CL-22 alone still catches the +$11.00 injection (delta=$11.00 &gt; tol=$0.50), preserving the RED verdict.</description></item>
///   <item><term>bad-font-cl35.pdf</term><description>RED / CL-35 — Courier font detected; Helvetica required by bundle (plus the same 3 checks as good.pdf: CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE; CL-48 now Pass post-RC1.S5).</description></item>
///   <item><term>scanned.pdf</term><description>BLOCKED — image-only PDF, text-layer floor not met.</description></item>
/// </list>
/// <para>
/// <b>Product token binding:</b> <c>PdfPigStatementFieldExtractor</c> finds the word "tarjeta"
/// from the label "Número de tarjeta" at Y≈607 (PdfPig coords) and returns the full band text
/// <c>"Número de tarjeta 4111000000070001"</c> as the product token.  The bundle's
/// <c>products.csv</c> carries this string as a pipe-separated alias for TC-BSSB so that
/// product resolution succeeds and the engine runs real rules.
/// </para>
/// <para>
/// <b>Fixture guard</b> — each theory case skips cleanly via <see cref="Assert.Skip"/> when
/// its fixture PDF is absent.  The scaffold therefore builds and runs green (all skipped)
/// before the corpus lands, and becomes live as soon as the owner drops the files in.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class VecChecklistDemoE2ETests
{
    // -----------------------------------------------------------------------
    // Corpus location
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sub-path from the repository root to the demo corpus directory.
    /// Fixtures are expected at <c>{RepoRoot}/{DemoCorpusRelativePath}/{filename}</c>.
    /// </summary>
    private const string DemoCorpusRelativePath = "Prisma/Fixtures/PRP2/demo";

    /// <summary>
    /// Absolute path to the demo corpus directory, resolved at runtime by walking up from the
    /// test assembly location until a directory containing <c>CLAUDE.md</c> is found (the
    /// repository root).  Returns <see langword="null"/> when the repo root cannot be located
    /// (e.g. running outside the normal build tree), in which case all theory cases skip.
    /// </summary>
    private static readonly string? DemoCorpusDir = ComputeDemoCorpusDir();

    private static string? ComputeDemoCorpusDir()
    {
        // Walk up from the assembly output directory looking for the repo root,
        // identified by the presence of CLAUDE.md.
        //
        // Two cases are handled at each level:
        //   1. Direct: CLAUDE.md in the current directory — assembly output is inside the
        //      repo tree (e.g. a Debug output dir placed within the repo).
        //   2. Sibling: a child directory named "ExxerCube.Prisma" contains CLAUDE.md —
        //      handles the standard layout where BuildArtifacts sits next to the repo root:
        //      IndFusion/
        //        BuildArtifacts/  ← assembly lives here
        //        ExxerCube.Prisma/CLAUDE.md  ← repo root sibling
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            // Case 1: assembly is inside the repo tree
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return Path.Combine(dir.FullName, DemoCorpusRelativePath);

            // Case 2: repo root is a sibling of the build-artifacts tree at this level
            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) &&
                File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return Path.Combine(siblingRepo, DemoCorpusRelativePath);

            dir = dir.Parent;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Theory data — 4 demo fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// MemberData source for <see cref="Pipeline_DemoFixture_ProducesExpectedVerdict"/>.
    /// </summary>
    /// <remarks>
    /// Columns: fixture file name, expected <see cref="VerdictSignal"/>,
    /// optional check ID that MUST appear in <see cref="VerdictSummary.FailCheckIds"/> when
    /// the signal is <see cref="VerdictSignal.Red"/> (<see langword="null"/> when no specific
    /// check ID is required, e.g. for BLOCKED cases).
    ///
    /// <b>RC1.S5 update (2026-07-23, reconciled against a live run post owner-ruled footer-gap
    /// fix):</b> the "13 structural FailCheckIds" figure below this line used to describe (from a
    /// 2026-06-27 run, before RC1.S4's real-corpus calibration fixes landed) is STALE — RC1.S4.a/b
    /// recalibrated CL-31, CL-50/51/52/53, and the LAW-SEC/LAW-§26/LAW-§27 detectors against the
    /// real Banamex layout family (see
    /// <c>docs/planning-artifacts/epic-veriqan-real-corpus-calibration-2026-07-22.md</c>, RC1.S4
    /// row), and RC1.S5 (2026-07-23, owner ruling) additionally cleared CL-48: a probe of 8 real
    /// corpus statements found every flagged CL-48 gap was trailing whitespace down to the page
    /// footer (not a genuine blank-page violation), so <c>ComputeMaxVerticalGap</c> now excludes
    /// footer-only content before measuring gaps. Most of the RC1.S4/S5 checks now Pass or
    /// honestly abstain (InsufficientData) instead of failing. <b>ACTUAL verdicts, verified live
    /// on this branch post-RC1.S5:</b>
    /// <list type="table">
    ///   <listheader><term>Fixture</term><description>FailCheckIds (BankFail / CondusefFail split)</description></listheader>
    ///   <item><term>good.pdf</term><description>[CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE] — Bank:[CL-32], Condusef:[CL-32,LAW-SEC-PRESENCE,LAW-TYPO-MINSIZE]</description></item>
    ///   <item><term>bad-math-cl21.pdf</term><description>[CL-22, CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE] — Bank:[CL-22,CL-32], Condusef:[CL-22,CL-32,LAW-SEC-PRESENCE,LAW-TYPO-MINSIZE]</description></item>
    ///   <item><term>bad-font-cl35.pdf</term><description>[CL-32, CL-35, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE] — Bank:[CL-32,CL-35], Condusef:[CL-32,LAW-SEC-PRESENCE,LAW-TYPO-MINSIZE]</description></item>
    ///   <item><term>compliant-master.pdf</term><description>[LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE] — Bank:[] (was [CL-48] pre-RC1.S5 — now empty, BankTierVerdict flips Yellow→Green), Condusef:[LAW-SEC-PRESENCE,LAW-TYPO-MINSIZE]</description></item>
    ///   <item><term>scanned.pdf</term><description>ExtractionGap — no rules ran, no findings.</description></item>
    /// </list>
    /// Only 3 checks remain genuinely failing on this PDF family now: CL-32 (ComparaTuTarjeta —
    /// (a) genuine, section absent from this layout), LAW-SEC-PRESENCE
    /// ((a)/(b) required sections absent/undetectable), and LAW-TYPO-MINSIZE ((e) owner-ratified
    /// genuine finding — anonymizer-surviving footnote-marker spans, see RC1.S3/S4.d). CL-31,
    /// CL-46, CL-48, CL-50, CL-51, CL-52, CL-53, LAW-SEC-ORDER-GAP, LAW-§26-NOTAS, and
    /// LAW-§27-GLOSARIO — all present in the old 13-check list — now Pass or InsufficientData on
    /// this PDF family and are NOT in the current FailCheckIds for any of these 4 fixtures (CL-48
    /// is the RC1.S5 addition to this now-clean list — see the class remarks above for the
    /// footer-gap rationale). See the RC1.S4 row of the epic doc for the fix-by-fix rationale, and
    /// <c>RealCorpusCalibrationPinTests</c> (Veriqan real-corpus suite) for the equivalent
    /// per-check pin table over the full 16-statement corpus.
    ///
    /// <b>Why bad-math-cl21.pdf is Red (CL-22 fires + structural failures; RC1.S4.a update):</b>
    /// The +$11.00 injection changes PagoParaNoGenerarIntereses: $12,604.55 → $12,615.55. Before
    /// RC1.S4.a (Epic 5's unconditional guarded-implied-zero), this single fat-finger entry broke
    /// TWO arithmetic identities (CL-21 AND CL-22). RC1.S4.a retired the unconditional implied-zero
    /// for AdeudoPeriodoAnterior/PagosYAbonos: implied-zero now requires the operand's RESUMEN
    /// label to be grounded (found somewhere in the text layer). Neither label appears anywhere in
    /// this PDF family's text layer, so CL-21 now honestly ABSTAINS (InsufficientData) — computing
    /// on an ungrounded implied zero would risk a false verdict on a genuinely non-zero,
    /// image-rendered row (the exact real-corpus failure mode RC1.S4.a fixes). CL-22 does NOT
    /// depend on Adeudo/Pagos and is unaffected:
    /// <list type="bullet">
    ///   <item>CL-21: now <b>InsufficientData</b> (RC1.S4.a — ungrounded Adeudo/Pagos; formerly computed a Fail via unconditional implied-zero).</item>
    ///   <item>CL-22: SaldoCargosRegulares(untouched)=$12,604.55 vs PagoParaNoGenerarIntereses(injected)=$12,615.55, delta=$11.00 &gt; $0.50 → <b>Fail/Critical</b> (unchanged).</item>
    /// </list>
    /// The Red signal is preserved via CL-22 (math error) plus the structural failures — the
    /// defect specimen still correctly signals RED end-to-end even though the specific CL-21
    /// finding changed. The assertions confirm CL-22 is in FailCheckIds and CL-21 is in
    /// InsufficientDataCheckIds (RC1.S4.a — see
    /// <c>docs/qa/calibration/real-corpus-triage-2026-07.md</c>).
    /// </remarks>
    /// <summary>
    /// MemberData source for <see cref="Pipeline_DemoFixture_ProducesExpectedVerdict"/>.
    /// </summary>
    /// <remarks>
    /// Columns (6):
    /// <list type="number">
    ///   <item>fixture file name</item>
    ///   <item>expected overall <see cref="VerdictSignal"/></item>
    ///   <item>optional check ID that MUST appear in <see cref="VerdictSummary.FailCheckIds"/> (null for BLOCKED)</item>
    ///   <item>expected <see cref="VerdictSummary.BankTierVerdict"/> (Story 1.3)</item>
    ///   <item>expected <see cref="VerdictSummary.CondusefTierVerdict"/> (Story 1.3)</item>
    /// </list>
    ///
    /// <b>Tier partition analysis (RC1.S5 update 2026-07-23 — footer-aware CL-48 gap fix,
    /// reconciled against a live run — see the FailCheckIds table on <see cref="DemoFixtures"/>
    /// above; the previous "13 structural failures" list here was stale after RC1.S4.a/b
    /// recalibration):</b>
    /// <list type="table">
    ///   <listheader><term>CheckId</term><description>CSV tier → partition(s)</description></listheader>
    ///   <item><term>CL-22</term><description>Both → condusef + bank (bad-math-cl21.pdf only)</description></item>
    ///   <item><term>CL-32</term><description>Both → condusef + bank</description></item>
    ///   <item><term>CL-35</term><description>Bank → bank only (bad-font-cl35.pdf only)</description></item>
    ///   <item><term>CL-48</term><description>Both → condusef + bank (still tier-classified Both by the CSV, but no longer FAILS on any of these 4 fixtures post-RC1.S5 — see class remarks)</description></item>
    ///   <item><term>LAW-SEC-PRESENCE</term><description>Condusef → condusef only</description></item>
    ///   <item><term>LAW-TYPO-MINSIZE</term><description>Condusef → condusef only</description></item>
    /// </list>
    /// CL-31, CL-46, CL-48, CL-50, CL-51, CL-52, CL-53, LAW-SEC-ORDER-GAP, LAW-§26-NOTAS, and
    /// LAW-§27-GLOSARIO no longer appear in any of these 4 fixtures' FailCheckIds post-RC1.S5 —
    /// they now Pass or honestly abstain (InsufficientData) on this PDF layout family.
    ///
    /// Result: condusefFailIds is non-empty → CondusefTierVerdict=Red; bankFailIds non-empty →
    /// BankTierVerdict=Yellow, EXCEPT compliant-master.pdf where CL-48 was the sole Bank-tier
    /// fail — with CL-48 now Pass, bankFailIds is empty there and BankTierVerdict=Green (see that
    /// fixture's row below).
    /// Combined overall = Red (Condusef=Red takes precedence via <see cref="VerdictSummary.CombineOverallSignal"/>).
    /// BLOCKED fixtures carry Blocked on both tier properties (no rules ran).
    /// </remarks>
    public static IEnumerable<object?[]> DemoFixtures =>
    [
        // good.pdf: RED — 3 failures post-RC1.S5 (CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE);
        // condusef-tier = Red, bank-tier = Yellow. See the DemoFixtures remarks above for the full
        // reconciled FailCheckIds table (CL-31/46/48/50/51/52/53/LAW-SEC-ORDER-GAP/LAW-§26/LAW-§27
        // no longer fail on this PDF family).
        // CondusefTierVerdict=Red because LAW-SEC-PRESENCE/LAW-TYPO-MINSIZE/CL-32 are Condusef/Both.
        // BankTierVerdict=Yellow because CL-32 is Both (no Bank-only fails on good.pdf; CL-48 no
        // longer contributes post-RC1.S5, but CL-32 alone keeps this fixture Yellow, not Green).
        ["good.pdf",          VerdictSignal.Red,     "LAW-SEC-PRESENCE", VerdictSignal.Yellow, VerdictSignal.Red],

        // bad-math-cl21.pdf: RED (structural failures + CL-22 Fail; delta=$11.00 > tol=$0.50).
        // RC1.S4.a (2026-07-22): CL-21 now InsufficientData (grounded-implied-zero gate — neither
        // AdeudoPeriodoAnterior nor PagosYAbonos label is found anywhere in this PDF family's text
        // layer, so the rule honestly abstains instead of computing on an ungrounded implied zero).
        // CL-22 (SaldoCargosRegulares == PagoParaNoGenerarIntereses) does not depend on Adeudo/Pagos
        // and alone still catches the +$11.00 injection — RED verdict preserved.
        // Tier partition: CL-22 and CL-32 are both "Both" tier → keep condusef + bank fail sets
        // non-empty even after CL-48 drops out post-RC1.S5.
        // CondusefTierVerdict remains Red; BankTierVerdict remains Yellow (non-empty bankFailIds).
        // Note: expectedFailCheckId column uses "CL-22" — CL-21's InsufficientData is asserted in-branch below.
        ["bad-math-cl21.pdf", VerdictSignal.Red,     "CL-22",            VerdictSignal.Yellow, VerdictSignal.Red],

        // bad-font-cl35.pdf: RED / CL-35 (Bank tier) + the same 3 checks as good.pdf
        // (CL-32, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE) — 4 total post-RC1.S5 (CL-48 now Pass).
        // CL-35 adds to bankFailIds but not condusefFailIds — CL-32 (Both) also keeps bankFailIds
        // non-empty independent of CL-35, so tier split is unaffected by the CL-48 fix.
        ["bad-font-cl35.pdf", VerdictSignal.Red,     "CL-35",            VerdictSignal.Yellow, VerdictSignal.Red],

        // scanned.pdf: ExtractionGap (Story 4.2) — text-layer floor fires before rules; tier verdicts = ExtractionGap.
        ["scanned.pdf",       VerdictSignal.ExtractionGap, null,         VerdictSignal.ExtractionGap, VerdictSignal.ExtractionGap],

        // compliant-master.pdf: RED / 2 failures post-RC1.S5 (LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE)
        // — honest verdict after Epic 3 corpus injection.
        // §11 / §17 / §26 / §27 pages added → CL-32, LAW-§26-NOTAS, LAW-§27-GLOSARIO PASS (as before).
        // RC1.S4 additionally cleared CL-31, CL-46, CL-50, CL-51, CL-52, CL-53, LAW-SEC-ORDER-GAP
        // on this fixture (same recalibration as good.pdf) — not re-verified by a dedicated
        // DiagnosticCompliantMaster run, but this fixture shares good.pdf's layout family and the
        // same RC1.S4 fixes apply.
        // RC1.S5 (2026-07-23, owner-ruled footer-gap fix): CL-48 was previously the ONLY Bank-tier
        // fail on this fixture (compliant-master.pdf's Epic-3 enhancement already cleared CL-32,
        // unlike good.pdf/bad-math-cl21.pdf/bad-font-cl35.pdf where CL-32 still fails). Once CL-48
        // itself flips Fail→Pass (footer-trailing whitespace, not a real blank-page violation —
        // this fixture shares good.pdf's layout/footer), bankFailIds becomes EMPTY and
        // BankTierVerdict genuinely flips Yellow→Green. This is a live-measured, deliberate
        // consequence of the fix — not a silent rewrite: the live pipeline run (RC1.S5) confirms
        // only LAW-SEC-PRESENCE/LAW-TYPO-MINSIZE remain in FailCheckIds; overall Signal stays Red
        // (CondusefTierVerdict is still Red via LAW-SEC-PRESENCE) via
        // <see cref="VerdictSummary.CombineOverallSignal"/>.
        // BankTierVerdict=Green (BankFail: none — was [CL-48] pre-RC1.S5).
        // CondusefTierVerdict=Red (CondusefFail: LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE — was also
        // [CL-48, ...] pre-RC1.S5).
        ["compliant-master.pdf", VerdictSignal.Red, "LAW-SEC-PRESENCE", VerdictSignal.Green, VerdictSignal.Red],
    ];

    // -----------------------------------------------------------------------
    // Shared context key (institution must match bundle-metadata.csv)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Context key used for all demo submissions.
    /// Institution "Demo Bank (Iqubica)" maps (via space→underscore normalization) to the
    /// bundle sub-directory <c>Demo_Bank_(Iqubica)/</c> under the reference-bundle root.
    /// Period label reflects the actual SUT statement period (2026-04).
    /// </summary>
    private static readonly StatementContextKey DemoContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    // -----------------------------------------------------------------------
    // Theory test
    // -----------------------------------------------------------------------

    /// <summary>
    /// Drives the full ingest → extract → bind → engine → verdict pipeline against each demo
    /// corpus fixture and asserts the actual VEC verdict signal and two-tier verdicts (Story 1.3).
    /// </summary>
    /// <param name="fixtureName">PDF file name inside the demo corpus directory.</param>
    /// <param name="expectedSignal">Expected overall <see cref="VerdictSignal"/> traffic-light.</param>
    /// <param name="expectedFailCheckId">
    /// When non-<see langword="null"/> the specified check ID must appear in
    /// <see cref="VerdictSummary.FailCheckIds"/> (proves the specific RED-triggering rule fired).
    /// </param>
    /// <param name="expectedBankTierVerdict">
    /// Expected <see cref="VerdictSummary.BankTierVerdict"/> produced by the two-tier aggregation.
    /// </param>
    /// <param name="expectedCondusefTierVerdict">
    /// Expected <see cref="VerdictSummary.CondusefTierVerdict"/> produced by the two-tier aggregation.
    /// </param>
    [Theory]
    [MemberData(nameof(DemoFixtures))]
    public async Task Pipeline_DemoFixture_ProducesExpectedVerdict(
        string fixtureName,
        VerdictSignal expectedSignal,
        string? expectedFailCheckId,
        VerdictSignal expectedBankTierVerdict,
        VerdictSignal expectedCondusefTierVerdict)
    {
        // ------------------------------------------------------------------
        // Fixture guard — skip if the demo corpus hasn't landed yet.
        // ------------------------------------------------------------------
        if (DemoCorpusDir is null)
            Assert.Skip("Repository root could not be determined from assembly location — demo corpus unreachable.");

        var fixturePath = Path.Combine(DemoCorpusDir, fixtureName);

        if (!File.Exists(fixturePath))
            Assert.Skip(
                $"Demo fixture '{fixtureName}' not present at '{fixturePath}'. " +
                "Drop the anonymized PDF in that directory to activate this test.");

        // ------------------------------------------------------------------
        // Reference-bundle root — sibling of the demo corpus directory.
        // The CsvReferenceDataAdapter resolves: bundleRoot / "Demo_Bank_(Iqubica)" / *.csv
        // ------------------------------------------------------------------
        var bundleRootDir = Path.Combine(DemoCorpusDir, "reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip(
                $"Reference bundle root not found at '{bundleRootDir}'. " +
                "Run the bundle-authoring step to create the CSV files.");

        // ------------------------------------------------------------------
        // Arrange — real CsvReferenceDataAdapter pointed at the anti-tautology bundle.
        // No NSubstitute fake: the verifier uses actual neighbour-month reference data.
        // ------------------------------------------------------------------
        var services = new ServiceCollection();
        services.AddLogging();

        // Application layer
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        // Infrastructure adapters (real implementations)
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        // Real reference-data provider (CSV adapter, anti-tautology bundle from neighbour months).
        // AddVeriqanReferenceData registers CsvReferenceDataAdapter as IVecReferenceDataProvider.
        // AddVeriqanBinding does NOT register IVecReferenceDataProvider, so no Replace needed.
        services.AddVeriqanReferenceData(opts => opts.RootDirectory = bundleRootDir);

        // No TenantProfile override: the extractor now extracts ≥21 fields from this PDF layout
        // (floor = 10), so the LegalBaseline TenantProfile registered by AddVeriqanVerdict() is
        // sufficient.  The bypass (minExtractionCoverageCount: 0) was removed in story PRISMA-Ext.

        // In-memory persistence stubs — no SQL Server required
        services.AddVeriqanInMemoryPersistence();

        // Metrics singleton required by VerificationPipeline
        services.AddSingleton<VeriqanMetrics>();

        // Pipeline
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        // E7.S7.2/S7.3: dispose the root container (not just the child scope below) — it owns the
        // singleton IHeaderProductOcrEngine (TesseractHeaderProductOcrEngine), and each theory
        // case's fresh ServiceCollection constructs its own native TesseractEngine. Leaving `sp`
        // undisposed would leak that native engine across every theory case in this process (the
        // engine's own doc-comment: native engines must be fully torn down, not just abandoned) —
        // disposing here keeps at most one engine alive at a time even though this test's DI
        // lifetime (fresh container per fixture) differs from the real Worker's single
        // process-lifetime container.
        await using var sp = services.BuildServiceProvider();

        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);

        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: fixtureName,
            ContextKey: DemoContextKey);

        // ------------------------------------------------------------------
        // Act
        // ------------------------------------------------------------------
        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        // ------------------------------------------------------------------
        // Assert — pipeline must return a successful Result<T> in all cases
        // (GREEN, RED, and BLOCKED are all returned as Result.WithSuccess).
        // ------------------------------------------------------------------
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for '{fixtureName}': {result.Error ?? "<none>"}. " +
            "Check that the CSV bundle files exist and that the header-image OCR product resolution " +
            "(E7.S7.2/S7.3) recovered the product and resolved it to TC-COSTCO-BANAMEX in products.csv.");

        var outcome = result.Value!;

        // Job must always be recorded (proves ingestion + in-memory persistence ran).
        outcome.Job.ShouldNotBeNull(
            $"'{fixtureName}': VerificationJob must be set on the outcome (ingestion persisted).");
        outcome.Job.Id.ShouldNotBe(
            Guid.Empty,
            $"'{fixtureName}': VerificationJob.Id must be a real GUID.");

        // Summary must always be present.
        outcome.Summary.ShouldNotBeNull(
            $"'{fixtureName}': VerdictSummary must be populated.");

        // Processing duration must be positive (proves the pipeline actually ran).
        outcome.ProcessingDuration.ShouldBeGreaterThan(
            TimeSpan.Zero,
            $"'{fixtureName}': ProcessingDuration must be positive — indicates the pipeline executed.");

        // Traffic-light signal must match the expected actual verdict.
        outcome.Summary.Signal.ShouldBe(
            expectedSignal,
            $"'{fixtureName}': expected {expectedSignal} but got {outcome.Summary.Signal}. " +
            $"FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", outcome.Summary.InsufficientDataCheckIds)}]. " +
            $"BlockedReason={outcome.Summary.BlockedOutcome?.Reason}. " +
            $"BlockedDetail={outcome.Summary.BlockedOutcome?.Detail}.");

        // ------------------------------------------------------------------
        // Two-tier verdict assertions (Story 1.3)
        // The pipeline fetches checklist-tiers.csv and passes it to the aggregator so
        // BankTierVerdict and CondusefTierVerdict are computed from the real tier map.
        // ------------------------------------------------------------------
        outcome.Summary.BankTierVerdict.ShouldBe(
            expectedBankTierVerdict,
            $"'{fixtureName}': BankTierVerdict expected {expectedBankTierVerdict} but got " +
            $"{outcome.Summary.BankTierVerdict}. " +
            $"BankFailCheckIds=[{string.Join(", ", outcome.Summary.BankFailCheckIds)}]. " +
            $"Overall FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");

        outcome.Summary.CondusefTierVerdict.ShouldBe(
            expectedCondusefTierVerdict,
            $"'{fixtureName}': CondusefTierVerdict expected {expectedCondusefTierVerdict} but got " +
            $"{outcome.Summary.CondusefTierVerdict}. " +
            $"CondusefFailCheckIds=[{string.Join(", ", outcome.Summary.CondusefFailCheckIds)}]. " +
            $"Overall FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");

        // ------------------------------------------------------------------
        // Signal-specific assertions
        // ------------------------------------------------------------------
        switch (expectedSignal)
        {
            case VerdictSignal.Green:
                // GREEN: no Fail findings — the extraction gaps produce InsufficientData, not Fail.
                outcome.Summary.FailCount.ShouldBe(
                    0,
                    $"'{fixtureName}': a GREEN verdict must have zero Fail findings. " +
                    $"Unexpected FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");
                break;

            case VerdictSignal.Red:
                // RED: at least one Fail finding must be present — engine ran real rules.
                outcome.Summary.FailCount.ShouldBeGreaterThan(
                    0,
                    $"'{fixtureName}': a RED verdict must have at least one Fail finding.");

                // When a specific check ID is required, assert it fired.
                if (expectedFailCheckId is not null)
                {
                    outcome.Summary.FailCheckIds.ShouldContain(
                        expectedFailCheckId,
                        $"'{fixtureName}': expected check '{expectedFailCheckId}' to be in FailCheckIds " +
                        $"but got [{string.Join(", ", outcome.Summary.FailCheckIds)}].");
                }

                // Findings list must also be non-empty.
                outcome.Findings.Count.ShouldBeGreaterThan(
                    0,
                    $"'{fixtureName}': RED outcome must carry at least one RuleFinding.");

                // For bad-math-cl21.pdf specifically (RC1.S4.a, 2026-07-22 — grounded implied-zero):
                // AdeudoPeriodoAnterior and PagosYAbonos are NotExtracted with NEITHER label found
                // anywhere in this PDF family's text layer (verified via pdftotext) — real-corpus
                // evidence proved the old unconditional Epic-5 implied-zero produces false Fails on
                // real Banamex statements where these rows are non-zero but image-rendered (same
                // NotExtracted signature as a genuinely zero-suppressed row). CL-21 therefore now
                // honestly ABSTAINS instead of computing on an ungrounded implied zero — this is a
                // deliberate widening of honesty, not a regression: the same "misread digit ≠ false
                // non-compliant" principle that made CL-21 fire on this fixture under Epic 5 now
                // makes it abstain, because the grounding evidence for the implied zero doesn't
                // exist on this PDF. The +$11.00 injection is still caught independently:
                //   CL-21: InsufficientData (ungrounded Adeudo/Pagos — RC1.S4.a).
                //   CL-22: SaldoCargosRegulares (NIVEL DE USO, untouched) = $12,604.55
                //          vs PagoParaNoGenerarIntereses (injected) = $12,615.55, delta = $11.00
                //          >> $0.50 → Fail/Critical (does not depend on Adeudo/Pagos, unaffected).
                if (fixtureName == "bad-math-cl21.pdf")
                {
                    outcome.Summary.InsufficientDataCheckIds.ShouldContain(
                        "CL-21",
                        "CL-21 must be InsufficientData for bad-math-cl21.pdf (RC1.S4.a) — neither " +
                        "AdeudoPeriodoAnterior nor PagosYAbonos label is found anywhere in this PDF " +
                        "family's text layer, so the grounded-implied-zero gate abstains rather than " +
                        "computing on a fabricated zero.");
                    outcome.Summary.FailCheckIds.ShouldNotContain(
                        "CL-21",
                        "CL-21 must NOT be Fail for bad-math-cl21.pdf post-RC1.S4.a — it abstains " +
                        "instead (see InsufficientDataCheckIds assertion above).");
                    outcome.Summary.FailCheckIds.ShouldContain(
                        "CL-22",
                        "CL-22 must be Fail for bad-math-cl21.pdf — it does not depend on " +
                        "Adeudo/Pagos grounding and alone still catches the +$11.00 injected error " +
                        "(SaldoCargosRegulares=$12,604.55 untouched vs injected $12,615.55, " +
                        "delta=$11.00 >> $0.50 tolerance), preserving the RED verdict.");
                }
                break;

            case VerdictSignal.ExtractionGap:
                // ExtractionGap (Story 4.2): the pipeline halted before rules ran (e.g. insufficient text layer).
                // BlockedOutcome carrier is reused for ExtractionGap — it carries the reason + detail.
                outcome.Summary.BlockedOutcome.ShouldNotBeNull(
                    $"'{fixtureName}': ExtractionGap outcome must carry a non-null BlockedOutcome " +
                    "identifying the reason (InsufficientTextLayer, UnknownProduct, etc.).");
                break;

            case VerdictSignal.Blocked:
                // Blocked (reserved — no emitter after Story 4.2): document defect requiring human review.
                outcome.Summary.BlockedOutcome.ShouldNotBeNull(
                    $"'{fixtureName}': Blocked verdict must carry a non-null BlockedOutcome.");
                break;
        }
    }
}
