using ExxerCube.Prisma.Domain.Interfaces;
using Shouldly;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Shared contract tests that validate any <see cref="IPersonIdentityResolver"/> implementation
/// behaves consistently across adapters.
/// </summary>
/// <remarks>
/// These helpers are meant to be invoked from adapter-specific test suites to ensure a uniform
/// contract without duplicating assertions in every project.
/// </remarks>
public static class IPersonIdentityResolverContractTests
{
    /// <summary>
    /// Verifies <see cref="IPersonIdentityResolver.FindByRfcAsync(string,System.Threading.CancellationToken)"/>
    /// returns a successful result with a null value when a person is not found.
    /// </summary>
    /// <param name="resolver">Resolver implementation under test.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup operation.</param>
    /// <returns>A task that completes after the assertions have been evaluated.</returns>
    /// <exception cref="Shouldly.ShouldAssertException">Thrown when the contract expectations fail.</exception>
    public static async Task VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
        IPersonIdentityResolver resolver,
        CancellationToken cancellationToken = default)
    {
        // Arrange
        var rfc = "PEGJ850101ABC";

        // Act
        var result = await resolver.FindByRfcAsync(rfc, cancellationToken);

        // Assert
        result.IsSuccessMayBeNull.ShouldBeTrue("Result should be Success (even with null value)");
        result.IsSuccessValueNull.ShouldBeTrue("Result value should be null");
        result.Value.ShouldBeNull("Value should be null when person not found");
    }
}
