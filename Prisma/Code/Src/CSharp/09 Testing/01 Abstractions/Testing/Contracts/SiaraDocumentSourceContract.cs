using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ISiaraDocumentSource"/> — every implementation (the reference fake
/// and the production <c>SiaraDocumentSource</c>) must pass these tests unchanged (ADR-005, ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited <c>[Fact]</c> runs once per deriving
/// class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct discovery source must exhibit — the
/// Railway-Oriented cancellation guarantee and the success-shape guarantee (a non-null list of non-blank
/// ids the downloader can resolve). Mode-specific acquisition/scrape mechanics and the fail-closed paths
/// stay in the SUT's own test project.
/// </para>
/// </remarks>
public abstract class SiaraDocumentSourceContract
{
    /// <summary>Initializes the contract with the discovery source under test.</summary>
    /// <param name="sut">The <see cref="ISiaraDocumentSource"/> implementation to verify.</param>
    protected SiaraDocumentSourceContract(ISiaraDocumentSource sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the discovery source under test.</summary>
    protected ISiaraDocumentSource Sut { get; }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task DiscoverDocumentIdsAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.DiscoverDocumentIdsAsync(cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a successful discovery wraps a non-null list of non-blank ids — each id is something a
    /// downloader can resolve. The list may be empty, but it is never null and never carries a blank id.
    /// </summary>
    [Fact]
    public async Task DiscoverDocumentIdsAsync_Success_ReturnsNonNullListOfNonBlankIds()
    {
        var result = await Sut.DiscoverDocumentIdsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        foreach (var id in result.Value!)
        {
            id.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
