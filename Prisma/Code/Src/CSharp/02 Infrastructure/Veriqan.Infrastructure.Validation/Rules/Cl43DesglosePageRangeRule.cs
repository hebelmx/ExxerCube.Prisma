using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-43: Per-page DESGLOSE period range confirmation (best-effort).
/// </summary>
/// <remarks>
/// <para>
/// The per-page period range data is NOT captured by the current extractor.
/// The DESGLOSE pages may print a range header ("Periodo: &lt;start&gt; – &lt;end&gt;")
/// at the top of each table page, but the extractor does not store this per-page data.
/// Story 4.4 page-level header data is not available in the extraction model;
/// therefore CL-43 always returns <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>.
/// </para>
/// <para>
/// When the extractor is extended to capture per-DESGLOSE-page stated ranges, this rule
/// can be updated to compare each page's stated range against PeriodStart/PeriodCutDate.
/// </para>
/// </remarks>
internal sealed class Cl43DesglosePageRangeRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-43";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §22";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "Per-page DESGLOSE period range is not captured by the extractor " +
                        "(Story 4.4 page-level header data not available); " +
                        "CL-43 cannot be evaluated deterministically."));
    }
}
