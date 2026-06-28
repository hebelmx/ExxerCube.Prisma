using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§23-ABONO-LINK: When §23 <i>Cargos no reconocidos</i> has concluded dispute rows,
/// verifies that each concluded row's disputed amount is also present as a line item in
/// the DESGLOSE DE MOVIMIENTOS DEL PERIODO section.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §23.f mandates that
/// when a disputed charge is concluded (<i>concluida-procedente</i> or
/// <i>concluida-improcedente</i>), the institution must reflect the resolved charge as
/// a movement line item in the DESGLOSE DE MOVIMIENTOS DEL PERIODO section, so the
/// client can cross-reference the resolution with the transaction table.
/// </para>
/// <para>
/// <b>Scope:</b> only <see cref="DisputeStatus.ConcluidaProcedente"/> and
/// <see cref="DisputeStatus.ConcluidaImprocedente"/> rows are checked.
/// <see cref="DisputeStatus.Pendiente"/> and <see cref="DisputeStatus.Unknown"/> rows are
/// excluded — pending disputes have not yet been resolved and therefore have no required
/// DESGLOSE entry.
/// </para>
/// <para>
/// <b>Amount matching:</b> the rule looks for any movement in
/// <see cref="StatementModel.Movements"/> whose <see cref="StatementMovement.Amount"/>
/// is within <see cref="AmountMatchTolerance"/> (2 centavos) of the disputed row's amount.
/// Sign is intentionally <em>not</em> checked — a resolved dispute may appear as either a
/// credit (procedente reversal) or a debit (improcedente re-assertion) depending on the
/// DESGLOSE layout.
/// </para>
/// <para>
/// <b>Abstain-safe guards (return InsufficientData, never false-Fail):</b>
/// <list type="bullet">
///   <item>Cancellation token cancelled → Cancelled.</item>
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="StatementModel.DisputeRowsStatus"/> ≠
///         <see cref="DisputeRowsExtractionStatus.Extracted"/> — structured rows unavailable.</item>
///   <item><see cref="StatementModel.MovementsStatus"/> ≠
///         <see cref="MovementsExtractionStatus.Extracted"/> — DESGLOSE not parsed.</item>
///   <item>§23 section is not present and not applicable in the detected-section list
///         (mirrors the existing LAW-§23-STATUS guard) → Pass (not applicable).</item>
/// </list>
/// </para>
/// <para>
/// <b>Verdict logic (when §23 is applicable and both extractions are Extracted):</b>
/// <list type="bullet">
///   <item>No concluded rows → Pass (nothing to link).</item>
///   <item>All concluded rows have a matching DESGLOSE amount → Pass.</item>
///   <item>Any concluded row lacks a matching DESGLOSE amount → Fail Critical, naming
///         the unmatched amounts.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section23AbonoLinkageRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// Maximum absolute difference (in MXN) between a concluded-dispute amount and a
    /// DESGLOSE movement amount for the two to be considered a match.
    /// Set to 0.02 (2 centavos) — tighter than the standard CurrencyMxn tolerance of 0.50
    /// because this is an identity linkage (same charge), not an arithmetic reconciliation.
    /// </summary>
    internal const decimal AmountMatchTolerance = 0.02m;

    /// <inheritdoc />
    public string CheckId => "LAW-§23-ABONO-LINK";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §23";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Guard: statement model must be present.
        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated; extraction pass did not run.");

        // Guard: check §23 applicability (mirrors Section23CargosNoReconocidosRule).
        // If Sections list is populated, confirm §23 is applicable.
        var sections = model.Sections;
        if (sections.Count > 0)
        {
            DetectedSection? section23 = null;
            foreach (var s in sections)
            {
                if (s.SectionNumber == 23)
                {
                    section23 = s;
                    break;
                }
            }

            if (section23 is not null && !section23.IsApplicable)
            {
                // §23 trigger condition absent → not applicable → Pass (consistent with LAW-§23-STATUS).
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Pass(
                        checkId: CheckId,
                        technique: Technique,
                        engineVersion: Version,
                        observed: "§23 Cargos no reconocidos is not applicable — trigger condition absent."));
            }
        }

        // Guard: dispute rows must have been successfully extracted.
        if (model.DisputeRowsStatus != DisputeRowsExtractionStatus.Extracted)
        {
            return InsufficientData(
                $"§23 dispute rows were not extracted (DisputeRowsStatus = {model.DisputeRowsStatus}); " +
                "linkage check requires structured rows.  " +
                "If §23 is absent from this statement this result is expected.");
        }

        // Guard: DESGLOSE movements must have been successfully extracted.
        if (model.MovementsStatus != MovementsExtractionStatus.Extracted)
        {
            return InsufficientData(
                $"DESGLOSE movements were not extracted (MovementsStatus = {model.MovementsStatus}); " +
                "cannot verify linkage without the transaction table.");
        }

        // Collect concluded rows (Pendiente and Unknown are excluded — not required to link).
        var concludedRows = new List<DisputeRow>(model.DisputeRows.Count);
        foreach (var row in model.DisputeRows)
        {
            if (row.IsConcluida)
                concludedRows.Add(row);
        }

        // No concluded rows → nothing to link → Pass.
        if (concludedRows.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"§23 has {model.DisputeRows.Count} dispute row(s) but none are concluded " +
                               "(all are Pendiente or Unknown); no linkage check required."));
        }

        // For each concluded row, check whether any DESGLOSE movement matches its amount.
        var unmatched = new List<decimal>(concludedRows.Count);
        var movements = model.Movements;

        foreach (var row in concludedRows)
        {
            var found = false;
            foreach (var mv in movements)
            {
                if (Math.Abs(mv.Amount - row.Amount) <= AmountMatchTolerance)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                unmatched.Add(row.Amount);
        }

        if (unmatched.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {concludedRows.Count} concluded §23 dispute row(s) have a matching " +
                               $"amount in the DESGLOSE (tolerance ±{AmountMatchTolerance:F2} MXN)."));
        }

        // One or more concluded rows have no matching DESGLOSE movement.
        var unmatchedList = string.Join(", ",
            unmatched.ConvertAll(a => a.ToString("F2", CultureInfo.InvariantCulture)));

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Each concluded §23 dispute amount to appear in the DESGLOSE " +
                           $"(tolerance ±{AmountMatchTolerance:F2} MXN).",
                observed: $"{unmatched.Count} concluded §23 dispute row(s) have no matching DESGLOSE " +
                           $"movement: unmatched amounts = [{unmatchedList}] MXN."));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
