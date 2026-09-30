namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="ProcessClearanceTokenServiceContract"/> (ADR-005 §6): proves
/// the stateful <see cref="FakeProcessClearanceTokenService"/> honors the port contract, so it is a
/// trustworthy stand-in for forwarder and broadcaster tests that run without a real JWT signing key
/// (MVP-PATH 1.5, A5).
/// </summary>
public sealed class FakeProcessClearanceTokenServiceContractTests : ProcessClearanceTokenServiceContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeProcessClearanceTokenServiceContractTests()
        : base(new FakeProcessClearanceTokenService())
    {
    }
}
