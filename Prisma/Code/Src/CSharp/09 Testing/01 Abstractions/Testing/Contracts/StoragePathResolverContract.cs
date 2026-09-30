using ExxerCube.Prisma.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IStoragePathResolver"/> — every implementation (the reference
/// <see cref="FakeStoragePathResolver"/> and the production <c>SharedStoragePathResolver</c>) must pass these
/// tests unchanged (ADR-005, ADR-011).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited <c>[Fact]</c>/<c>[Theory]</c> runs once per
/// deriving class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct resolver must exhibit — fail-closed on blank
/// input and on base-escaping (traversal) input, and a rooted absolute result that ends with the requested
/// relative path on success. Mode-specific concerns (the unconfigured-base failure, which needs control over
/// the base) stay in each SUT's own test project.
/// </para>
/// </remarks>
public abstract class StoragePathResolverContract
{
    /// <summary>Initializes the contract with the resolver under test.</summary>
    /// <param name="sut">The <see cref="IStoragePathResolver"/> implementation to verify.</param>
    protected StoragePathResolverContract(IStoragePathResolver sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the resolver under test.</summary>
    protected IStoragePathResolver Sut { get; }

    /// <summary>Contract: a blank relative path fails closed (a failure Result, never an exception).</summary>
    /// <param name="blank">A blank relative path.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankRelativePath_ReturnsFailure(string blank)
    {
        var result = Sut.Resolve(blank);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a relative path that would escape the storage base fails closed.</summary>
    /// <param name="escaping">A traversal/escape path.</param>
    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("../outside.pdf")]
    [InlineData("2026/../../escape.pdf")]
    public void Resolve_BaseEscapingPath_ReturnsFailure(string escaping)
    {
        var result = Sut.Resolve(escaping);

        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a well-formed relative path resolves to a rooted absolute path that ends with that relative
    /// path (separators normalized to this platform) — i.e. the file's location under the configured base.
    /// </summary>
    [Fact]
    public void Resolve_ValidRelativePath_ReturnsRootedAbsolutePathEndingWithRelative()
    {
        var result = Sut.Resolve("2026/06/12/doc-abc.pdf");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        Path.IsPathRooted(result.Value).ShouldBeTrue();

        var expectedTail = Path.Combine("2026", "06", "12", "doc-abc.pdf");
        result.Value!.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }
}
