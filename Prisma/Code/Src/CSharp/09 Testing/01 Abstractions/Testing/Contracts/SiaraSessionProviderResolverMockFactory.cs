using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// <see cref="SiaraSessionProviderResolverContract"/> (ADR-005 §6, ADR-010).
/// </summary>
/// <remarks>
/// The mock resolves to a contract-conforming <see cref="ISiaraSessionProvider"/> for the requested mode
/// (built via <see cref="SiaraSessionProviderMockFactory"/>), encoding the rule that a resolver returns the
/// provider matching the configured mode.
/// </remarks>
public static class SiaraSessionProviderResolverMockFactory
{
    /// <summary>
    /// Creates an <see cref="ISiaraSessionProviderResolver"/> mock that resolves to a provider of the
    /// given mode.
    /// </summary>
    /// <param name="mode">The mode the resolver should resolve. Defaults to
    /// <see cref="SiaraAuthMode.SessionPassthrough"/>.</param>
    /// <returns>The configured mock.</returns>
    public static ISiaraSessionProviderResolver CreateContractConformingMock(
        SiaraAuthMode mode = SiaraAuthMode.SessionPassthrough)
    {
        var provider = SiaraSessionProviderMockFactory.CreateContractConformingMock(mode);
        var mock = Substitute.For<ISiaraSessionProviderResolver>();
        mock.Resolve().Returns(Result<ISiaraSessionProvider>.Success(provider));
        return mock;
    }
}
