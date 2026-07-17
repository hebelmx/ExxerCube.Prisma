using System.IO;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Story <b>C1.6</b> — the ship-dark arming-evidence gate for the DESGLOSE total-row
/// (<c>TotalCargos</c>/<c>TotalAbonos</c>) slice of the geometric-plausibility confidence
/// (VERIQAN C1 epic; addendum closing a coverage gap an adversarial review of C1.4 found — see
/// <c>docs/planning-artifacts/TRACKER-veriqan-c1-geometric-confidence.md</c>, "C1.4 CORRECTION"
/// note). Reuses the exact DI/harness pattern <see cref="GeometricConfidenceResumenArmingGateE2ETests"/>
/// built for C1.4, extended to the 2 <c>TryParseTotalRow</c>-wired fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mandatory negative control:</b> <c>decoy-total-amount.pdf</c> (C1.6, dummievec profile)
/// carries a decoy amount that <c>TryParseTotalRow</c>'s leftmost-match pick grabs instead of the
/// true <c>TotalAbonos</c> value. Unlike C1.4's RESUMEN negative control (CL-21), this one targets
/// <b>CL-44</b> (<c>Cl44DesgloseTotalsMatchRule</c>) — the rule that directly reads
/// <c>TotalCargos</c>/<c>TotalAbonos</c> and sums the DESGLOSE movement rows against them. Because
/// CL-44 abstains BEFORE its confidence guard whenever <c>MovementsStatus != Extracted</c>, both
/// C1.6 specimens carry 3 real movement rows (not just header + totals) so CL-44 actually reaches
/// the confidence guard — proven separable by
/// <see cref="Veriqan.Infrastructure.Extraction.Tests.GeometricPlausibilityTotalRowCalibrationTests"/>.
/// This test drives the specimen through the FULL pipeline and proves the mis-pick flips CL-44
/// from a confident (wrong-sum) <c>Fail</c> to an honest <c>InsufficientData</c> abstain once armed.
/// </para>
/// <para>
/// <b>5-demo diff is VACUOUS on real data, like C1.3's Tasa/Cat gate (not C1.4's non-vacuous
/// RESUMEN gate):</b> a throwaway diagnostic (removed) proved <c>TotalCargos</c>/<c>TotalAbonos</c>
/// are <c>NotExtracted</c> on all 5 real demo fixtures — <c>TryParseTotalRow</c> never matches a
/// "Total cargos"/"Total abonos" row on this bank's actual DESGLOSE layout (whereas C1.4 found 5
/// of 7 RESUMEN fields DO extract there). So this gate proves the flag changes nothing else on
/// real-bank data, but the false-abstain-on-a-clean-pick claim for the total-row slice is proven
/// ONLY on the synthetic corpus (<c>GeometricPlausibilityTotalRowCalibrationTests</c>) plus the
/// <c>decoy-total-amount</c> flip below — NOT on any live non-synthetic fixture. Same caveat C1.3
/// documented for Tasa/Cat.
/// </para>
/// <para>
/// <b>DI wiring</b> is copied verbatim from <see cref="GeometricConfidenceResumenArmingGateE2ETests"/> —
/// same TryAdd-override trick to pin <c>emitGeometricConfidence</c> per run.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class GeometricConfidenceTotalRowArmingGateE2ETests
{
    // -----------------------------------------------------------------------
    // Repo-root walk (copied verbatim from GeometricConfidenceArmingGateE2ETests)
    // -----------------------------------------------------------------------

    private const string DemoCorpusRelativePath = "Prisma/Fixtures/PRP2/demo";
    private const string SyntheticCorpusRelativePath = "Prisma/Fixtures/PRP2/synthetic";

    private static readonly string? RepoRoot = ComputeRepoRoot();

    private static string? ComputeRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return dir.FullName;

            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) &&
                File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return siblingRepo;

            dir = dir.Parent;
        }

        return null;
    }

    private static string? DemoCorpusDir =>
        RepoRoot is null ? null : Path.Combine(RepoRoot, DemoCorpusRelativePath);

    private static string? SyntheticCorpusDir =>
        RepoRoot is null ? null : Path.Combine(RepoRoot, SyntheticCorpusRelativePath);

    private static readonly StatementContextKey DemoContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    private sealed record VerdictSnapshot(
        VerdictSignal Signal,
        string[] FailCheckIds,
        string[] InsufficientDataCheckIds)
    {
        public string Dump(string label) =>
            $"{label}: Signal={Signal}. FailCheckIds=[{string.Join(", ", FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", InsufficientDataCheckIds)}].";
    }

    private static async Task<VerdictSnapshot> RunPipelineAsync(
        string corpusDir,
        string bundleRootDir,
        string fixtureName,
        bool emitGeometricConfidence,
        CancellationToken ct)
    {
        var fixturePath = Path.Combine(corpusDir, fixtureName);

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(sp => new PdfPigStatementFieldExtractor(
            sp.GetRequiredService<ILogger<PdfPigStatementFieldExtractor>>(),
            sp.GetRequiredService<IOptions<PdfExtractionOptions>>(),
            sp.GetRequiredService<IPasswordProvider>(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence));

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

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: fixtureName,
            ContextKey: DemoContextKey);

        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        result.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for '{fixtureName}' (emitGeometricConfidence={emitGeometricConfidence}): " +
            $"{result.Error ?? "<none>"}.");

        var summary = result.Value!.Summary;
        summary.ShouldNotBeNull($"'{fixtureName}': VerdictSummary must be populated.");

        return new VerdictSnapshot(
            summary.Signal,
            [.. summary.FailCheckIds.OrderBy(id => id, StringComparer.Ordinal)],
            [.. summary.InsufficientDataCheckIds.OrderBy(id => id, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// The 2 DESGLOSE total-row fields wired to the C1.6 scorer, by name (for dump messages) and a
    /// <see cref="PeriodSummary"/> accessor.
    /// </summary>
    private static readonly (string Name, Func<PeriodSummary, ExtractedField<decimal>> Accessor)[] TotalRowFields =
    [
        ("TotalCargos", ps => ps.TotalCargos),
        ("TotalAbonos", ps => ps.TotalAbonos),
    ];

    /// <summary>
    /// Runs the extractor directly to read the raw TotalCargos/TotalAbonos <c>Confidence</c>/
    /// <c>Status</c> values — mirrors <c>GeometricConfidenceResumenArmingGateE2ETests</c>'s
    /// analogous helper. Returns <c>Extracted=false</c> (empty dictionary) when the extractor
    /// itself fails.
    /// </summary>
    private static async Task<(bool Extracted, Dictionary<string, (double Confidence, ExtractionStatus Status)> Fields)>
        TryExtractTotalRowConfidenceAsync(
            string corpusDir,
            string fixtureName,
            bool emitGeometricConfidence,
            CancellationToken ct)
    {
        var pdfPath = Path.Combine(corpusDir, fixtureName);
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(sp => new PdfPigStatementFieldExtractor(
            sp.GetRequiredService<ILogger<PdfPigStatementFieldExtractor>>(),
            sp.GetRequiredService<IOptions<PdfExtractionOptions>>(),
            sp.GetRequiredService<IPasswordProvider>(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence));
        services.AddVeriqanExtraction();

        await using var sp = services.BuildServiceProvider();
        var extractor = sp.GetRequiredService<PdfPigStatementFieldExtractor>();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);
        if (!result.IsSuccess)
            return (false, []);

        var periodSummary = result.Value!.PeriodSummary;
        if (periodSummary is null)
            return (false, []);

        var fields = new Dictionary<string, (double, ExtractionStatus)>();
        foreach (var (name, accessor) in TotalRowFields)
        {
            var field = accessor(periodSummary);
            fields[name] = (field.Confidence, field.Status);
        }

        return (true, fields);
    }

    // -----------------------------------------------------------------------
    // Part A — the 5-demo verdict diff
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> DemoFixtureNames =>
    [
        ["good.pdf"],
        ["bad-math-cl21.pdf"],
        ["bad-font-cl35.pdf"],
        ["scanned.pdf"],
        ["compliant-master.pdf"],
    ];

    /// <summary>
    /// The C1.6 gate: for each of the 5 real demo fixtures, the verdict and every total-row
    /// field's raw confidence must be byte-identical whether <c>emitGeometricConfidence</c> is
    /// off or on. Per the class remarks, TotalCargos/TotalAbonos are <c>NotExtracted</c> on all 5
    /// fixtures today, so every per-field comparison below passes on <c>NotExtracted==NotExtracted,
    /// 0.0==0.0</c> — this proves the flag is inert on real data, not that a real clean pick
    /// survives arming (that proof is synthetic-only; see
    /// <c>GeometricPlausibilityTotalRowCalibrationTests</c>).
    /// </summary>
    [Theory]
    [MemberData(nameof(DemoFixtureNames))]
    public async Task DemoFixture_VerdictAndTotalRowConfidence_IdenticalFlagOffVsOn(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;

        var corpusDir = DemoCorpusDir;
        if (corpusDir is null)
            Assert.Skip("Repository root could not be determined from assembly location — demo corpus unreachable.");

        var fixturePath = Path.Combine(corpusDir, fixtureName);
        if (!File.Exists(fixturePath))
            Assert.Skip($"Demo fixture '{fixtureName}' not present at '{fixturePath}'.");

        var bundleRootDir = Path.Combine(corpusDir, "reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");

        var off = await RunPipelineAsync(corpusDir, bundleRootDir, fixtureName, emitGeometricConfidence: false, ct);
        var on = await RunPipelineAsync(corpusDir, bundleRootDir, fixtureName, emitGeometricConfidence: true, ct);

        var dump = $"'{fixtureName}':\n  {off.Dump("OFF")}\n  {on.Dump("ON")}";

        on.Signal.ShouldBe(off.Signal, dump);
        on.FailCheckIds.ShouldBe(off.FailCheckIds, dump);
        on.InsufficientDataCheckIds.ShouldBe(off.InsufficientDataCheckIds, dump);

        var (offExtracted, offFields) = await TryExtractTotalRowConfidenceAsync(corpusDir, fixtureName, false, ct);
        var (onExtracted, onFields) = await TryExtractTotalRowConfidenceAsync(corpusDir, fixtureName, true, ct);

        offExtracted.ShouldBe(
            onExtracted,
            $"'{fixtureName}': extraction success must not depend on the geometric-confidence flag.");

        if (!offExtracted)
            return;

        foreach (var (name, _) in TotalRowFields)
        {
            var (offConfidence, offStatus) = offFields[name];
            var (onConfidence, onStatus) = onFields[name];

            offStatus.ShouldBe(
                onStatus,
                $"'{fixtureName}'/{name}: extraction status must not depend on the flag.");
            onConfidence.ShouldBe(
                offConfidence,
                $"'{fixtureName}'/{name}: confidence changed OFF={offConfidence} -> ON={onConfidence} — " +
                "cardinal C1 false-abstain risk on REAL demo data.");

            if (onStatus == ExtractionStatus.Extracted)
            {
                onConfidence.ShouldBeGreaterThanOrEqualTo(
                    0.8,
                    $"'{fixtureName}'/{name}: confidence must clear the 0.8 guard when armed.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Part B — mandatory negative control (CL-44, not CL-21 — see class remarks)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The negative control the C1 design of record requires: a harness that only ever shows
    /// "nothing moved" cannot distinguish a working fix from an inert one. <c>decoy-total-amount.pdf</c>
    /// (C1.6) carries a decoy amount that <c>TryParseTotalRow</c>'s leftmost-match pick grabs
    /// instead of the true <c>TotalAbonos</c> value, proven separable by
    /// <c>GeometricPlausibilityTotalRowCalibrationTests</c>. This drives it through the FULL
    /// pipeline and proves the mis-pick flips CL-44 (<c>Cl44DesgloseTotalsMatchRule</c>, which
    /// directly reads TotalCargos/TotalAbonos and sums the movement rows against them) from a
    /// confident (wrong-sum) <c>Fail</c> to an honest <c>InsufficientData</c> abstain once armed.
    /// </summary>
    [Fact]
    public async Task NegativeControl_DecoyTotalAmount_Cl44FlipsToInsufficientDataWhenArmed()
    {
        var ct = TestContext.Current.CancellationToken;

        var syntheticDir = SyntheticCorpusDir;
        if (syntheticDir is null || RepoRoot is null)
            Assert.Skip("Repository root could not be determined from assembly location — synthetic corpus unreachable.");

        const string fixtureName = "decoy-total-amount.pdf";
        var fixturePath = Path.Combine(syntheticDir, fixtureName);
        if (!File.Exists(fixturePath))
            Assert.Skip($"Synthetic fixture '{fixtureName}' not present at '{fixturePath}'.");

        // Reuse the demo reference bundle — the s6211 baseline's TC-BSSB product token aliases
        // into products.csv exactly like s-c1-swap-displaced.pdf / decoy-resumen-amount.pdf (see
        // GeometricConfidenceArmingGateE2ETests / GeometricConfidenceResumenArmingGateE2ETests).
        var bundleRootDir = Path.Combine(RepoRoot, "Prisma/Fixtures/PRP2/demo/reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");

        // ------------------------------------------------------------------
        // Flag OFF — TotalAbonos mis-picked at confidence 1.0; CL-44 sees the wrong printed sum
        // and must NOT abstain (it has no reason to — confidence is 1.0) — it must FAIL, because
        // the movement-row sum ($67,796.35) does not match the decoy printed value ($99.99).
        // ------------------------------------------------------------------
        var off = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: false, ct);
        off.FailCheckIds.ShouldContain(
            "CL-44",
            "flag-off CL-44 must FAIL — the decoy TotalAbonos ($99.99) reports confidence 1.0 but "
            + "does not match the real movement-row sum ($67,796.35): " + off.Dump("OFF"));
        off.InsufficientDataCheckIds.ShouldNotContain(
            "CL-44",
            "flag-off CL-44 must not abstain — the mis-pick reports confidence 1.0: " + off.Dump("OFF"));

        // ------------------------------------------------------------------
        // Flag ON — the competition signal must abstain-gate CL-44's TotalAbonos operand.
        // ------------------------------------------------------------------
        var on = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: true, ct);
        on.InsufficientDataCheckIds.ShouldContain(
            "CL-44",
            "flag-on must convert the decoy-leaked pick into an honest abstain: " + on.Dump("ON"));
        on.FailCheckIds.ShouldNotContain(
            "CL-44",
            "flag-on CL-44 must abstain, not still confidently Fail: " + on.Dump("ON"));

        // ------------------------------------------------------------------
        // Corroborate at the confidence level (belt-and-braces with the verdict-level proof).
        // ------------------------------------------------------------------
        var (offExtracted, offFields) = await TryExtractTotalRowConfidenceAsync(syntheticDir, fixtureName, false, ct);
        offExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag off).");
        offFields["TotalAbonos"].Status.ShouldBe(ExtractionStatus.Extracted,
            $"'{fixtureName}': TotalAbonos must be Extracted (flag off).");
        offFields["TotalAbonos"].Confidence.ShouldBe(1.0,
            $"'{fixtureName}' (flag off): TotalAbonos confidence must be the pre-C1.6 constant 1.0.");

        var (onExtracted, onFields) = await TryExtractTotalRowConfidenceAsync(syntheticDir, fixtureName, true, ct);
        onExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag on).");
        onFields["TotalAbonos"].Status.ShouldBe(ExtractionStatus.Extracted,
            $"'{fixtureName}': TotalAbonos must be Extracted (flag on).");
        onFields["TotalAbonos"].Confidence.ShouldBeLessThan(0.8,
            $"'{fixtureName}' (flag on): TotalAbonos confidence must fall below the 0.8 guard.");

        // TotalCargos on this same fixture is an undisturbed clean pick — the flag must not have
        // collaterally abstain-gated it.
        onFields["TotalCargos"].Confidence.ShouldBe(1.0,
            $"'{fixtureName}'/TotalCargos: undisturbed sibling must stay at ceiling when armed.");
    }
}
