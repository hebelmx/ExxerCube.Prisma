using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// <see cref="SiaraCredentialSourceContract"/> (ADR-005 §6, ADR-010 P5).
/// </summary>
/// <remarks>
/// The configuration is the <strong>executable design specification</strong> for
/// <see cref="ISiaraCredentialSource"/>: a pre-cancelled token yields a cancelled result; otherwise each
/// read returns a fresh, non-empty, self-clearing <see cref="SiaraCredential"/>. The fuller stateful
/// reference lives in <see cref="FakeSiaraCredentialSource"/>.
/// </remarks>
public static class SiaraCredentialSourceMockFactory
{
    /// <summary>
    /// Creates an <see cref="ISiaraCredentialSource"/> mock that satisfies every test in
    /// <see cref="SiaraCredentialSourceContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static ISiaraCredentialSource CreateContractConformingMock()
    {
        var mock = Substitute.For<ISiaraCredentialSource>();

        mock.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(0).IsCancellationRequested
                ? ResultExtensions.Cancelled<SiaraCredential>()
                : Result<SiaraCredential>.Success(
                    new SiaraCredential("mock-user".ToCharArray(), "mock-password".ToCharArray())));

        return mock;
    }
}
