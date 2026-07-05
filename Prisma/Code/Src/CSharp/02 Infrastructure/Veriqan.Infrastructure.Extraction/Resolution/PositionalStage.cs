using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// <see cref="IFieldResolutionStage{TValue}"/> adapter for stage 1 — the existing
/// <c>PdfPigStatementFieldExtractor</c>.
/// </summary>
/// <remarks>
/// <para>
/// The positional extractor already computed this field's <see cref="ExtractedField{T}"/> as part
/// of its single-pass <c>ExtractHeaderAsync</c>/<c>ExtractFullAsync</c> call — re-running per-field
/// positional logic would mean refactoring roughly 25 private extraction methods, which the design
/// explicitly keeps out of scope for E1 (design doc, risk 1). This adapter therefore does
/// <b>not</b> re-extract anything: it simply wraps the already-known field as a
/// <see cref="FieldCandidate{TValue}"/> so the orchestrator can treat stage 1 uniformly with every
/// higher stage (same interface, same provenance bookkeeping).
/// </para>
/// <para>
/// Ignores <see cref="FieldResolutionContext{TValue}"/> entirely (it needs neither the PDF bytes
/// nor the lazy corpus) — evidence that stage 1 alone never triggers the corpus parse.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The type of the field this instance wraps.</typeparam>
public sealed class PositionalStage<TValue> : IFieldResolutionStage<TValue>
{
    private readonly ExtractedField<TValue> _positionalField;

    /// <summary>
    /// Initializes a <see cref="PositionalStage{TValue}"/> wrapping the field value already
    /// produced by the positional extractor.
    /// </summary>
    /// <param name="positionalField">The field as extracted by stage 1.</param>
    public PositionalStage(ExtractedField<TValue> positionalField)
    {
        _positionalField = positionalField ?? throw new ArgumentNullException(nameof(positionalField));
    }

    /// <inheritdoc/>
    public StageId Stage => StageId.Positional;

    /// <inheritdoc/>
    /// <remarks>Never fails; never inspects <paramref name="context"/>; never awaits anything.</remarks>
    public Task<Result<FieldCandidate<TValue>>> TryResolveAsync(
        FieldResolutionContext<TValue> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<FieldCandidate<TValue>>());

        var candidate = _positionalField.Status == ExtractionStatus.NotExtracted
            ? FieldCandidate<TValue>.None(StageId.Positional, _positionalField.Locator)
            : FieldCandidate<TValue>.Found(
                _positionalField.Value!,
                _positionalField.Confidence,
                StageId.Positional,
                _positionalField.Locator,
                _positionalField.Status);

        return Task.FromResult(Result<FieldCandidate<TValue>>.WithSuccess(candidate));
    }
}
