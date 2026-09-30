using System.Text.Json;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// GH#31 — oficio-classification evaluation harness (deterministic baseline track).
///
/// Runs the REAL deterministic <see cref="SemanticAnalyzerService"/> (fuzzy phrase matching, Ollama
/// disabled) over a diverse, labelled corpus of "Texto del oficio" variants
/// (<c>OficioClassificationCorpus.json</c>) and reports per-category accuracy so the classifier's
/// adaptiveness across authorities / phrasings / entity shapes / edge cases can be measured.
///
/// This is the BASELINE/ORACLE track. The agent-classifier track (client ask) is the SAME service with
/// <c>OllamaOptions.Enabled = true</c> and a real <see cref="ExxerCube.Prisma.Domain.Interfaces.IOllamaClient"/>
/// endpoint — see <c>AgentClassifierTrack_Comparison_RequiresOllamaEndpoint</c> for the seam. Feed both
/// tracks the same corpus and diff the reports to get the accuracy / cost / latency / explainability
/// trade-off the client wants.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Evaluation")]
public sealed class OficioClassificationEvalHarness
{
    private static readonly string[] Labels =
        ["Bloqueo", "Desbloqueo", "Documentacion", "Transferencia", "InformacionGeneral"];

    private readonly ITestOutputHelper _output;

    public OficioClassificationEvalHarness(ITestOutputHelper output) => _output = output;

    private sealed record CorpusEntry(
        string Id,
        bool AssertPrimary,
        string Primary,
        Dictionary<string, bool> Expected,
        string VariantClass,
        string Text);

    private static SemanticAnalyzerService BuildDeterministicAnalyzer()
    {
        var textComparer = new LevenshteinTextComparer(NullLogger<LevenshteinTextComparer>.Instance);
        // ollamaClient/ollamaOptions null → LLM enrichment disabled → pure deterministic baseline.
        return new SemanticAnalyzerService(
            textComparer,
            NullLogger<SemanticAnalyzerService>.Instance,
            ollamaClient: null,
            ollamaOptions: null);
    }

