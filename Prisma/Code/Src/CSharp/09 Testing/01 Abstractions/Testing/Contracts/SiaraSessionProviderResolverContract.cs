using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ISiaraSessionProviderResolver"/> — every implementation (the mock
/// blueprint and the production resolver) must pass these tests unchanged (ADR-005, ADR-010).
/// </summary>
/// <remarks>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. Because a resolver is
/// only meaningful relative to a configured mode, the inheritor also supplies the
/// <see cref="ExpectedMode"/> it was configured for. Scope (ADR-005 §5): a resolver never returns a null
/// Result and, for a configured-and-registered mode, returns the provider whose
/// <see cref="ISiaraSessionProvider.Mode"/> matches. The fail-closed behavior for an unregistered/unknown
/// mode is exercised in the production resolver's own mechanics tests.
/// </remarks>
public abstract class SiaraSessionProviderResolverContract
{
    /// <summary>Initializes the contract with the resolver under test and the mode it was configured for.</summary>
    /// <param name="sut">The <see cref="ISiaraSessionProviderResolver"/> implementation to verify.</param>
    /// <param name="expectedMode">The auth mode the resolver is configured to resolve.</param>
    protected SiaraSessionProviderResolverContract(ISiaraSessionProviderResolver sut, SiaraAuthMode expectedMode)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
        ExpectedMode = expectedMode;
    }

    /// <summary>Gets the resolver under test.</summary>
    protected ISiaraSessionProviderResolver Sut { get; }

    /// <summary>Gets the mode the resolver under test is configured to resolve.</summary>
    protected SiaraAuthMode ExpectedMode { get; }

    /// <summary>Contract: resolution never yields a null Result.</summary>
    [Fact]
    public void Resolve_ReturnsNonNullResult()
    {
        Sut.Resolve().ShouldNotBeNull();
    }

    /// <summary>Contract: for the configured (registered) mode, resolution succeeds.</summary>
    [Fact]
    public void Resolve_ForConfiguredMode_ReturnsSuccess()
    {
        var result = Sut.Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>Contract: the resolved provider's mode matches the configured mode.</summary>
    [Fact]
    public void Resolve_ResolvedProviderMatchesConfiguredMode()
    {
        var result = Sut.Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Mode.ShouldBe(ExpectedMode);
    }
}
