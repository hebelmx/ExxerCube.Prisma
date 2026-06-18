using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§16-OTRASLINEAS: Validates per-row arithmetic for §16
/// "Información de otras líneas de crédito" — a conditional section that is ONLY
/// present when the cardholder has active other credit lines on the same statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §16 mandates that
/// when an account has other credit lines, each row must carry arithmetically consistent
/// figures: IVA ≈ Interés × 0.16 (Mexican IVA rate), and interest is consistent with
/// the reported rate/days when those cells are present.
/// </para>
/// <para>
/// <b>Conditional-section behaviour (cardinal rule — preventive gate):</b>
/// <list type="bullet">
///   <item>
///     §16 <see cref="TableExtractionStatus.SectionNotFound"/> → <b>Pass</b> with an
///     "N/A — section not applicable" note.  The section is legitimately absent
///     when the cardholder has no other credit lines; this is the normal path for
///     all current fixtures.  Never Fail for a legitimately absent conditional section.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Indeterminate"/> or
///     <see cref="TableExtractionStatus.NoRowsParsed"/> → <b>InsufficientData</b>.
///     The section was detected but could not be reliably read.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Extracted"/> → <b>InsufficientData</b>.
///     Per-column semantics, interest/rate/días, and totals-tie checks are corpus-gated:
///     no §16 fixture exists to validate column mapping. Running arithmetic on unverified
///     columns risks a false Fail that halts the billing run. The rule abstains until a
///     validated corpus is available.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Aggregate verdict when §16 is Extracted:</b>
/// <list type="bullet">
///   <item>InsufficientData — corpus-gated abstain (current behaviour).</item>
/// </list>
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing
/// or low-confidence cell — never guess, never false-Fail.
/// </para>
/// </remarks>
internal sealed class Section16OtherCreditLinesRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section16Number = 16;

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section16OtherCreditLinesRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section16OtherCreditLinesRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§16-OTRASLINEAS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §16";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Guard: tolerance must be registered before attempting any resolution.
        if (!_toleranceProvider.Has(CheckId))
            return InsufficientData($"No legal tolerance registered for {CheckId} — cannot evaluate.");

        // Guard: StatementModel must be present.
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Locate the §16 FinancialTable.
        var table16 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section16Number);

        // ----------------------------------------------------------------
        // Conditional-section gate:
        // SectionNotFound means the cardholder has no other credit lines —
        // this is NOT a defect. Return Pass with N/A note (matches E10
        // conditional-rule pattern for §23, §25 when trigger is absent).
        // ----------------------------------------------------------------
        if (table16 is null || table16.Status == TableExtractionStatus.SectionNotFound)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§16 Información de otras líneas de crédito is not applicable — " +
                              "section not found (cardholder has no other credit lines on this statement). " +
                              "N/A — conditional section legitimately absent."));
        }

        // Section was detected but extraction was not reliable.
        if (table16.Status != TableExtractionStatus.Extracted)
        {
            return InsufficientData(
                $"§16 table extraction status is {table16.Status} — not Extracted; " +
                "cannot verify per-row arithmetic.");
        }

        // §16 is Extracted (section is present in the document).
        // Per-column semantics + interest/rate/días + totals-tie checks require a
        // validated §16 corpus to map columns correctly — no such corpus exists yet.
        // Running arithmetic on unverified column mappings risks a false Fail that
        // halts the billing run. Abstain until corpus-gated validation is available.
        return InsufficientData(
            "§16 present but per-column semantics + interest/rate/días + totals-tie checks " +
            "are corpus-gated (no §16 fixture available to validate column mapping). " +
            "Abstaining rather than compute on unverified columns.");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
