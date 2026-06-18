using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§18-COMPLETE: Verifies that, when a benefit/rewards program section is present in the
/// statement, ALL nine mandated concepts are shown — including explicit "0" values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo relativo al formato de estado de cuenta estandarizado
/// de tarjeta de crédito para personas físicas</i> (DOF 29-Dec-2022, mandatory since 17-Oct-2024),
/// §18 <i>Programas de beneficios</i>. The section is conditional: it is only required when the
/// product has a benefit/rewards program and the program section is detected in the statement.
/// </para>
/// <para>
/// <b>Mandated concepts (CL-38 list, §18):</b>
/// <list type="bullet">
///   <item>Saldo inicial</item>
///   <item>Generados</item>
///   <item>Redimidos / Utilizados</item>
///   <item>Vencidos</item>
///   <item>Por vencer</item>
///   <item>Saldo final</item>
///   <item>Unidad (label for the unit of the program, e.g. "puntos")</item>
///   <item>Equivalencia en pesos</item>
///   <item>Datos de contacto / Contacto</item>
/// </list>
/// Each concept is detected by a label-presence search in <see cref="StatementModel.NormalizedFullText"/>.
/// The check looks for the LABEL, not a non-zero value; a "0" balance still satisfies the requirement
/// because the regulation requires disclosure even when the balance is zero (CL-38).
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — the law fixes which
/// concepts must appear; a tenant profile may NOT relax this rule.
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — NEVER false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="StatementModel.Sections"/> is empty (detection pass did not run).</item>
///   <item>§18 section is not applicable (product has no rewards/benefit program and trigger absent).</item>
///   <item>§18 section is applicable but not present (absent from PDF) — not-applicable result to avoid
///     double-penalizing with <c>MandatorySectionsPresenceRule</c>.</item>
///   <item><see cref="DetectedSection.SectionText"/> for §18 is empty — section-scoped text not available
///     (predates R1 section-slicing or synthetic fixture without scoped text).</item>
///   <item>§18 <see cref="DetectedSection.DetectionStatus"/> is <see cref="SectionDetectionStatus.Indeterminate"/>
///     — section has no reliable text anchor; the rule must not Fail.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section18BenefitProgramCompletenessRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // -----------------------------------------------------------------------
    // Mandated concept label constants — normalized search strings.
    // These are the canonical Spanish labels required by §18 of the Acuerdo.
    // Normalization is applied at runtime; stored as-is for readability.
    // -----------------------------------------------------------------------

    /// <summary>Opening balance label: "Saldo inicial".</summary>
    internal const string ConceptSaldoInicial = "SALDO INICIAL";

    /// <summary>Earned/credited units label: "Generados".</summary>
    internal const string ConceptGenerados = "GENERADOS";

    /// <summary>Redeemed/used units label: "Redimidos" (also "Utilizados").</summary>
    internal const string ConceptRedimidos = "REDIMIDOS";

    /// <summary>Expired units label: "Vencidos".</summary>
    internal const string ConceptVencidos = "VENCIDOS";

    /// <summary>About to expire label: "Por vencer".</summary>
    internal const string ConceptPorVencer = "POR VENCER";

    /// <summary>Closing balance label: "Saldo final".</summary>
    internal const string ConceptSaldoFinal = "SALDO FINAL";

    /// <summary>Unit of the program (e.g. "puntos", "millas") — label: "Unidad".</summary>
    internal const string ConceptUnidad = "UNIDAD";

    /// <summary>Peso-equivalent label: "Equivalencia en pesos".</summary>
    internal const string ConceptEquivalenciaEnPesos = "EQUIVALENCIA EN PESOS";

    /// <summary>Contact information label: "Contacto" (also "Datos de contacto").</summary>
    internal const string ConceptContacto = "CONTACTO";

    /// <summary>
    /// All nine mandated concept labels, in canonical order for reporting.
    /// </summary>
    private static readonly IReadOnlyList<string> MandatedConcepts =
    [
        ConceptSaldoInicial,
        ConceptGenerados,
        ConceptRedimidos,
        ConceptVencidos,
        ConceptPorVencer,
        ConceptSaldoFinal,
        ConceptUnidad,
        ConceptEquivalenciaEnPesos,
        ConceptContacto,
    ];

    private const int Section18Number = 18;

    /// <inheritdoc />
    public string CheckId => "LAW-§18-COMPLETE";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §18";

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
            return InsufficientData("StatementModel is not populated; section-detection pass did not run.");

        // Guard: Sections must be populated (non-empty = detection pass ran).
        var sections = model.Sections;
        if (sections.Count == 0)
            return InsufficientData(
                "Sections list is empty — either the extraction predates Story 10.1 " +
                "or the PDF has no text layer.");

        // Locate §18 in the sections map.
        var section18 = sections.FirstOrDefault(s => s.SectionNumber == Section18Number);

        // If §18 is not applicable (conditional trigger absent) → not-applicable, never Fail.
        if (section18 is null || !section18.IsApplicable)
            return InsufficientData(
                "§18 is not applicable: product has no benefit program or the conditional trigger is absent.");

        // If §18 is applicable but not present → abstain (MandatorySectionsPresenceRule already flags it).
        if (!section18.IsPresent)
            return InsufficientData(
                "§18 is applicable but not present in the PDF; " +
                "MandatorySectionsPresenceRule covers the absence finding.");

        // If §18 is Indeterminate (no text anchor), abstain — do NOT Fail.
        if (section18.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§18 detection status is Indeterminate — section has no reliable text anchor; " +
                "cannot scope concept-label check.");

        // Prefer the section-scoped text (heading → next heading) to avoid false-Pass from
        // labels present in OTHER sections (e.g. SALDO INICIAL in §7, CONTACTO in §24).
        // Fall back to NormalizedFullText only when SectionText is empty (synthetic fixtures
        // where section-slicing did not run) — but in that case we cannot guarantee accuracy.
        var sectionText = section18.SectionText;
        if (string.IsNullOrWhiteSpace(sectionText))
        {
            // SectionText is empty: either an old extraction that predates R1 section-slicing,
            // or a synthetic fixture. Abstain to avoid a false scope.
            return InsufficientData(
                "§18 SectionText is empty — section-scoped text not available; " +
                "cannot reliably check §18 concepts without risking false-Pass from other sections.");
        }

        // Check each mandated concept label within §18's own section text.
        // SectionText is already upper-cased and accent-stripped (VecTextNormalizer contract).
        // The concept constants are already in normalized form (upper-case, no accents).
        // "0" values count as present: we look for the LABEL, not a non-zero value.
        var missing = MandatedConcepts
            .Where(concept => !sectionText.Contains(concept, StringComparison.Ordinal))
            .ToList();

        if (missing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"all {MandatedConcepts.Count} mandated §18 concepts present"));
        }

        // Fail — name every missing concept label.
        var missingList = string.Join(", ", missing);
        var allList = string.Join(", ", MandatedConcepts);

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: allList,
                observed: $"missing: {missingList}"));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
