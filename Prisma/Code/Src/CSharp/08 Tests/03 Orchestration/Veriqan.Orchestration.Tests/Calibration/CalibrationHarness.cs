using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// §5.2 — Corpus calibration harness.
/// Runs the real verification pipeline over every specimen in a <see cref="CorpusManifest"/>,
/// aggregates findings into a <see cref="CalibrationResult"/>, and collects threshold-evidence
/// probe rows.  Purely read-only against production code — no production file is touched.
/// </summary>
public sealed class CalibrationHarness
{
    // -----------------------------------------------------------------------
    // Threshold constants (mirrored from rules for probe computation)
    // -----------------------------------------------------------------------

    private const double TypoMinSizeFloorPt   = 8.0;   // LAW-TYPO-MINSIZE body floor
    private const double TypoFechaFloorPt    = 10.0;   // LAW-TYPO-MINSIZE fecha-límite floor
    private const int    AdsMaxCharLength     = 805;    // LAW-ADS-PLACEMENT §12 max chars
    private const double SecSizeCapSec17     = 0.25;   // LAW-SEC-SIZECAP §17 cap = 25 %
    private const double SecSizeCapSec21And28 = 0.333; // LAW-SEC-SIZECAP §21/§28 cap = 33 %

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the harness over all specimens in the manifest and returns the aggregated result.
    /// </summary>
    /// <param name="manifest">Deserialized corpus manifest.</param>
    /// <param name="corpusDir">Absolute path to the directory containing the PDF fixtures.</param>
    /// <param name="cancellationToken">Propagated to all async operations.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with the <see cref="CalibrationResult"/>; or a failure
    /// result when the manifest is empty or an unrecoverable error occurs.
    /// Never throws for control flow.
    /// </returns>
    public async Task<Result<CalibrationResult>> RunAsync(
        CorpusManifest manifest,
        string corpusDir,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<CalibrationResult>();

        if (manifest is null)
            return Result<CalibrationResult>.WithFailure("Manifest must not be null.");

        if (manifest.Specimens.Count == 0)
            return Result<CalibrationResult>.WithFailure("Manifest contains no specimens.");

        var specimenResults = new List<SpecimenResult>();
        var probeRows = new List<ThresholdProbeRow>();

        foreach (var specimen in manifest.Specimens)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<CalibrationResult>();

            var specimenResult = await RunSpecimenAsync(specimen, corpusDir, probeRows, cancellationToken)
                .ConfigureAwait(false);
            specimenResults.Add(specimenResult);
        }

