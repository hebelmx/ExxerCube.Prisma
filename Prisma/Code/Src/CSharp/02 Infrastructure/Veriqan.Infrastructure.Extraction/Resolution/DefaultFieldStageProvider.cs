using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Production <see cref="IFieldStageProvider"/>: supplies the real higher-stage implementations
/// registered for each <see cref="FieldKind"/> (VERIQAN-E2+).
/// </summary>
/// <remarks>
/// As of E2.2, <see cref="FieldKind.PaymentDueDate"/> gets a single
/// <see cref="FuzzyLabelStage{TValue}"/> over a small, phrasing-based set of Spanish label
/// aliases for "Fecha límite de pago". As of E7.S7.2/S7.3, <see cref="FieldKind.Product"/> gets a
/// single <see cref="HeaderImageOcrStage"/> that reads the product name off the page-1 header
/// image via OCR when the positional extractor found nothing. Every other field still returns an
/// empty list — identical to <see cref="EmptyFieldStageProvider"/> — until a later epic registers
/// a stage for it.
/// </remarks>
public sealed class DefaultFieldStageProvider : IFieldStageProvider
{
    private readonly IHeaderProductOcrEngine _headerProductOcrEngine;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initializes a <see cref="DefaultFieldStageProvider"/>.</summary>
    /// <param name="headerProductOcrEngine">
    /// OCR engine used by the <see cref="FieldKind.Product"/> header-image OCR stage
    /// (E7.S7.2/S7.3, design doc §4.C.3).
    /// </param>
    /// <param name="loggerFactory">Used to construct per-stage loggers.</param>
    public DefaultFieldStageProvider(IHeaderProductOcrEngine headerProductOcrEngine, ILoggerFactory loggerFactory)
    {
        _headerProductOcrEngine = headerProductOcrEngine ?? throw new ArgumentNullException(nameof(headerProductOcrEngine));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }
    /// <summary>
    /// Accent-folded, lowercased label-phrasing aliases for the payment-due-date field. Kept
    /// intentionally small and phrasing-based — NOT calibrated to any one fixture's coordinates —
    /// per the design doc's "label-alias dictionary" fallback strategy (design doc: "PaymentDueDate:
    /// Positional→fuzzy/Levenshtein, STOP … CONDUSEF language is a bounded set").
    /// </summary>
    private static readonly IReadOnlyList<string> PaymentDueDateAliases =
    [
        "fecha limite de pago",
        "fecha de pago",
        "pagar antes del",
        "fecha limite pago",
    ];

    /// <inheritdoc/>
    public IReadOnlyList<IFieldResolutionStage<TValue>> GetHigherStages<TValue>(FieldKind fieldKind)
    {
        if (fieldKind == FieldKind.PaymentDueDate && typeof(TValue) == typeof(DateOnly))
        {
            IFieldResolutionStage<DateOnly>[] stages = [CreatePaymentDueDateStage()];

            // Safe: the typeof check above proves TValue and DateOnly are the same reified
            // runtime type (FieldKind.PaymentDueDate is only ever resolved as DateOnly in
            // production), so this reinterpret-cast cannot fail. It is required because
            // IFieldResolutionStage<T> is invariant (T appears in an input position on
            // TryResolveAsync), so the compiler cannot otherwise treat
            // IFieldResolutionStage<DateOnly> as assignable to IFieldResolutionStage<TValue> even
            // though, at runtime, they are the identical type.
            return (IReadOnlyList<IFieldResolutionStage<TValue>>)(object)stages;
        }

        if (fieldKind == FieldKind.Product && typeof(TValue) == typeof(string))
        {
            IFieldResolutionStage<string>[] stages = [CreateHeaderImageOcrStage()];

            // Safe for the same reason as the PaymentDueDate cast above: FieldKind.Product is
            // only ever resolved as string in production.
            return (IReadOnlyList<IFieldResolutionStage<TValue>>)(object)stages;
        }

        return Array.Empty<IFieldResolutionStage<TValue>>();
    }

    private static FuzzyLabelStage<DateOnly> CreatePaymentDueDateStage() =>
        new(
            PaymentDueDateAliases,
            ParsePaymentDueDateBand,
            PaymentDueDatePlausibilityValidator.IsPlausible);

    private static bool ParsePaymentDueDateBand(string bandText, out DateOnly value) =>
        StatementValueParsers.TryParseSpanishDate(bandText, out value);

    private HeaderImageOcrStage CreateHeaderImageOcrStage() =>
        new(_headerProductOcrEngine, _loggerFactory.CreateLogger<HeaderImageOcrStage>());
}
