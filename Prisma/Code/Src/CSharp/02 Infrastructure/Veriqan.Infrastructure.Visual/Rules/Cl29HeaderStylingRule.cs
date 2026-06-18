using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-29: Verifies that every detected section header in the statement PDF is
/// both <b>bold</b> and <b>uppercase</b>, as required by the VEC typographic standard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure font-name
/// inspection and character-case analysis on data extracted by PdfPig; no ML required.
/// </para>
/// <para>
/// <b>Header detection:</b> the extractor matches known Spanish section-title strings
/// (e.g. "RESUMEN DE CARGOS Y ABONOS DEL PERIODO") against each page's word bands.
/// Bold is detected when the first letter glyph of the header band uses a font whose
/// name contains <c>"Bold"</c>. Uppercase is detected when every alphabetic character
/// in the printed header text is uppercase.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction did not run).</item>
///   <item><see cref="StatementModel.SectionHeaderStyles"/> is empty (no known section
///         titles were found — the rule cannot confirm compliance).</item>
/// </list>
/// </para>
/// <para>
/// <b>Pass:</b> every detected header has <c>IsBold == true</c> AND
/// <c>IsUppercase == true</c>.
/// </para>
/// <para>
/// <b>Fail:</b> at least one header is not bold or not uppercase — the finding
/// names the offending header text and which attribute failed.
/// </para>
/// </remarks>
internal sealed class Cl29HeaderStylingRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-29";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — encabezados de sección en negritas y mayúsculas";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;

        if (model is null)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No statement model available — extraction stage did not run."));
        }

        var headers = model.SectionHeaderStyles;

        if (headers.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No known section headers were detected in the PDF — cannot confirm styling compliance."));
        }

        // Find the first non-compliant header.
        var offender = headers.FirstOrDefault(h => !h.IsBold || !h.IsUppercase);

        if (offender is null)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {headers.Count} detected section header(s) are bold and uppercase."));
        }

        // Build a precise failure message describing which attribute(s) failed.
        var failedAttributes = BuildFailedAttributeList(offender);
        var totalViolations = headers.Count(h => !h.IsBold || !h.IsUppercase);

        var detail = totalViolations > 1
            ? $"Header '{offender.HeaderText}' (page {offender.PageNumber}) is not {failedAttributes}. {totalViolations} non-compliant header(s) total."
            : $"Header '{offender.HeaderText}' (page {offender.PageNumber}) is not {failedAttributes}.";

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "All section headers must be bold and uppercase",
                observed: detail,
                locator: offender.Locator));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Produces a human-readable list of the attributes that failed for a header
    /// (e.g. <c>"bold"</c>, <c>"uppercase"</c>, or <c>"bold or uppercase"</c>).
    /// </summary>
    private static string BuildFailedAttributeList(SectionHeaderStyle header)
    {
        if (!header.IsBold && !header.IsUppercase)
            return "bold or uppercase";
        if (!header.IsBold)
            return "bold";
        return "uppercase";
    }
}
