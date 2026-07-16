using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Looks up the <see cref="FieldEscalationLadder"/> for a given <see cref="FieldKind"/>.
/// </summary>
public interface IFieldEscalationLadderRegistry
{
    /// <summary>
    /// Returns the escalation ladder for <paramref name="fieldKind"/>.
    /// </summary>
    /// <param name="fieldKind">The field to look up.</param>
    /// <returns>
    /// The registered ladder, or <see cref="FieldEscalationLadder.PositionalOnly"/> when no
    /// explicit ladder has been registered for the field.
    /// </returns>
    FieldEscalationLadder GetLadder(FieldKind fieldKind);
}

/// <summary>
/// Default <see cref="IFieldEscalationLadderRegistry"/> — a per-<see cref="FieldKind"/> lookup
/// table built once at construction. As of E1 every field resolved to
/// <see cref="FieldEscalationLadder.PositionalOnly"/>: no <see cref="FieldKind"/> had a rung
/// beyond stage 1, so nothing ever escalated (design doc program plan — E1 is behavior-neutral by
/// construction). E2.2 gives <see cref="FieldKind.PaymentDueDate"/> the first non-empty ladder;
/// every other field remains <see cref="FieldEscalationLadder.PositionalOnly"/>.
/// </summary>
/// <remarks>
/// Later epics (E2.3+) grow this table further — e.g. giving TASA/CAT a similar fuzzy ladder — by
/// adding entries in <see cref="BuildDefaultLadders"/> (or by constructing the registry with an
/// explicit table via the internal constructor used by tests). That is a data change to this one
/// method, not new stage or orchestrator code.
/// </remarks>
public sealed class FieldEscalationLadderRegistry : IFieldEscalationLadderRegistry
{
    private readonly IReadOnlyDictionary<FieldKind, FieldEscalationLadder> _ladders;

    /// <summary>
    /// Initializes the registry with the default, all-positional-only ladder table.
    /// </summary>
    public FieldEscalationLadderRegistry()
        : this(BuildDefaultLadders())
    {
    }

    /// <summary>
    /// Initializes the registry with an explicit ladder table. Exposed for future epics/tests
    /// that need to exercise a non-default ladder without changing the production default.
    /// </summary>
    /// <param name="ladders">
    /// Explicit per-<see cref="FieldKind"/> ladder table. A <see cref="FieldKind"/> absent from
    /// this dictionary falls back to <see cref="FieldEscalationLadder.PositionalOnly"/>.
    /// </param>
    internal FieldEscalationLadderRegistry(IReadOnlyDictionary<FieldKind, FieldEscalationLadder> ladders)
    {
        _ladders = ladders ?? throw new ArgumentNullException(nameof(ladders));
    }

    /// <inheritdoc/>
    public FieldEscalationLadder GetLadder(FieldKind fieldKind) =>
        _ladders.TryGetValue(fieldKind, out var ladder)
            ? ladder
            : FieldEscalationLadder.PositionalOnly(fieldKind);

    private static IReadOnlyDictionary<FieldKind, FieldEscalationLadder> BuildDefaultLadders()
    {
        var allFieldKinds = Enum.GetValues<FieldKind>();
        var table = new Dictionary<FieldKind, FieldEscalationLadder>(allFieldKinds.Length);
        foreach (var fieldKind in allFieldKinds)
            table[fieldKind] = FieldEscalationLadder.PositionalOnly(fieldKind);

        // E2.2: the first non-empty ladder. PaymentDueDate escalates to the fuzzy label-anchor
        // stage (StageId.FuzzyLabel) ONLY when the positional extractor found nothing at all
        // (StatusGate) — the safe, additive case; a positionally-found value is never
        // second-guessed by this rung. A recovered value must independently clear
        // PaymentDueDatePlausibilityValidator (belt-and-suspenders alongside the stage's own
        // plausibility gate).
        table[FieldKind.PaymentDueDate] = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.StatusGate)],
            Validator: new PaymentDueDatePlausibilityValidator());

        // E7.S7.2/S7.3: Product escalates to the header-image OCR stage (StageId.HeaderImageOcr)
        // ONLY when the positional extractor found nothing at all (StatusGate) — mirrors the
        // PaymentDueDate pattern above. As of this slice, PdfPigStatementFieldExtractor's
        // card-number-band fallback is neutered to abstain (Missing) instead of returning the
        // digit band, so this rung is what actually recovers the real product name via OCR.
        table[FieldKind.Product] = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.HeaderImageOcr, EscalationTrigger.StatusGate)]);

        // B2 — plausibility validators on empty ladders; armed via the positional-short-circuit
        // guard, no recovery rung (no recoverable second source, spike-refuted). Tasa/Cat and the
        // 11 RESUMEN/NIVEL/DESGLOSE money fields each get a range/magnitude validator so a GROSS
        // misread downgrades to Missing/InsufficientData instead of silently gating a compliance
        // verdict on garbage.
        table[FieldKind.Tasa] = new FieldEscalationLadder(
            FieldKind.Tasa, ConfidenceFloor: 0.0, Rungs: [], Validator: new TasaPlausibilityValidator());

        table[FieldKind.Cat] = new FieldEscalationLadder(
            FieldKind.Cat, ConfidenceFloor: 0.0, Rungs: [], Validator: new CatPlausibilityValidator());

        FieldKind[] moneyFields =
        [
            FieldKind.AdeudoPeriodoAnterior,
            FieldKind.CargosRegularesNoMeses,
            FieldKind.CargosComprasAMesesCapital,
            FieldKind.MontoIntereses,
            FieldKind.MontoComisiones,
            FieldKind.IvaInteresesYComisiones,
            FieldKind.PagosYAbonos,
            FieldKind.SaldoCargosRegulares,
            FieldKind.SaldoCargosAMeses,
            FieldKind.TotalCargos,
            FieldKind.TotalAbonos,
        ];
        foreach (var moneyField in moneyFields)
        {
            table[moneyField] = new FieldEscalationLadder(
                moneyField, ConfidenceFloor: 0.0, Rungs: [], Validator: new MoneyMagnitudeValidator());
        }

        return table;
    }
}
