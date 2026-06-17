using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// SCAFFOLDING -- example rule used by Story 4.1 to prove the InsufficientData path
/// when a required <see cref="ReferenceCapability"/> is absent from the bundle.
/// When <see cref="ReferenceCapability.Rate"/> (TASA) is available this rule emits Pass;
/// when it is absent it emits InsufficientData (FR-20).
/// Stories 4.2+ will replace or augment this with the real rate-comparison logic.
/// </summary>
internal sealed class RateCapabilityExampleRule : IVecValidationRule
{
    private const string Version = "1.0.0-example";

    /// <inheritdoc />
    public string CheckId => "CL-EXAMPLE-RATE-CAP";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // FR-20: if the Rate capability is absent, emit InsufficientData rather than a false verdict
        if (ctx.Availability.IsInsufficientData(ReferenceCapability.Rate))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: $"Capability {nameof(ReferenceCapability.Rate)} is not available in the bundle."));
        }

        // Capability is present; return Pass (real check logic belongs in Story 4.2)
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: "rate-section-present"));
    }
}
