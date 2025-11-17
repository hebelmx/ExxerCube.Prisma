using ExxerCube.Prisma.Domain.Interfaces;
using Shouldly;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Contract tests for <see cref="IPersonIdentityResolver"/> interface.
/// These are reusable test methods that can be called by Infrastructure test projects
/// to verify that adapters correctly implement the interface contract.
/// </summary>
public static class IPersonIdentityResolverContractTests
{
    /// <summary>
    /// Verifies that FindByRfcAsync returns Success with null value when person not found.
    /// This is the expected behavior for nullable Result types.
    /// </summary>
    /// <param name="resolver">The resolver implementation to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