    private static IReadOnlyList<CorpusEntry> LoadCorpus()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "OficioClassificationCorpus.json");
        File.Exists(path).ShouldBeTrue($"corpus not copied to output: {path}");
        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<CorpusEntry>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        entries.ShouldNotBeNull();
        entries!.Count.ShouldBeGreaterThan(0);
        return entries;
    }

    private static Dictionary<string, bool> Predict(SemanticAnalysis a) => new()
    {
        ["Bloqueo"] = a.RequiereBloqueo?.EsRequerido == true,
        ["Desbloqueo"] = a.RequiereDesbloqueo?.EsRequerido == true,
        ["Documentacion"] = a.RequiereDocumentacion?.EsRequerido == true,
        ["Transferencia"] = a.RequiereTransferencia?.EsRequerido == true,
        ["InformacionGeneral"] = a.RequiereInformacionGeneral?.EsRequerido == true,
    };

    /// <summary>
    /// BASELINE GUARANTEE: every "clear" variant (assertPrimary=true) must have its primary apartado
    /// detected by the deterministic classifier. This is the demo-reliability regression guard.
    /// </summary>
    [Fact]
    public async Task DeterministicBaseline_DetectsPrimaryApartado_ForEveryClearVariant()
    {
        var analyzer = BuildDeterministicAnalyzer();
        var corpus = LoadCorpus();
        var ct = TestContext.Current.CancellationToken;

        var misses = new List<string>();
        int clear = 0;

        _output.WriteLine("=== GH#31 deterministic baseline — primary-apartado detection ===");
        foreach (var e in corpus.Where(e => e.AssertPrimary))
        {
            clear++;
            var result = await analyzer.AnalyzeDirectivesAsync(e.Text, expediente: null, ct);
            result.IsSuccess.ShouldBeTrue($"analysis failed for {e.Id}");
            var predicted = Predict(result.Value!);
            var detected = predicted[e.Primary];
            _output.WriteLine($"  [{(detected ? "OK " : "MISS")}] {e.Id,-34} primary={e.Primary,-18} ({e.VariantClass})");
            if (!detected)
            {
                misses.Add($"{e.Id} (expected primary {e.Primary})");
            }
        }

        _output.WriteLine($"  clear variants: {clear}, primary detected: {clear - misses.Count}/{clear}");
        misses.ShouldBeEmpty($"deterministic baseline missed primary apartado on: {string.Join("; ", misses)}");
    }

    /// <summary>
    /// MEASUREMENT (does not fail on cross-triggers): reports micro precision/recall/F1 over all five
    /// labels across the whole corpus, plus exact-match rate — the "adaptiveness" numbers to diff the
    /// agent track against. Asserts only a loose sanity floor so it stays a report, not a brittle gate.
    /// </summary>
    [Fact]
    public async Task DeterministicBaseline_ReportsMultiLabelMetrics_AcrossCorpus()
    {
        var analyzer = BuildDeterministicAnalyzer();
        var corpus = LoadCorpus();
        var ct = TestContext.Current.CancellationToken;

        int tp = 0, fp = 0, fn = 0, tn = 0, exactMatch = 0;
        var perLabelTp = Labels.ToDictionary(l => l, _ => 0);
        var perLabelFp = Labels.ToDictionary(l => l, _ => 0);
        var perLabelFn = Labels.ToDictionary(l => l, _ => 0);

        _output.WriteLine("=== GH#31 deterministic baseline — multi-label report ===");
        _output.WriteLine($"{"id",-34} {"expected",-24} {"predicted",-24} match");
        foreach (var e in corpus)
        {
            var result = await analyzer.AnalyzeDirectivesAsync(e.Text, expediente: null, ct);
            result.IsSuccess.ShouldBeTrue($"analysis failed for {e.Id}");
            var predicted = Predict(result.Value!);

            bool allCorrect = true;
            foreach (var label in Labels)
            {
                bool exp = e.Expected.TryGetValue(label, out var v) && v;
                bool pred = predicted[label];
                if (pred && exp) { tp++; perLabelTp[label]++; }
                else if (pred && !exp) { fp++; perLabelFp[label]++; allCorrect = false; }
                else if (!pred && exp) { fn++; perLabelFn[label]++; allCorrect = false; }
                else { tn++; }
            }
            if (allCorrect) { exactMatch++; }

            _output.WriteLine(
                $"{e.Id,-34} {ShortLabels(e.Expected),-24} {ShortLabels(predicted),-24} {(allCorrect ? "exact" : "-")}");
        }

        double precision = tp + fp == 0 ? 1.0 : (double)tp / (tp + fp);
        double recall = tp + fn == 0 ? 1.0 : (double)tp / (tp + fn);
        double f1 = precision + recall == 0 ? 0.0 : 2 * precision * recall / (precision + recall);

        _output.WriteLine("");
        _output.WriteLine("=== micro-averaged (all 5 labels) ===");
        _output.WriteLine($"  TP={tp} FP={fp} FN={fn} TN={tn}");
        _output.WriteLine($"  precision={precision:F3} recall={recall:F3} F1={f1:F3}");
        _output.WriteLine($"  exact-match: {exactMatch}/{corpus.Count} ({(double)exactMatch / corpus.Count:P0})");
        _output.WriteLine("");
        _output.WriteLine("=== per-label recall (detected / expected) ===");
        foreach (var label in Labels)
        {
            int exp = perLabelTp[label] + perLabelFn[label];
            double r = exp == 0 ? double.NaN : (double)perLabelTp[label] / exp;
            _output.WriteLine($"  {label,-18} recall={(double.IsNaN(r) ? "n/a" : r.ToString("F3"))} (TP={perLabelTp[label]} FP={perLabelFp[label]} FN={perLabelFn[label]})");
        }

        // Gross-regression floor only. The measured baseline recall sits ~0.74 BY DESIGN: the
        // InformacionGeneral apartado is brittle to phrasing/accent variation (recall ~0 on the varied
        // measure-only variants) while Bloqueo/Desbloqueo/Documentacion/Transferencia are robust
        // (recall 1.0). That gap is the headline "adaptiveness" finding and the prime target for the
        // agent-classifier track (which enriches exactly the Informacion apartado — see
        // SemanticAnalyzerService.EnrichInformacionWithLlmAsync). Floor is set well below current so this
        // stays a report, not a brittle gate.
        recall.ShouldBeGreaterThan(0.60, "deterministic baseline recall regressed below the sanity floor");
    }

    /// <summary>
    /// AGENT-CLASSIFIER TRACK (client ask) — documented seam, skipped until an Ollama endpoint is wired.
    /// To run: register a real IOllamaClient (OllamaHttpClient) pointing at an Ollama service + model,
    /// build <c>new SemanticAnalyzerService(textComparer, logger, ollamaClient, Options.Create(new
    /// OllamaOptions{ Enabled = true, Model = "…" }))</c>, run it over the SAME corpus with the same
    /// Predict()/metrics above, and diff the two reports (accuracy / latency / cost / explainability).
    /// No Ollama service is present in the demo stack today, so this is intentionally not executed here.
    /// </summary>
    [Fact(Skip = "Agent track: needs an Ollama endpoint + model in the stack — see method doc (GH#31).")]
    public void AgentClassifierTrack_Comparison_RequiresOllamaEndpoint()
    {
    }

    private static string ShortLabels(Dictionary<string, bool> labels) =>
        string.Join(",", Labels.Where(l => labels.TryGetValue(l, out var v) && v).Select(l => l[..3])) is { Length: > 0 } s
            ? s
            : "(none)";
}
