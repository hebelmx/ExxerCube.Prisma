using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IPersonIdentityResolver"/> — every implementation (and the mock
/// blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-Sut</strong> mechanism (the resolver constructs in one
/// expression). Phase 5 of the ITDD refactor created this base by <em>folding</em> the orphaned static
/// helper <c>IPersonIdentityResolverContractTests.VerifyFindByRfcAsync_…</c> (Testing.Contracts — never
/// invoked by any impl test) into a real contract, and replaces the fake-green placeholder
/// <c>IPersonIdentityResolverContractExecutionTests</c> (whose body was <c>await Task.CompletedTask</c>).
/// </para>
/// <para>
/// <strong>Scope = cross-implementation behavioural invariants.</strong>
/// <see cref="IPersonIdentityResolver.FindByRfcAsync"/> now has two implementations:
/// the pure in-memory <c>PersonIdentityResolverService</c> (always returns null) and the DB-backed
/// <c>DbPersonIdentityResolverService</c> (returns null when no matching row exists, which is the
/// same result against an empty database). The contract asserts "not found → success(null)" which
/// is valid for both. DB-specific deduplication persistence is tested in the system-level integration
/// tests (<c>PersonIdentityDedupIntegrationTests</c>) where a real SQL Server container is available.
/// </para>
/// <para>
/// The pure methods (resolve/dedup/variants) are mutation-hardened in the impl's
/// <c>*MutationTests</c>; exact RFC-variant strings and name-normalisation remain implementation
/// richness kept in the impl-side tests (ADR-005 §5).
/// </para>
/// </remarks>
public abstract class PersonIdentityResolverContract
{
    /// <summary>Initializes the contract with the resolver under test.</summary>
    /// <param name="sut">The <see cref="IPersonIdentityResolver"/> implementation to verify.</param>
    protected PersonIdentityResolverContract(IPersonIdentityResolver sut) => Sut = sut;

    /// <summary>Gets the resolver under test.</summary>
    protected IPersonIdentityResolver Sut { get; }

    //
    // ResolveIdentityAsync
    //

    /// <summary>Contract: a null person is rejected with a failure (not a throw).</summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithNullPerson_ReturnsFailure()
    {
        var result = await Sut.ResolveIdentityAsync(null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a failure (not a throw).</summary>
    [Fact]
    public async Task ResolveIdentityAsync_WhenCancelled_ReturnsFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.ResolveIdentityAsync(new Persona { Nombre = "Juan" }, cts.Token);

        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a valid person resolves successfully to a non-null person.</summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithValidPerson_ReturnsSuccess()
    {
        var person = new Persona { ParteId = 1, Nombre = "Juan", Paterno = "Perez", Materno = "Garcia" };

        var result = await Sut.ResolveIdentityAsync(person, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>
    /// Contract: when the person has an RFC, identity resolution populates RFC variants that include the
    /// original RFC (the exact variant set is implementation detail, kept impl-side).
    /// </summary>
    [Fact]
    public async Task ResolveIdentityAsync_WithRfc_PopulatesRfcVariantsIncludingOriginal()
    {
        var person = new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" };

        var result = await Sut.ResolveIdentityAsync(person, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.RfcVariants.ShouldNotBeEmpty();
        result.Value.RfcVariants.ShouldContain("PEGJ850101ABC");
    }

    //
    // DeduplicatePersonsAsync
    //

    /// <summary>Contract: a null list is rejected with a failure (not a throw).</summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithNullList_ReturnsFailure()
    {
        var result = await Sut.DeduplicatePersonsAsync(null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a failure (not a throw).</summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WhenCancelled_ReturnsFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.DeduplicatePersonsAsync(new List<Persona>(), cts.Token);

        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: persons sharing an RFC across formats (e.g. "PEGJ850101ABC" vs "PEG-850101-ABC") are
    /// recognised as the same identity and collapsed — the core documented dedup invariant.
    /// </summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithDuplicateRfcVariants_RemovesDuplicates()
    {
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" },
            new Persona { ParteId = 2, Nombre = "Juan", Rfc = "PEG-850101-ABC" },
            new Persona { ParteId = 3, Nombre = "Maria", Rfc = "MARG900202XYZ" }
        };

        var result = await Sut.DeduplicatePersonsAsync(persons, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(2);
    }

    /// <summary>Contract: distinct persons are all preserved (nothing is dropped spuriously).</summary>
    [Fact]
    public async Task DeduplicatePersonsAsync_WithUniquePersons_PreservesAll()
    {
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" },
            new Persona { ParteId = 2, Nombre = "Maria", Rfc = "MARG900202XYZ" }
        };

        var result = await Sut.DeduplicatePersonsAsync(persons, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(2);
    }

    //
    // FindByRfcAsync
    //

    /// <summary>
    /// Contract: looking up an RFC that has never been persisted returns <c>success(null)</c> —
    /// "not found" is a valid non-error outcome for the nullable result type. This invariant holds
    /// for both the in-memory service (always returns null) and the DB-backed service (returns null
    /// when the Persona table contains no matching row).
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue()
    {
        // Use a highly-unlikely RFC so neither in-memory nor a shared DB test fixture
        // can collide with a pre-existing row.
        var result = await Sut.FindByRfcAsync("TEST000101ZZZ", TestContext.Current.CancellationToken);

        result.IsSuccessMayBeNull.ShouldBeTrue("Result should be Success (even with null value)");
        result.IsSuccessValueNull.ShouldBeTrue("Result value should be null when RFC is not found");
        result.Value.ShouldBeNull("Value should be null when person not found");
    }

    /// <summary>Contract: an empty/whitespace RFC is rejected with a failure.</summary>
    [Fact]
    public async Task FindByRfcAsync_WithEmptyRfc_ReturnsFailure()
    {
        var result = await Sut.FindByRfcAsync("   ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a failure (not a throw).</summary>
    [Fact]
    public async Task FindByRfcAsync_WhenCancelled_ReturnsFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.FindByRfcAsync("PEGJ850101ABC", cts.Token);

        result.IsFailure.ShouldBeTrue();
    }

    //
    // GenerateRfcVariants
    //

    /// <summary>Contract: an empty/whitespace RFC produces an empty (never null) variant list.</summary>
    [Fact]
    public void GenerateRfcVariants_WithEmptyRfc_ReturnsEmptyList()
    {
        var variants = Sut.GenerateRfcVariants(string.Empty);

        variants.ShouldNotBeNull();
        variants.ShouldBeEmpty();
    }

    /// <summary>Contract: a valid RFC yields a non-empty variant list that includes the original RFC.</summary>
    [Fact]
    public void GenerateRfcVariants_WithValidRfc_IncludesOriginalRfc()
    {
        var variants = Sut.GenerateRfcVariants("PEGJ850101ABC");

        variants.ShouldNotBeEmpty();
        variants.ShouldContain("PEGJ850101ABC");
    }
}
