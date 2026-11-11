using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
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
/// Railway-Oriented cancellation guarantee and the success-shape guarantee (a non-null list of
/// <see cref="SiaraCase"/> bundles with non-blank <see cref="SiaraCase.CaseId"/> values). Mode-specific
/// acquisition/scrape mechanics and the fail-closed paths stay in the SUT's own test project.
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

    /// <summary>
    /// Contract: a pre-cancelled token to <see cref="ISiaraDocumentSource.DiscoverCasesAsync"/> yields a
    /// Cancelled Result — never an exception (MVP-PATH 2.1).
    /// </summary>
    [Fact]
    public async Task DiscoverCasesAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.DiscoverCasesAsync(cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a successful case-discovery wraps a non-null list of <see cref="SiaraCase"/> entries.
    /// The list may be empty, but it is never null, and each case must have a non-blank CaseId (MVP-PATH 2.1).
    /// </summary>
    [Fact]
    public async Task DiscoverCasesAsync_Success_ReturnsNonNullListOfCases()
    {
        var result = await Sut.DiscoverCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        foreach (var siaraCase in result.Value!)
        {
            siaraCase.CaseId.ShouldNotBeNullOrWhiteSpace();
            siaraCase.Files.ShouldNotBeNull();
        }
    }
}
