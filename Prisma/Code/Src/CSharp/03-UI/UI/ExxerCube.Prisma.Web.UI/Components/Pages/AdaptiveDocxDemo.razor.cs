using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Web.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Components.Pages;

/// <summary>
/// AdaptiveDocxDemo
/// </summary>
public partial class AdaptiveDocxDemo : ComponentBase
{
    private static readonly AdaptiveDocxFixture PlaceholderFixture = new("placeholder", "Select fixture", string.Empty, string.Empty, string.Empty);
    private static readonly DocxFixtureContent PlaceholderContent = new(PlaceholderFixture, string.Empty, string.Empty);

    private IReadOnlyList<AdaptiveDocxFixture> _fixtures = Array.Empty<AdaptiveDocxFixture>();
    private string _selectedFixtureKey = string.Empty;
    private AdaptiveDocxFixture _selectedFixture = PlaceholderFixture;
    private DocxFixtureContent _loadedFixture = PlaceholderContent;
    private bool _hasLoadedFixture;
    private string _preview = string.Empty;
    private string _docxText = string.Empty;

    private bool _loadingFixture;
    private bool _running;
    private string _error = string.Empty;

    private ExtractionMode _mode = ExtractionMode.BestStrategy;
    private string _existingExpediente = string.Empty;
    private string _existingCausa = string.Empty;
    private string _existingAccion = string.Empty;

    private IReadOnlyList<StrategyConfidence> _confidences = Array.Empty<StrategyConfidence>();
    private IReadOnlyList<StrategyRunResult> _strategyRuns = Array.Empty<StrategyRunResult>();
    private ExtractedFields _result = new();
    private bool _hasResult;
    private MergeResult _mergeResult = new();
    private List<KeyValuePair<string, string>> _additionalFields = new();

    /// <summary>
    /// OnInitializedAsync
    /// </summary>
    /// <returns></returns>
    protected override async Task OnInitializedAsync()
    {
        await LoadFixturesAsync();
        if (!string.IsNullOrWhiteSpace(_selectedFixtureKey))
        {
            await LoadFixtureAsync();
        }
    }

    private async Task OnFixtureChanged(string key)
    {
        _selectedFixtureKey = key;
        await LoadFixtureAsync();
    }

    private async Task LoadFixturesAsync()
    {
        _fixtures = await FixtureService.GetFixturesAsync();
        if (_fixtures.Count > 0)
        {
            _selectedFixtureKey = _fixtures[0].Key;
            _selectedFixture = _fixtures[0];
        }
    }

