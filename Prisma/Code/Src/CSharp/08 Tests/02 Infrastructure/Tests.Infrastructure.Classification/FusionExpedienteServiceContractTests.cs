namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Implementation instance of <see cref="FusionExpedienteContract"/> for
/// <see cref="FusionExpedienteService"/> (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// Phase 4 of the ITDD refactor split the previously-conflated real-SUT class: the interface-generic
/// behaviour (the full <c>FusionDecision</c> ladder + <c>NextAction</c> routing) moved into
/// <see cref="FusionExpedienteContract"/> and runs here through inheritance against the real
/// <see cref="FusionExpedienteService"/> built with default <c>FusionCoefficients</c>.
/// </para>
/// <para>
/// The behavioural threshold clauses are pinned to the implementation's actual coefficient values via
/// the property overrides below (auto-process 0.85, fuzzy 0.85, exact-match 0.80) — every other
/// assertion is behavioural and lives in the base, so no implementation-detail test remains here.
/// Mutation-pinning lives in <c>FusionExpedienteServiceMutationTests</c> (untouched).
/// </para>
/// </remarks>
public sealed class FusionExpedienteServiceContractTests(ITestOutputHelper output) : FusionExpedienteContract
{
    private readonly ITestOutputHelper _output = output;

    /// <inheritdoc />
    protected override IFusionExpediente CreateSut()
    {
        var logger = XUnitLogger.CreateLogger<FusionExpedienteService>(_output);
        var coefficients = new FusionCoefficients(); // Use default coefficients
        return new FusionExpedienteService(logger, coefficients);
    }

    /// <inheritdoc />
    protected override double AutoProcessThreshold => new FusionCoefficients().AutoProcessThreshold;

    /// <inheritdoc />
    protected override double FuzzyMatchThreshold => new FusionCoefficients().FuzzyMatchThreshold;

    /// <inheritdoc />
    protected override double MinExactMatchConfidence => 0.80;
}