        var stats = AggregateStats(specimenResults, manifest.Specimens);
        var result = new CalibrationResult(specimenResults, stats, probeRows);
        return Result<CalibrationResult>.WithSuccess(result);
    }

    // -----------------------------------------------------------------------
    // Per-specimen pipeline execution
    // -----------------------------------------------------------------------

    private static async Task<SpecimenResult> RunSpecimenAsync(
        CorpusSpecimen specimen,
        string corpusDir,
        List<ThresholdProbeRow> probeRows,
        CancellationToken cancellationToken)
    {
        var pdfPath = Path.Combine(corpusDir, specimen.FileName);

        // Graceful skip — missing PDF is not an error (CI may lack binaries).
        if (!File.Exists(pdfPath))
        {
            return new SpecimenResult(
                Specimen: specimen,
                Skipped: true,
                Signal: null,
                FindingCount: 0,
                FailCheckIds: Array.Empty<string>(),
                NewFails: Array.Empty<string>(),
                DetectedDefects: Array.Empty<string>(),
                MissedDefects: Array.Empty<string>());
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken).ConfigureAwait(false);

        // Build a fresh DI container per specimen with a bundle tailored to it.
        // Per design §4: simplest correct approach is one container per specimen.
        var bundle = BuildBundle(specimen);
        var fakeProvider = Substitute.For<IVecReferenceDataProvider>();
        fakeProvider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VecReferenceBundle>.WithSuccess(bundle)));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();
        services.Replace(ServiceDescriptor.Scoped<IVecReferenceDataProvider>(_ => fakeProvider));
        services.AddVeriqanInMemoryPersistence();
        services.AddSingleton<VeriqanMetrics>();
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        await using var sp = services.BuildServiceProvider();

        var contextKey = new StatementContextKey(
            specimen.Bundle.Institution,
            specimen.Bundle.PeriodLabel);

        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: specimen.FileName,
            ContextKey: contextKey);

        Result<VerificationOutcome> pipelineResult;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            pipelineResult = await pipeline.ProcessAsync(submission, cancellationToken).ConfigureAwait(false);
        }

        if (!pipelineResult.IsSuccess)
        {
            // Pipeline-level failure (not a Blocked verdict — that is a success with Blocked signal).
            return new SpecimenResult(
                Specimen: specimen,
                Skipped: false,
                Signal: $"PIPELINE_FAILURE: {pipelineResult.Error}",
                FindingCount: 0,
                FailCheckIds: Array.Empty<string>(),
                NewFails: Array.Empty<string>(),
                DetectedDefects: Array.Empty<string>(),
                MissedDefects: Array.Empty<string>());
        }

        var outcome = pipelineResult.Value!;
        var signal = outcome.Summary.Signal.ToString();
        var findings = outcome.Findings;

        var failCheckIds = findings
            .Where(f => f.Verdict == FindingVerdict.Fail)
            .Select(f => f.CheckId)
            .OrderBy(id => id)
            .ToList();

        // Three suppression buckets — a Fail must clear ALL of them to be a NewFail.
        // allowedSet         : omitted-optional-reference-data gaps (not PDF defects).
        // knownFixtureDefects: genuine non-compliance properties of the fixture PDF.
        // intendedSet        : deliberately-injected defects in KnownBroken specimens.
        var allowedSet = new HashSet<string>(
            specimen.AllowedFails.Select(af => af.CheckId),
            StringComparer.OrdinalIgnoreCase);
        var fixtureDefectSet = new HashSet<string>(
            specimen.KnownFixtureDefects.Select(fd => fd.CheckId),
            StringComparer.OrdinalIgnoreCase);
        var intendedSet = new HashSet<string>(
            specimen.IntendedDefects.Select(d => d.CheckId),
            StringComparer.OrdinalIgnoreCase);

        var newFails = failCheckIds
            .Where(id => !allowedSet.Contains(id)
                      && !fixtureDefectSet.Contains(id)
                      && !intendedSet.Contains(id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var detectedDefects = specimen.IntendedDefects
            .Where(d => failCheckIds.Contains(d.CheckId, StringComparer.OrdinalIgnoreCase))
            .Select(d => d.CheckId)
            .ToList();

        var missedDefects = specimen.IntendedDefects
            .Where(d => !failCheckIds.Contains(d.CheckId, StringComparer.OrdinalIgnoreCase))
            .Select(d => d.CheckId)
            .ToList();

        // Threshold-evidence probes — call ExtractFullAsync for direct model access.
        await CollectProbesAsync(specimen, pdfBytes, sp, probeRows, findings, cancellationToken)
            .ConfigureAwait(false);

        return new SpecimenResult(
            Specimen: specimen,
            Skipped: false,
            Signal: signal,
            FindingCount: findings.Count,
            FailCheckIds: failCheckIds,
            NewFails: newFails,
            DetectedDefects: detectedDefects,
            MissedDefects: missedDefects);
    }

    // -----------------------------------------------------------------------
    // Threshold-evidence probes (§5.2)
    // -----------------------------------------------------------------------

    private static async Task CollectProbesAsync(
        CorpusSpecimen specimen,
        byte[] pdfBytes,
        ServiceProvider sp,
        List<ThresholdProbeRow> probeRows,
        IReadOnlyCollection<ExxerCube.Prisma.Veriqan.Domain.Verification.RuleFinding> findings,
        CancellationToken cancellationToken)
    {
        // Attempt to get the StatementModel via ExtractFullAsync for direct measurement.
        StatementModel? model = null;
        try
        {
            await using var extractScope = sp.CreateAsyncScope();
            var extractor = extractScope.ServiceProvider.GetRequiredService<IStatementFieldExtractor>();
            var extractResult = await extractor.ExtractFullAsync(pdfBytes, cancellationToken).ConfigureAwait(false);
            if (extractResult.IsSuccess)
                model = extractResult.Value;
        }
        catch (Exception)
        {
            // Probe collection is best-effort — never throw.
        }

        var specimenName = specimen.FileName;
        var label = specimen.Label;

        // --- LAW-TYPO-MINSIZE: real-word body point size distribution + fecha size ---
        if (model?.TypographySamples is { Count: > 0 } samples)
        {
            // Body words: non-bold, at least 2 characters (filters out isolated punctuation)
            var bodyWords = samples
                .Where(s => !s.IsBold && s.Text.Length >= 2)
                .ToList();

            if (bodyWords.Count > 0)
            {
                var minBodySize = bodyWords.Min(s => s.PointSize);
                var margin = minBodySize - TypoMinSizeFloorPt;
                probeRows.Add(new ThresholdProbeRow(
                    CheckId: "LAW-TYPO-MINSIZE",
                    Specimen: specimenName,
                    Label: label,
                    MeasuredValue: $"{minBodySize:F2} pt (min body word)",
                    CurrentThreshold: $"{TypoMinSizeFloorPt:F1} pt",
                    Margin: $"{margin:+0.00;-0.00} pt",
                    Note: $"Body word count: {bodyWords.Count}"));
            }

            // Fecha-límite words: search for the fecha-límite text vicinity.
            var fechaWords = samples
                .Where(s => s.Text.Contains("fecha", StringComparison.OrdinalIgnoreCase)
                         || s.Text.Contains("límite", StringComparison.OrdinalIgnoreCase)
                         || s.Text.Contains("limite", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (fechaWords.Count > 0)
            {
                var fechaSize = fechaWords.Min(s => s.PointSize);
                var fechaMargin = fechaSize - TypoFechaFloorPt;
                probeRows.Add(new ThresholdProbeRow(
                    CheckId: "LAW-TYPO-MINSIZE",
                    Specimen: specimenName,
                    Label: label,
                    MeasuredValue: $"{fechaSize:F2} pt (fecha-límite words)",
                    CurrentThreshold: $"{TypoFechaFloorPt:F1} pt",
                    Margin: $"{fechaMargin:+0.00;-0.00} pt",
                    Note: "Measured near 'fecha' / 'límite' text tokens"));
            }
        }

        // --- LAW-SEC-SIZECAP: §17 (≤ 25%), §21/§28 (≤ 33%) page-fraction ---
        // Note: §17 has a stricter cap than §21/§28 (Acuerdo §17 vs §21/§28).
        if (model?.Sections is { Count: > 0 } sections)
        {
            // Sections of interest: 17, 21, 28 — each with its own cap.
            var targetSections = new[] { (Num: 17, Cap: SecSizeCapSec17), (Num: 21, Cap: SecSizeCapSec21And28), (Num: 28, Cap: SecSizeCapSec21And28) };
            foreach (var (sectionNum, cap) in targetSections)
            {
                var sec = sections.FirstOrDefault(s => s.SectionNumber == sectionNum);
                if (sec is null || !sec.IsPresent)
                    continue;

                // Approximate text length as a proxy for section size.
                // Page-fraction cannot be computed without rendered page area here;
                // use text-length as an observable proxy and note limitation.
                var textLength = sec.SectionText.Length;
                probeRows.Add(new ThresholdProbeRow(
                    CheckId: "LAW-SEC-SIZECAP",
                    Specimen: specimenName,
                    Label: label,
                    MeasuredValue: $"§{sectionNum} text chars: {textLength}",
                    CurrentThreshold: $"≤ {cap:P0} of page area",
                    Margin: "n/a (text-length proxy; page-area requires rendered coords)",
                    Note: $"§{sectionNum} '{sec.Name}' — direct page-fraction from Observed if rule fires"));
            }
        }

        // Also capture §17/§21/§28 from findings' Observed field when the rule ran.
        // Determine the correct cap from the finding's locator / DOF numeral if available;
        // fall back to the conservative §17 cap (0.25) when the section cannot be inferred.
        foreach (var finding in findings.Where(f => f.CheckId == "LAW-SEC-SIZECAP" && f.Observed != null))
        {
            // Try to determine which section this finding is for from DofNumeral (string).
            // §17 has a 25% cap; §21 and §28 have a 33% cap.
            var isSection17 = finding.DofNumeral.Contains("17", StringComparison.Ordinal);
            var capForFinding = isSection17 ? SecSizeCapSec17 : SecSizeCapSec21And28;

            probeRows.Add(new ThresholdProbeRow(
                CheckId: "LAW-SEC-SIZECAP",
                Specimen: specimenName,
                Label: label,
                MeasuredValue: finding.Observed ?? string.Empty,
                CurrentThreshold: $"≤ {capForFinding:P0}",
                Margin: finding.Verdict == FindingVerdict.Fail ? "EXCEEDED" : "OK",
                Note: $"From RuleFinding.Observed (verdict: {finding.Verdict}, §{finding.DofNumeral})"));
        }

        // --- LAW-ADS-PLACEMENT: §12 char length ---
        foreach (var finding in findings.Where(f => f.CheckId == "LAW-ADS-PLACEMENT" && f.Observed != null))
        {
            var observedStr = finding.Observed ?? string.Empty;
            // Try to parse numeric value from Observed string
            var charLenStr = observedStr;
            probeRows.Add(new ThresholdProbeRow(
                CheckId: "LAW-ADS-PLACEMENT",
                Specimen: specimenName,
                Label: label,
                MeasuredValue: charLenStr,
                CurrentThreshold: $"≤ {AdsMaxCharLength} chars",
                Margin: finding.Verdict == FindingVerdict.Fail ? "EXCEEDED" : "OK",
                Note: $"From RuleFinding.Observed (verdict: {finding.Verdict})"));
        }

        // If §12 section text is available, measure its char length directly.
        if (model?.Sections is { Count: > 0 } secs12)
        {
            var sec12 = secs12.FirstOrDefault(s => s.SectionNumber == 12);
            if (sec12 is { IsPresent: true })
            {
                var len = sec12.SectionText.Length;
                var margin12 = AdsMaxCharLength - len;
                probeRows.Add(new ThresholdProbeRow(
                    CheckId: "LAW-ADS-PLACEMENT",
                    Specimen: specimenName,
                    Label: label,
                    MeasuredValue: $"{len} chars (§12 section text)",
                    CurrentThreshold: $"≤ {AdsMaxCharLength} chars",
                    Margin: $"{margin12:+0;-0} chars",
                    Note: "Measured from StatementModel.Sections[12].SectionText"));
            }
        }

        // --- LAW-§19-INTERES / currency residuals ---
        foreach (var finding in findings.Where(f =>
            (f.CheckId.StartsWith("LAW-", StringComparison.OrdinalIgnoreCase) ||
             f.CheckId.StartsWith("CL-19", StringComparison.OrdinalIgnoreCase) ||
             f.CheckId == "LAW-SEC-19-INTERES")
            && f.Observed != null
            && (f.Expected != null || f.ToleranceApplied != null)))
        {
            probeRows.Add(new ThresholdProbeRow(
                CheckId: finding.CheckId,
                Specimen: specimenName,
                Label: label,
                MeasuredValue: $"Observed={finding.Observed}",
                CurrentThreshold: finding.Expected ?? "(none)",
                Margin: finding.ToleranceApplied.HasValue
                    ? $"tolerance applied: {finding.ToleranceApplied.Value}"
                    : "n/a",
                Note: $"Verdict: {finding.Verdict}"));
        }
    }

    // -----------------------------------------------------------------------
    // Cross-specimen aggregation
    // -----------------------------------------------------------------------

    private static IReadOnlyList<CheckIdStats> AggregateStats(
        List<SpecimenResult> specimenResults,
        List<CorpusSpecimen> specimens)
    {
        // Gather all check ids that appeared in any run.
        var allCheckIds = specimenResults
            .Where(r => !r.Skipped)
            .SelectMany(r => r.FailCheckIds)
            .Concat(specimenResults
                .Where(r => !r.Skipped)
                .SelectMany(r => r.DetectedDefects))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id)
            .ToList();

        // KnownSynthetic specimens share the no-new-Fail guard with KnownGood specimens —
        // count both for FPR denominator.
        var knownGoodCount = specimens.Count(s =>
            s.Label == SpecimenLabel.KnownGood || s.Label == SpecimenLabel.KnownSynthetic);
        var stats = new List<CheckIdStats>();

        foreach (var checkId in allCheckIds)
        {
            // Count pass/fail/insufficientData across all non-skipped results.
            var failCount = specimenResults.Count(r =>
                !r.Skipped &&
                r.FailCheckIds.Contains(checkId, StringComparer.OrdinalIgnoreCase));

            var passCount = 0; // We only know pass when finding is explicitly listed; approx from non-fail below.
            var insufCount = 0;

            // Detection rate over KnownBroken specimens that declared this defect.
            var brokenThatIntendIt = specimens
                .Where(s => s.Label == SpecimenLabel.KnownBroken
                            && s.IntendedDefects.Any(d =>
                                d.CheckId.Equals(checkId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            double? detectionRate = null;
            if (brokenThatIntendIt.Count > 0)
            {
                var tpCount = specimenResults.Count(r =>
                    !r.Skipped
                    && r.Specimen.Label == SpecimenLabel.KnownBroken
                    && r.DetectedDefects.Contains(checkId, StringComparer.OrdinalIgnoreCase));
                detectionRate = (double)tpCount / brokenThatIntendIt.Count;
            }

            // FPR: KnownGood/KnownSynthetic specimens where the rule newly Failed
            // (i.e. is in NewFails — already filtered through all three suppression buckets).
            double? fpr = null;
            if (knownGoodCount > 0)
            {
                var fpCount = specimenResults.Count(r =>
                    !r.Skipped
                    && (r.Specimen.Label == SpecimenLabel.KnownGood
                        || r.Specimen.Label == SpecimenLabel.KnownSynthetic)
                    && r.NewFails.Contains(checkId, StringComparer.OrdinalIgnoreCase));
                fpr = (double)fpCount / knownGoodCount;
            }

            stats.Add(new CheckIdStats(
                CheckId: checkId,
                PassCount: passCount,
                FailCount: failCount,
                InsufficientDataCount: insufCount)
            {
                DetectionRate = detectionRate,
                FalsePositiveRate = fpr,
            });
        }

        return stats;
    }

    // -----------------------------------------------------------------------
    // Bundle construction (mirrors BuildFakeBundle from E2E test spine)
    // -----------------------------------------------------------------------

    private static VecReferenceBundle BuildBundle(CorpusSpecimen specimen)
    {
        var b = specimen.Bundle;
        var productId = b.ProductId;

        var aliases = new List<string>(b.Aliases) { b.ProductToken };
        if (!aliases.Contains(b.ProductName, StringComparer.OrdinalIgnoreCase))
            aliases.Add(b.ProductName);

        return new VecReferenceBundle(
            BundleMetadata: new BundleMetadata(
                SchemaVersion: "1.0.0",
                Institution: b.Institution,
                BundleId: $"calibration-{specimen.FileName}",
                GeneratedAt: DateTime.UtcNow.ToString("O"),
                Period: new PeriodRange(
                    Label: b.PeriodLabel,
                    Start: b.PeriodStart,
                    End: b.PeriodEnd),
                Source: new BundleSource(
                    Mechanism: "calibration-harness",
                    Reference: specimen.FileName,
                    Notes: null)),

            Products: new[]
            {
                new VecProduct(
                    ProductId: productId,
                    ProductName: b.ProductName,
                    Aliases: aliases,
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: new ProductTariffs(
                        AnnualCommission: b.AnnualCommissionMxn,
                        Currency: "MXN",
                        OtherCharges: null))
            },

            InterestRates: new[]
            {
                new InterestRateEntry(
                    ProductId: productId,
                    RatesByPeriod: new[]
                    {
                        new RateByPeriod(
                            AnnualOrdinaryFixedRate: b.AnnualOrdinaryRate,
                            PeriodLabel: b.PeriodLabel,
                            PeriodStart: b.PeriodStart,
                            PeriodEnd: b.PeriodEnd)
                    })
            },

            ClientAccounts: new[]
            {
                new ClientAccount(
                    ClientId: "CLIENT-001",
                    ClientName: new Domain.ReferenceData.ClientName(
                        FirstNames: "Juan",
                        LastNames: "Pérez García",
                        Full: "PÉREZ GARCÍA JUAN"),
                    Rfc: "PEGJ800101ABC",
                    ClientNumber: "12345678",
                    Address: new Domain.ReferenceData.Address(
                        Street: "Av. Insurgentes",
                        Number: "100",
                        Neighborhood: "Centro",
                        PostalCode: "06600",
                        State: "CDMX"),
                    Accounts: new[]
                    {
                        new AccountEntry(
                            AccountRef: "ACC-001",
                            ProductId: productId,
                            CardNumber: "4111XXXXXXXX1111",
                            Clabe: null,
                            BranchNumber: null,
                            CreditLine: b.CreditLine,
                            AccountOpenDate: "2020-01-15")
                    })
            },

            ToleranceConfig: new ToleranceConfig(
                CurrencyToleranceMxn: 0.50m,
                PointsTolerance: 1.00m,
                RewardsPesosToleranceMxn: 1.00m,
                PointsToPesosExchangeRate: 0.10m),

            ValidationConstants: new ValidationConstants(
                RequiredFontFamily: b.RequiredFontFamily,
                BankingYearDays: b.BankingYearDays,
                CatAnnualCommissionMxn: b.AnnualCommissionMxn),

            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            PriorStatements: null,
            ExpectedTransactions: null);
    }
}
