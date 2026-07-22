using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
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
/// RC1.S2 — baseline measurement run: drives the full Veriqan verdict pipeline (mirroring the
/// exact DI composition used by <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Tests.VecChecklistDemoE2ETests"/>) over all 16 entries of the
/// real, anonymized corpus resolved by <see cref="RealCorpusFixtureLocator"/>, and separately
/// captures per-field extraction status/confidence by calling <see cref="IStatementFieldExtractor"/>
/// directly (that data is NOT reachable from <see cref="VerificationOutcome"/> — confirmed by
/// inspection: <c>VerificationOutcome</c> carries only <c>Job</c>/<c>Summary</c>/<c>Findings</c>, and
/// the internal <c>StatementModel</c> built at pipeline Stage 2 is discarded before the outcome is
/// constructed). Both calls use the same real DI-wired <c>EscalatingStatementFieldExtractor</c>
/// (registered by <c>AddVeriqanExtraction()</c>), so the standalone extraction call is not an
/// invented code path — it is the identical port the pipeline itself calls at Stage 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measurement only (no assertions on outcomes):</b> per the RC1.S2 program rule, this test
/// NEVER fails because of a verdict signal, a Result failure, or an exception raised while
/// processing a specimen — every one of those is itself a measurement data point and is recorded
/// into the report. The test only fails if the HARNESS breaks: the corpus can't be read, the
/// expected 16-row loud-loader count is wrong, or a report file can't be written.
/// </para>
/// <para>
/// <b>Reference bundle reuse (no invented composition):</b> the only reference bundle that exists
/// in this repository is <c>Prisma/Fixtures/PRP2/demo/reference-bundle/Demo_Bank_(Iqubica)</c> (the
/// same one <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Tests.VecChecklistDemoE2ETests"/> uses). The 4 defect specimens in the real corpus
/// index are in fact byte-identical to 3 of that demo test's fixtures (verified via sha256:
/// <c>defect-good</c> == demo <c>good.pdf</c>; <c>defect-bad-math-cl21</c> == demo
/// <c>bad-math-cl21.pdf</c>; <c>defect-bad-font-cl35</c> == demo <c>bad-font-cl35.pdf</c>), so
/// reusing that bundle for all 16 entries (rather than inventing a second, unauthored bundle) is the
/// only reuse-not-invent option available. For the 12 real account statements this may cause
/// InsufficientData/ExtractionGap findings where the bundle's reference data doesn't cover a given
/// account/product/period — that is itself a valid, recorded measurement, not a defect to fix here.
/// </para>
/// <para>
/// <b>Two-tier PII discipline:</b> the machine-readable JSON report
/// (<c>real-corpus-baseline.json</c>) is written OUT-OF-REPO, beside the corpus at
/// <see cref="RealCorpusFixtureLocator.ResolveRoot"/>, and may carry raw extracted values
/// (amounts, dates, names) because it never enters git. The committed Markdown report
/// (<c>docs/qa/calibration/real-corpus-baseline-2026-07.md</c>) is generated from the SAME
/// in-memory measurements but deliberately omits every raw field value, every rule
/// Expected/Observed string, and every staging path — it carries only neutral ids, check ids,
/// verdict/status enum names, counts, confidence numbers, and durations.
/// </para>
/// </remarks>
[Trait("Category", "RealCorpus")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class RealCorpusBaselineMeasurementTests
{
    /// <summary>Institution key for the only reference bundle available in this repository.</summary>
    private const string Institution = "Demo Bank (Iqubica)";

    /// <summary>Repo-relative path to the reference-bundle root (parent of the institution folder).</summary>
    private const string ReferenceBundleRelativePath = "Prisma/Fixtures/PRP2/demo/reference-bundle";

    /// <summary>Repo-relative path of the committed, PII-free human report.</summary>
    private const string CommittedReportRelativePath = "docs/qa/calibration/real-corpus-baseline-2026-07.md";

    [Fact]
    public async Task RealCorpus_AllSpecimens_MeasuredAndReported()
    {
        var ct = TestContext.Current.CancellationToken;

        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null)
        {
            Assert.Skip(
                $"Real corpus not staged locally — set {RealCorpusFixtureLocator.RootEnvVar} to the " +
                "staging root containing corpus-index.json. Expected in CI (no corpus shipped).");
            return;
        }

        var repoRoot = ComputeRepoRoot();
        if (repoRoot is null)
        {
            Assert.Skip("Repository root could not be determined from assembly location.");
            return;
        }

        var bundleRootDir = Path.Combine(repoRoot, ReferenceBundleRelativePath);
        if (!Directory.Exists(bundleRootDir))
        {
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");
            return;
        }

        var measurements = new List<StatementMeasurement>();

        foreach (var entry in fixture.Entries)
        {
            var measurement = await MeasureEntryAsync(fixture.RootDir, entry, bundleRootDir, ct);
            measurements.Add(measurement);
        }

        // Loud-loader completeness: never let a silently-shrunk measurement set pass as "done".
        measurements.Count.ShouldBe(
            16,
            $"expected exactly 16 measurement rows (one per corpus-index.json entry), got {measurements.Count}.");

        var jsonPath = Path.Combine(fixture.RootDir, "real-corpus-baseline.json");
        await WriteJsonReportAsync(jsonPath, measurements, ct);
        File.Exists(jsonPath).ShouldBeTrue($"Machine-readable baseline report must exist at '{jsonPath}'.");

        var mdPath = Path.Combine(repoRoot, CommittedReportRelativePath);
        WriteMarkdownReport(mdPath, measurements);
        File.Exists(mdPath).ShouldBeTrue($"Committed human report must exist at '{mdPath}'.");
    }

    // -----------------------------------------------------------------------
    // Per-entry measurement
    // -----------------------------------------------------------------------

    private static async Task<StatementMeasurement> MeasureEntryAsync(
        string corpusRoot,
        RealCorpusEntry entry,
        string bundleRootDir,
        CancellationToken ct)
    {
        var pdfPath = Path.Combine(corpusRoot, entry.RelativePath);
        if (!File.Exists(pdfPath))
        {
            return new StatementMeasurement(
                entry.Id, entry.Product, entry.Period, entry.DefectKind, entry.AccountLabel,
                Extraction: new ExtractionMeasurement(false, $"File not found: {entry.RelativePath}", 0, []),
                Verdict: new VerdictMeasurement(
                    false, "File not found", 0, null, null, null, null, null, null, null, null,
                    [], [], [], [], null, null, []));
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct).ConfigureAwait(false);

        var contextKey = new StatementContextKey(
            Institution,
            PeriodLabel: entry.Period ?? entry.DefectKind);

        // -------------------------------------------------------------------
        // DI composition — identical to VecChecklistDemoE2ETests.
        // -------------------------------------------------------------------
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

        var extraction = await MeasureExtractionAsync(sp, pdfBytes, ct).ConfigureAwait(false);
        var verdict = await MeasureVerdictAsync(sp, pdfBytes, entry.Id, contextKey, ct).ConfigureAwait(false);

        return new StatementMeasurement(
            entry.Id, entry.Product, entry.Period, entry.DefectKind, entry.AccountLabel,
            extraction, verdict);
    }

    /// <summary>
    /// Calls <see cref="IStatementFieldExtractor.ExtractFullAsync"/> directly — the same port the
    /// pipeline calls internally at Stage 2 — because <see cref="VerificationOutcome"/> does not
    /// surface per-field extraction status/confidence. Never throws: any exception or Result
    /// failure is captured as a measurement, never propagated.
    /// </summary>
    private static async Task<ExtractionMeasurement> MeasureExtractionAsync(
        ServiceProvider sp, byte[] pdfBytes, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = sp.CreateAsyncScope();
            var extractor = scope.ServiceProvider.GetRequiredService<IStatementFieldExtractor>();
            var result = await extractor.ExtractFullAsync(pdfBytes, ct).ConfigureAwait(false);
            sw.Stop();

            if (!result.IsSuccess || result.Value is null)
                return new ExtractionMeasurement(false, result.Error ?? "<no value>", sw.ElapsedMilliseconds, []);

            var fields = new List<FieldMeasurement>();
            fields.AddRange(EnumerateExtractedFields(result.Value, prefix: ""));
            if (result.Value.PeriodSummary is not null)
                fields.AddRange(EnumerateExtractedFields(result.Value.PeriodSummary, prefix: "PeriodSummary."));

            return new ExtractionMeasurement(true, null, sw.ElapsedMilliseconds, fields);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new ExtractionMeasurement(false, $"{ex.GetType().Name}: {ex.Message}", sw.ElapsedMilliseconds, []);
        }
    }

    /// <summary>
    /// Runs the full verification pipeline exactly as <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Tests.VecChecklistDemoE2ETests"/> does.
    /// Never throws: any exception or Result failure is captured as a measurement, never propagated.
    /// </summary>
    private static async Task<VerdictMeasurement> MeasureVerdictAsync(
        ServiceProvider sp, byte[] pdfBytes, string fileNameForSubmission,
        StatementContextKey contextKey, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var submission = new StatementSubmission(
                Pdf: pdfBytes,
                FileName: fileNameForSubmission,
                ContextKey: contextKey);

            Result<VerificationOutcome> result;
            await using (var scope = sp.CreateAsyncScope())
            {
                var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
                result = await pipeline.ProcessAsync(submission, ct).ConfigureAwait(false);
            }

            sw.Stop();

            if (!result.IsSuccess || result.Value is null)
                return new VerdictMeasurement(
                    false, result.Error ?? "<no value>", sw.ElapsedMilliseconds,
                    null, null, null, null, null, null, null, null,
                    [], [], [], [], null, null, []);

            var outcome = result.Value;
            var summary = outcome.Summary;

            var findings = outcome.Findings.Select(f => new FindingMeasurement(
                f.CheckId,
                f.Verdict.ToString(),
                f.Severity.ToString(),
                f.Technique.ToString(),
                f.Confidence,
                f.Expected,
                f.Observed,
                f.ToleranceApplied)).ToList();

            return new VerdictMeasurement(
                true, null, sw.ElapsedMilliseconds,
                summary.Signal.ToString(),
                summary.BankTierVerdict.ToString(),
                summary.CondusefTierVerdict.ToString(),
                summary.Confidence,
                summary.FailCount,
                summary.PassCount,
                summary.InsufficientDataCount,
                summary.Total,
                summary.FailCheckIds,
                summary.InsufficientDataCheckIds,
                summary.BankFailCheckIds,
                summary.CondusefFailCheckIds,
                summary.BlockedOutcome?.Reason.ToString(),
                summary.BlockedOutcome?.Detail,
                findings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new VerdictMeasurement(
                false, $"{ex.GetType().Name}: {ex.Message}", sw.ElapsedMilliseconds,
                null, null, null, null, null, null, null, null,
                [], [], [], [], null, null, []);
        }
    }

    /// <summary>
    /// Reflectively enumerates every <c>ExtractedField&lt;T&gt;</c>-typed public property on
    /// <paramref name="model"/> (works uniformly across <c>StatementModel</c>'s and
    /// <c>PeriodSummary</c>'s differing generic type arguments without needing one branch per
    /// field). Non-<c>ExtractedField&lt;T&gt;</c> properties (movements, sections, font runs, …)
    /// are skipped — they are not per-field extraction outcomes.
    /// </summary>
    private static IEnumerable<FieldMeasurement> EnumerateExtractedFields(object model, string prefix)
    {
        foreach (var prop in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.PropertyType.IsGenericType ||
                prop.PropertyType.GetGenericTypeDefinition() != typeof(ExtractedField<>))
                continue;

            var fieldObj = prop.GetValue(model);
            if (fieldObj is null)
                continue;

            var statusValue = prop.PropertyType.GetProperty("Status")!.GetValue(fieldObj);
            var confidenceValue = prop.PropertyType.GetProperty("Confidence")!.GetValue(fieldObj);
            var rawValue = prop.PropertyType.GetProperty("Value")!.GetValue(fieldObj);

            yield return new FieldMeasurement(
                prefix + prop.Name,
                statusValue!.ToString()!,
                (double)confidenceValue!,
                rawValue?.ToString());
        }
    }

    // -----------------------------------------------------------------------
    // Repo-root resolution — mirrors VecChecklistDemoE2ETests.ComputeDemoCorpusDir.
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

    // -----------------------------------------------------------------------
    // JSON report (out-of-repo, full detail)
    // -----------------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static async Task WriteJsonReportAsync(
        string path, IReadOnlyList<StatementMeasurement> measurements, CancellationToken ct)
    {
        var report = new BaselineReport(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            EntryCount: measurements.Count,
            Statements: measurements);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, report, JsonOptions, ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Markdown report (committed, PII-free)
    // -----------------------------------------------------------------------

    private static void WriteMarkdownReport(string path, IReadOnlyList<StatementMeasurement> measurements)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Real-Corpus Baseline Measurement (RC1.S2)");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd}. Measurement only — no calibration, no");
        sb.AppendLine("tuning, no assertions on outcomes. Full raw detail (amounts, dates, names) lives");
        sb.AppendLine("out-of-repo in `real-corpus-baseline.json` beside the staged corpus. This file");
        sb.AppendLine("contains only neutral statement ids, check ids, verdict/status enum names, counts,");
        sb.AppendLine("confidence numbers, and durations — no raw extracted values, no staging paths.");
        sb.AppendLine();
        sb.AppendLine("Reference bundle used for every entry: the single bundle in this repo,");
        sb.AppendLine("`Demo_Bank_(Iqubica)` — see the class doc-comment on");
        sb.AppendLine("`RealCorpusBaselineMeasurementTests` for why reusing it (rather than authoring an");
        sb.AppendLine("unverified second bundle) is the correct \"don't invent a new composition\" choice.");
        sb.AppendLine();

        var accountEntries = measurements.Where(m => m.DefectKind is null).ToList();
        var defectEntries = measurements.Where(m => m.DefectKind is not null).ToList();

        // ---------------------------------------------------------------
        // Per-statement verdict table (real account statements)
        // ---------------------------------------------------------------
        sb.AppendLine("## Per-statement verdict summary (12 real account statements)");
        sb.AppendLine();
        sb.AppendLine("| Id | Product | Signal | BankTier | CondusefTier | Fail | InsufficientData | Pass | Extraction ms | Verdict ms |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var m in accountEntries.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            sb.AppendLine(
                $"| {m.Id} | {m.Product} | {DisplaySignal(m)} | {m.Verdict.BankTierVerdict ?? "—"} | " +
                $"{m.Verdict.CondusefTierVerdict ?? "—"} | {m.Verdict.FailCount?.ToString() ?? "—"} | " +
                $"{m.Verdict.InsufficientDataCount?.ToString() ?? "—"} | {m.Verdict.PassCount?.ToString() ?? "—"} | " +
                $"{m.Extraction.WallTimeMs} | {m.Verdict.WallTimeMs} |");
        }
        sb.AppendLine();

        // ---------------------------------------------------------------
        // Per-check aggregate (real account statements only)
        // ---------------------------------------------------------------
        sb.AppendLine("## Per-check aggregate — real account statements (12)");
        sb.AppendLine();
        sb.AppendLine("Counts of Fail / InsufficientData(Abstain) / Pass across the 12 real account");
        sb.AppendLine("statements, by CheckId. A check absent from a statement's findings entirely is");
        sb.AppendLine("not counted for that statement (engine didn't evaluate it).");
        sb.AppendLine();
        sb.AppendLine("| CheckId | Severity (max seen) | Fail | Abstain | Pass | Statements seen |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var severityRank = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Info"] = 0,
            ["Warning"] = 1,
            ["Critical"] = 2,
        };
        var checkAgg = accountEntries
            .SelectMany(m => m.Verdict.Findings.Select(f => (m.Id, f)))
            .GroupBy(x => x.f.CheckId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var g in checkAgg)
        {
            var fail = g.Count(x => x.f.Verdict == "Fail");
            var abstain = g.Count(x => x.f.Verdict == "InsufficientData");
            var pass = g.Count(x => x.f.Verdict == "Pass");
            var maxSeverity = g.Select(x => x.f.Severity).Distinct()
                .OrderByDescending(s => severityRank.GetValueOrDefault(s, -1))
                .First();
            sb.AppendLine($"| {g.Key} | {maxSeverity} | {fail} | {abstain} | {pass} | {g.Select(x => x.Id).Distinct().Count()} |");
        }
        sb.AppendLine();

        // ---------------------------------------------------------------
        // Extraction coverage matrix
        // ---------------------------------------------------------------
        sb.AppendLine("## Extraction coverage matrix — field x account series (real statements)");
        sb.AppendLine();
        sb.AppendLine("For each account series (A = checking, B/C = credit card), how many of the 4");
        sb.AppendLine("monthly statements extracted the field (`Status == Extracted`), and the mean");
        sb.AppendLine("confidence across all 4 attempts (extracted or not).");
        sb.AppendLine();
        sb.AppendLine("| Field | A (checking) extracted/4 | A mean conf | B (credit card) extracted/4 | B mean conf | C (credit card) extracted/4 | C mean conf |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        var byAccount = accountEntries
            .Where(m => m.AccountLabel is not null)
            .GroupBy(m => m.AccountLabel!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var allFieldNames = accountEntries
            .SelectMany(m => m.Extraction.Fields.Select(f => f.FieldName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        foreach (var fieldName in allFieldNames)
        {
            var cells = new List<string>();
            foreach (var label in new[] { "A", "B", "C" })
            {
                if (!byAccount.TryGetValue(label, out var stmts))
                {
                    cells.Add("—");
                    cells.Add("—");
                    continue;
                }

                var attempts = stmts
                    .Select(m => m.Extraction.Fields.FirstOrDefault(f => f.FieldName == fieldName))
                    .Where(f => f is not null)
                    .Select(f => f!)
                    .ToList();

                var extractedCount = attempts.Count(f => f.Status == "Extracted");
                var meanConf = attempts.Count > 0 ? attempts.Average(f => f.Confidence) : 0.0;
                cells.Add($"{extractedCount}/{attempts.Count}");
                cells.Add(meanConf.ToString("0.00"));
            }

            sb.AppendLine($"| {fieldName} | {cells[0]} | {cells[1]} | {cells[2]} | {cells[3]} | {cells[4]} | {cells[5]} |");
        }
        sb.AppendLine();

        // ---------------------------------------------------------------
        // Defect-specimen sanity block
        // ---------------------------------------------------------------
        sb.AppendLine("## Defect-specimen sanity check (4 specimens)");
        sb.AppendLine();
        sb.AppendLine("These 4 files are the same fixtures used by the synthetic demo E2E suite");
        sb.AppendLine("(`VecChecklistDemoE2ETests`) — `defect-good` / `defect-bad-math-cl21` /");
        sb.AppendLine("`defect-bad-font-cl35` are byte-identical to that suite's `good.pdf` /");
        sb.AppendLine("`bad-math-cl21.pdf` / `bad-font-cl35.pdf` (verified by sha256 in the corpus");
        sb.AppendLine("index). Their known injected defects (from that suite's history) are listed for");
        sb.AppendLine("comparison; the Actual columns are this run's measurement.");
        sb.AppendLine();
        sb.AppendLine("| Id | Known injected defect | Actual Signal | Actual FailCheckIds | Actual InsufficientDataCheckIds |");
        sb.AppendLine("|---|---|---|---|---|");
        var knownDefects = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["defect-good"] = "none (reference-quality demo PDF; historically RED on structural/legend checks, not arithmetic)",
            ["defect-bad-math-cl21"] = "+$11.00 injected on PagoParaNoGenerarIntereses (historically trips CL-21 + CL-22)",
            ["defect-bad-font-cl35"] = "Courier font substituted for required Helvetica (historically trips CL-35)",
            ["defect-scanned"] = "image-only PDF, no text layer (historically ExtractionGap)",
        };
        foreach (var m in defectEntries.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            var known = knownDefects.GetValueOrDefault(m.Id, "—");
            sb.AppendLine(
                $"| {m.Id} | {known} | {DisplaySignal(m)} | " +
                $"{string.Join(", ", m.Verdict.FailCheckIds)} | {string.Join(", ", m.Verdict.InsufficientDataCheckIds)} |");
        }
        sb.AppendLine();

        // ---------------------------------------------------------------
        // Notable observations — computed strictly from the measured data.
        // ---------------------------------------------------------------
        sb.AppendLine("## Notable observations");
        sb.AppendLine();

        var harnessFailures = measurements.Where(m => !m.Verdict.Success || !m.Extraction.Success).ToList();
        if (harnessFailures.Count > 0)
        {
            sb.AppendLine($"- {harnessFailures.Count}/16 statements hit a Result failure or exception in the");
            sb.AppendLine("  pipeline and/or the standalone extractor call (recorded, not treated as a defect");
            sb.AppendLine("  here — see the JSON report for the captured error message per statement).");
        }
        else
        {
            sb.AppendLine("- No harness-level Result failures or exceptions were recorded for any of the 16 statements.");
        }

        foreach (var fieldName in new[] { "PeriodSummary.Tasa", "PeriodSummary.Cat", "PeriodSummary.TotalCargos", "PeriodSummary.TotalAbonos" })
        {
            foreach (var label in new[] { "B", "C" })
            {
                if (!byAccount.TryGetValue(label, out var stmts))
                    continue;

                var attempts = stmts
                    .Select(m => m.Extraction.Fields.FirstOrDefault(f => f.FieldName == fieldName))
                    .Where(f => f is not null)
                    .Select(f => f!)
                    .ToList();

                if (attempts.Count == 0)
                    continue;

                var extractedCount = attempts.Count(f => f.Status == "Extracted");
                sb.AppendLine(
                    $"- `{fieldName}` on account series {label} (credit card, {attempts.Count} statements): " +
                    $"extracted {extractedCount}/{attempts.Count}.");
            }
        }

        var accountAAbstainsOrUnknown = accountEntries
            .Where(m => m.AccountLabel == "A")
            .Count(m => m.Verdict.Signal is "ExtractionGap" or "Blocked" || (m.Verdict.InsufficientDataCount ?? 0) > 0);
        sb.AppendLine(
            $"- Account A (checking/savings product) shows {accountAAbstainsOrUnknown}/4 statements with an" +
            " abstention-shaped outcome (ExtractionGap/Blocked signal or non-zero InsufficientData count) —" +
            " expected per GH#19: non-credit-card products may legitimately not resolve against a" +
            " credit-card-oriented reference bundle; this is an honest abstention, not a defect.");

        File.WriteAllText(path, sb.ToString());
    }

    private static string DisplaySignal(StatementMeasurement m) =>
        m.Verdict.Success ? (m.Verdict.Signal ?? "—") : $"HARNESS-RECORDED-FAILURE ({m.Verdict.ErrorMessage})";

    // -----------------------------------------------------------------------
    // Measurement DTOs (serialized to JSON; the MD writer reads a strict subset)
    // -----------------------------------------------------------------------

    private sealed record BaselineReport(
        int SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        int EntryCount,
        IReadOnlyList<StatementMeasurement> Statements);

    private sealed record StatementMeasurement(
        string Id,
        string Product,
        string? Period,
        string? DefectKind,
        string? AccountLabel,
        ExtractionMeasurement Extraction,
        VerdictMeasurement Verdict);

    private sealed record ExtractionMeasurement(
        bool Success,
        string? ErrorMessage,
        long WallTimeMs,
        IReadOnlyList<FieldMeasurement> Fields);

    private sealed record FieldMeasurement(
        string FieldName,
        string Status,
        double Confidence,
        string? Value);

    private sealed record VerdictMeasurement(
        bool Success,
        string? ErrorMessage,
        long WallTimeMs,
        string? Signal,
        string? BankTierVerdict,
        string? CondusefTierVerdict,
        double? SummaryConfidence,
        int? FailCount,
        int? PassCount,
        int? InsufficientDataCount,
        int? Total,
        IReadOnlyList<string> FailCheckIds,
        IReadOnlyList<string> InsufficientDataCheckIds,
        IReadOnlyList<string> BankFailCheckIds,
        IReadOnlyList<string> CondusefFailCheckIds,
        string? BlockedReason,
        string? BlockedDetail,
        IReadOnlyList<FindingMeasurement> Findings);

    private sealed record FindingMeasurement(
        string CheckId,
        string Verdict,
        string Severity,
        string Technique,
        double Confidence,
        string? Expected,
        string? Observed,
        decimal? ToleranceApplied);
}
