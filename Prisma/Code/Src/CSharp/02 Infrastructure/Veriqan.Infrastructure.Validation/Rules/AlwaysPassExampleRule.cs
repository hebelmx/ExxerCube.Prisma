using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// SCAFFOLDING -- example rule used by Story 4.1 to prove DI assembly-scan discovery.
/// Always emits a <see cref="Domain.Enums.FindingVerdict.Pass"/> finding.
/// Stories 4.2+ will add real arithmetic rules; this class may be kept as a smoke-test
/// or removed once real rules are in place.
/// </summary>
internal sealed class AlwaysPassExampleRule : IVecValidationRule
{
    private const string Version = "1.0.0-example";

    /// <inheritdoc />
    public string CheckId => "CL-EXAMPLE-PASS";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: "example-always-pass"));
    }
}
