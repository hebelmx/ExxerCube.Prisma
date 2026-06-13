using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Testing.Contracts;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="ProcessClearanceTokenServiceContract"/> for the production
/// <see cref="JwtProcessClearanceTokenService"/> (ADR-005, MVP-PATH 1.5 A5 DoD).
/// </summary>
/// <remarks>
/// The SUT is the real JWT service configured with a test signing secret. This proves the real JWT
/// implementation satisfies the same behavioral contract as the reference
/// <see cref="ExxerCube.Prisma.Testing.Contracts.FakeProcessClearanceTokenService"/>. JWT internals
/// (signing algorithm, issuer, audience) are covered by the dedicated
/// <see cref="JwtProcessClearanceTokenServiceTests"/> class.
/// </remarks>
public sealed class JwtProcessClearanceTokenServiceContractTests : ProcessClearanceTokenServiceContract
{
    private const string TestSecret = "test-secret-at-least-32-chars-long-for-hmac-sha256";

    /// <summary>
    /// Initializes the implementation instance with the real JWT service over a test configuration.
    /// </summary>
    public JwtProcessClearanceTokenServiceContractTests()
        : base(new JwtProcessClearanceTokenService(
            Options.Create(new ProcessIdentityOptions
            {
                JwtSecret = TestSecret,
                JwtIssuer = "prisma-pipeline-test",
                JwtAudience = "prisma-pipeline-test",
                TokenLifetime = TimeSpan.FromMinutes(5),
            }),
            Substitute.For<ILogger<JwtProcessClearanceTokenService>>()))
    {
    }
}