    private async Task LoadFixtureAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedFixtureKey))
        {
            return;
        }

        _loadingFixture = true;
        _error = string.Empty;
        _hasLoadedFixture = false;
        _hasResult = false;
        _additionalFields.Clear();

        try
        {
            var selected = _fixtures.FirstOrDefault(f => f.Key == _selectedFixtureKey);
            _selectedFixture = selected ?? PlaceholderFixture;

            var loaded = await FixtureService.LoadAsync(_selectedFixtureKey);

            if (loaded is null)
            {
                _error = "Could not load the selected fixture.";
                _docxText = string.Empty;
                _preview = string.Empty;
                _loadedFixture = PlaceholderContent;
                return;
            }

            _loadedFixture = loaded;
            _hasLoadedFixture = true;
            _docxText = _loadedFixture.Text;
            _preview = string.Join(
                Environment.NewLine,
                _loadedFixture.Text
                    .Split(Environment.NewLine)
                    .Take(14));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load fixture {Fixture}", _selectedFixtureKey);
            _error = "Unexpected error while loading the fixture.";
        }
        finally
        {
            _loadingFixture = false;
        }
    }

    private async Task PrimeConfidencesAsync()
    {
        if (string.IsNullOrWhiteSpace(_docxText))
        {
            _error = "Select a fixture first.";
            return;
        }

        try
        {
            var token = CancellationToken.None;
            _confidences = await AdaptiveExtractor.GetStrategyConfidencesAsync(_docxText, token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh confidences");
            _error = "Could not refresh confidences.";
        }
    }

    private async Task RunExtractionAsync()
    {
        if (string.IsNullOrWhiteSpace(_docxText))
        {
            _error = "Select a fixture first.";
            return;
        }

        _running = true;
        _error = string.Empty;
        _hasResult = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var token = cts.Token;

            _confidences = await AdaptiveExtractor.GetStrategyConfidencesAsync(_docxText, token);
            _strategyRuns = await RunStrategiesAsync(_docxText, token);
            _mergeResult = new MergeResult();
            _additionalFields.Clear();

            var existing = BuildExistingFields();
            ExtractedFields? extractionResult;

            switch (_mode)
            {
                case ExtractionMode.MergeAll:
                    extractionResult = await AdaptiveExtractor.ExtractAsync(_docxText, ExtractionMode.MergeAll, null, token);
                    _mergeResult = await FieldMergeStrategy.MergeAsync(
                        _strategyRuns
                            .Where(r => r.HasExtraction)
                            .Select(r => r.Fields)
                            .ToList(),
                        token);
                    break;

                case ExtractionMode.Complement:
                    extractionResult = await AdaptiveExtractor.ExtractAsync(_docxText, ExtractionMode.Complement, existing, token);
                    var bestFromStrategies = _strategyRuns
                        .OrderByDescending(r => r.Confidence)
                        .Select(r => r.Fields)
                        .FirstOrDefault(f => f is not null);
                    _mergeResult = await FieldMergeStrategy.MergeAsync(existing, bestFromStrategies, token);
                    break;

                default:
                    extractionResult = await AdaptiveExtractor.ExtractAsync(_docxText, ExtractionMode.BestStrategy, null, token);
                    _mergeResult = extractionResult is null
                        ? new MergeResult()
                        : new MergeResult { MergedFields = extractionResult, SourceCount = 1 };
                    break;
            }

            if (extractionResult is not null)
            {
                _result = extractionResult;
                _hasResult = true;
                _additionalFields = BuildAdditionalFields(_result);
            }

            Snackbar.Add("Adaptive extraction completed", Severity.Success);
        }
        catch (OperationCanceledException)
        {
            _error = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Adaptive extraction failed");
            _error = "Adaptive extraction failed. Check logs for details.";
        }
        finally
        {
            _running = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<IReadOnlyList<StrategyRunResult>> RunStrategiesAsync(string docxText, CancellationToken cancellationToken)
    {
        var runs = new List<StrategyRunResult>();

        foreach (var strategy in Strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var confidence = await strategy.GetConfidenceAsync(docxText, cancellationToken);
            ExtractedFields? fields = null;

            if (confidence > 0)
            {
                fields = await strategy.ExtractAsync(docxText, cancellationToken);
            }

            runs.Add(new StrategyRunResult(strategy.StrategyName, confidence, fields ?? new ExtractedFields(), fields is not null));
        }

        return runs;
    }

    private ExtractedFields? BuildExistingFields()
    {
        if (string.IsNullOrWhiteSpace(_existingExpediente) &&
            string.IsNullOrWhiteSpace(_existingCausa) &&
            string.IsNullOrWhiteSpace(_existingAccion))
        {
            return null;
        }

        return new ExtractedFields
        {
            Expediente = string.IsNullOrWhiteSpace(_existingExpediente) ? null : _existingExpediente.Trim(),
            Causa = string.IsNullOrWhiteSpace(_existingCausa) ? null : _existingCausa.Trim(),
            AccionSolicitada = string.IsNullOrWhiteSpace(_existingAccion) ? null : _existingAccion.Trim()
        };
    }

    private Color ConfidenceColor(int value) =>
        value switch
        {
            >= 80 => Color.Success,
            >= 60 => Color.Info,
            >= 40 => Color.Warning,
            _ => Color.Error
        };

    private string FormatSummary(ExtractedFields? fields)
    {
        if (fields is null)
        {
            return "—";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(fields.Causa))
        {
            parts.Add($"Causa: {fields.Causa}");
        }

        if (!string.IsNullOrWhiteSpace(fields.AccionSolicitada))
        {
            parts.Add($"Acción: {fields.AccionSolicitada}");
        }

        if (fields.AdditionalFields.Count > 0)
        {
            parts.Add($"{fields.AdditionalFields.Count} additional");
        }

        if (fields.Montos.Count > 0)
        {
            parts.Add($"{fields.Montos.Count} monto(s)");
        }

        return parts.Count == 0 ? "No data" : string.Join(" · ", parts);
    }

    private sealed record StrategyRunResult(string Name, int Confidence, ExtractedFields Fields, bool HasExtraction);

    private static List<KeyValuePair<string, string>> BuildAdditionalFields(ExtractedFields fields)
    {
        return fields.AdditionalFields
            .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value ?? "—"))
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}