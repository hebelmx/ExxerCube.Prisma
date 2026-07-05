using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Production <see cref="IFieldStageProvider"/>: supplies the real higher-stage implementations
/// registered for each <see cref="FieldKind"/> (VERIQAN-E2+).
/// </summary>
/// <remarks>
/// As of this chunk (E2.2) the only field with a registered higher stage is
/// <see cref="FieldKind.PaymentDueDate"/>, which gets a single
/// <see cref="FuzzyLabelStage{TValue}"/> over a small, phrasing-based set of Spanish label
/// aliases for "Fecha límite de pago". Every other field still returns an empty list — identical
/// to <see cref="EmptyFieldStageProvider"/> — until a later epic registers a stage for it.
/// </remarks>
public sealed class DefaultFieldStageProvider : IFieldStageProvider
{
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

        return Array.Empty<IFieldResolutionStage<TValue>>();
    }

    private static FuzzyLabelStage<DateOnly> CreatePaymentDueDateStage() =>
        new(
            PaymentDueDateAliases,
            ParsePaymentDueDateBand,
            PaymentDueDatePlausibilityValidator.IsPlausible);

    private static bool ParsePaymentDueDateBand(string bandText, out DateOnly value) =>
        StatementValueParsers.TryParseSpanishDate(bandText, out value);
}
