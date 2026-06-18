using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tolerances;

/// <summary>
/// In-code implementation of <see cref="ILegalToleranceProvider"/> that holds the
/// legally-mandated <see cref="Tolerance"/> spec for every computation rule that uses
/// a tolerance band.
/// </summary>
/// <remarks>
/// <para>
/// <b>Range rationale (documented per field):</b>
/// </para>
/// <para>
/// <b>CurrencyToleranceMxn rules (CL-10, CL-17, CL-18, CL-19, CL-20, CL-21, CL-22, CL-24,
/// CL-25, CL-44, ITEM-58):</b>
/// Legal default 0.50 MXN (standard Mexican peso rounding unit). Min = 0.00 (a tenant may
/// set zero tolerance for tighter audits). Max = 1.00 MXN — beyond one peso the rounding
/// argument no longer holds under CONDUSEF Acuerdo §7/§13/§22 and the tolerance would be
/// economically material. Range [0.00, 1.00] is a JUDGEMENT CALL for the ceiling; flagged
/// below as estimated.
/// </para>
/// <para>
/// <b>CL-10 (CAT, percentage-point space):</b>
/// Same MXN-equivalent field drives the tolerance but the comparison occurs in percentage-point
/// space. The spec here still stores MXN; the rule multiplies by 100 before comparison (ADR-V3
/// owner ruling). Same range [0.00, 1.00] applies — ⚠️ ESTIMATED ceiling.
/// </para>
/// <para>
/// <b>PointsTolerance rules (CL-39):</b>
/// Legal default 1.00 point. Min = 0.00 (strictest possible). Max = 2.00 points — beyond
/// two points a single-point rounding argument cannot justify the deviation.
/// ⚠️ ESTIMATED ceiling.
/// </para>
/// <para>
/// <b>PointsToPesosExchangeRate (CL-37):</b>
/// Legal default 0.10 (10 pts = $1 MXN, standard Banamex Puntos rate). The exchange rate is
/// a published contractual constant, so the permitted override range is narrow:
/// [0.05, 0.20]. A rate below 0.05 or above 0.20 is implausible for any current product.
/// ⚠️ ESTIMATED range.
/// </para>
/// <para>
/// <b>RewardsPesosToleranceMxn (future rewards rules):</b>
/// Legal default 1.00 MXN. Range [0.00, 2.00] — broader than currency because
/// rewards-pesos conversion introduces two rounding steps. ⚠️ ESTIMATED ceiling.
/// </para>
/// <para>
/// All ⚠️-marked ranges are engineering estimates derived from the rounding math.
/// They should be confirmed by a legal/compliance review and updated in story 9.3
/// when the encrypted-store migration occurs.
/// </para>
/// </remarks>
internal sealed class DefaultLegalToleranceProvider : ILegalToleranceProvider
{
    // -----------------------------------------------------------------------
    // Legal defaults (from the law's rounding math — Acuerdo §7/§13/§18/§22)
    // -----------------------------------------------------------------------

    /// <summary>Standard MXN currency rounding tolerance (Acuerdo §7/§13/§22).</summary>
    private static readonly Tolerance CurrencyMxn = new(
        legalDefault: 0.50m,
        min: 0.00m,
        max: 1.00m); // ⚠️ ceiling estimated

    /// <summary>
    /// Points rounding tolerance for rewards-point balances (Acuerdo §18).
    /// ⚠️ Ceiling estimated.
    /// </summary>
    private static readonly Tolerance Points = new(
        legalDefault: 1.00m,
        min: 0.00m,
        max: 2.00m); // ⚠️ ceiling estimated

    /// <summary>
    /// Points-to-pesos exchange-rate contractual constant tolerance (Acuerdo §18).
    /// ⚠️ Full range estimated.
    /// </summary>
    private static readonly Tolerance ExchangeRate = new(
        legalDefault: 0.10m,
        min: 0.05m,
        max: 0.20m); // ⚠️ range estimated

    /// <summary>
    /// Rewards-pesos tolerance, wider than plain-currency due to double-conversion rounding
    /// (Acuerdo §18). ⚠️ Ceiling estimated.
    /// </summary>
    private static readonly Tolerance RewardsPesosMxn = new(
        legalDefault: 1.00m,
        min: 0.00m,
        max: 2.00m); // ⚠️ ceiling estimated

    // -----------------------------------------------------------------------
    // Registry: checkId → Tolerance spec
    // -----------------------------------------------------------------------

    private static readonly Dictionary<string, Tolerance> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // CurrencyToleranceMxn rules
            ["CL-10"]     = CurrencyMxn,  // CAT % comparison (ADR-V3: value in pct-pt space)
            ["CL-17"]     = CurrencyMxn,  // Adeudo periodo anterior cross-period check
            ["CL-18"]     = CurrencyMxn,  // Cargos regulares vs DESGLOSE sum
            ["CL-19"]     = CurrencyMxn,  // Cargos a meses capital vs DESGLOSE sum
            ["CL-20"]     = CurrencyMxn,  // Pagos y abonos vs DESGLOSE credits sum
            ["CL-21"]     = CurrencyMxn,  // Pago para no generar intereses formula
            ["CL-22"]     = CurrencyMxn,  // Saldo cargos regulares == PagoParaNoGenerarIntereses
            ["CL-24"]     = CurrencyMxn,  // Saldo deudor total = regulares + meses
            ["CL-25"]     = CurrencyMxn,  // Crédito disponible = creditLine − saldo
            ["CL-44"]     = CurrencyMxn,  // DESGLOSE footer total cargos + abonos
            ["ITEM-58"]   = CurrencyMxn,  // Per-transaction amount match

            // PointsTolerance rules
            ["CL-39"]     = Points,       // Saldo total puntos balance formula

            // Exchange-rate rules
            ["CL-37"]     = ExchangeRate, // Tipo de cambio rewards points→pesos

            // RewardsPesosToleranceMxn rules (future; registered for completeness)
            ["CL-36"]     = RewardsPesosMxn, // Saldo inicial rewards pesos
        };

    /// <inheritdoc />
    public Tolerance For(string checkId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);

        if (Registry.TryGetValue(checkId, out var tolerance))
            return tolerance;

        throw new ArgumentException(
            $"No legal tolerance specification is registered for check '{checkId}'.",
            nameof(checkId));
    }

    /// <inheritdoc />
    public bool Has(string checkId) =>
        !string.IsNullOrWhiteSpace(checkId) && Registry.ContainsKey(checkId);
}
