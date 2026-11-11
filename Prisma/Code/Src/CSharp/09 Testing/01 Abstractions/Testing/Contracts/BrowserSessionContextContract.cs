using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IBrowserSessionContext"/> — every implementation (the mock
/// blueprint and the Playwright adapter) must pass these tests unchanged (ADR-005, ADR-010 §7).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited <c>[Fact]</c> runs once per deriving
/// class.
/// </para>
/// <para>
/// Scope (ADR-005 §5): only the behavior <em>any</em> implementation must exhibit <em>without a live
/// browser</em> — Result semantics (failure not throw), null/empty-input handling, the
/// pre-cancelled-token cancellation contract, and fail-closed when no session is established. Every
/// such path short-circuits before touching Playwright, so the real adapter satisfies these with no
/// browser launched. The happy paths (a real capture/restore/attach round-trip) require a browser and
/// live in the implementation's E2E tests.
/// </para>
/// </remarks>
public abstract class BrowserSessionContextContract
{
    /// <summary>Initializes the contract with the session-context implementation under test.</summary>
    /// <param name="sut">The <see cref="IBrowserSessionContext"/> implementation to verify.</param>
    protected BrowserSessionContextContract(IBrowserSessionContext sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the implementation under test.</summary>
    protected IBrowserSessionContext Sut { get; }

    //
    // ExportStorageStateAsync
    //

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task ExportStorageStateAsync_PreCancelledToken_ReturnsCancelled()
    {
        var result = await Sut.ExportStorageStateAsync(new CancellationToken(canceled: true));

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: exporting with no established session fails closed (no silent empty success).</summary>
    [Fact]
    public async Task ExportStorageStateAsync_WithoutEstablishedSession_ReturnsFailure()
    {
        var result = await Sut.ExportStorageStateAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    //
    // LoadStorageStateAsync
    //

    /// <summary>Contract: a null storage-state reference yields a failure Result.</summary>
    [Fact]
    public async Task LoadStorageStateAsync_NullReference_ReturnsFailure()
    {
        var result = await Sut.LoadStorageStateAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a blank storage-state reference yields a failure Result.</summary>
    [Fact]
    public async Task LoadStorageStateAsync_BlankReference_ReturnsFailure()
    {
        var result = await Sut.LoadStorageStateAsync("   ", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result.</summary>
    [Fact]
    public async Task LoadStorageStateAsync_PreCancelledToken_ReturnsCancelled()
    {
        var result = await Sut.LoadStorageStateAsync("some-state", new CancellationToken(canceled: true));

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    //
    // ConnectToExistingContextAsync
    //

    /// <summary>Contract: a null endpoint yields a failure Result.</summary>
    [Fact]
    public async Task ConnectToExistingContextAsync_NullEndpoint_ReturnsFailure()
    {
        var result = await Sut.ConnectToExistingContextAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a blank endpoint yields a failure Result.</summary>
    [Fact]
    public async Task ConnectToExistingContextAsync_BlankEndpoint_ReturnsFailure()
    {
        var result = await Sut.ConnectToExistingContextAsync("   ", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result.</summary>
    [Fact]
    public async Task ConnectToExistingContextAsync_PreCancelledToken_ReturnsCancelled()
    {
        var result = await Sut.ConnectToExistingContextAsync("ws://localhost:9222", new CancellationToken(canceled: true));

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    //
    // IsAuthenticatedAsync
    //

    /// <summary>Contract: a null selector yields a failure Result.</summary>
    [Fact]
    public async Task IsAuthenticatedAsync_NullSelector_ReturnsFailure()
    {
        var result = await Sut.IsAuthenticatedAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a blank selector yields a failure Result.</summary>
    [Fact]
    public async Task IsAuthenticatedAsync_BlankSelector_ReturnsFailure()
    {
        var result = await Sut.IsAuthenticatedAsync("   ", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result.</summary>
    [Fact]
    public async Task IsAuthenticatedAsync_PreCancelledToken_ReturnsCancelled()
    {
        var result = await Sut.IsAuthenticatedAsync("#dashboard", new CancellationToken(canceled: true));

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }
}
