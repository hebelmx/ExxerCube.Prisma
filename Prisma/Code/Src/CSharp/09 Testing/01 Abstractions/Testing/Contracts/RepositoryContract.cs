using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Async;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IRepository{T, TId}"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <typeparam name="T">The aggregate/entity type the repository persists.</typeparam>
/// <typeparam name="TId">The identifier type used to locate entities.</typeparam>
/// <remarks>
/// <para>
/// Uses the sanctioned ADR-005 §3 <c>CreateSut()</c> fallback: a real implementation owns a
/// per-test persistence fixture (EF InMemory context), while the blueprint owns an in-memory
/// reference store (<see cref="RepositoryMockFactory"/>). The deriving class also supplies the
/// entity-shaped hooks (<see cref="CreateMatchingEntity"/>, <see cref="MatchingPredicate"/>, …)
/// because the contract is generic over <typeparamref name="T"/> and cannot author entity-specific
/// predicates itself — the Phase-4 abstract-fixture-hook pattern (master plan §4.1 Phase 4 addenda).
/// </para>
/// <para>
/// <strong>This is Phase 5 (severe drift): the bulk of these tests had NO real-SUT execution before
/// this contract existed</strong> (the real twin <c>EfCoreRepositoryTests</c> covered ~28% — see
/// master plan §2). The query/command behaviours below are net-new coverage. Names are preserved
/// from the original mock blueprint <c>IRepositoryContractTests</c>. Seeding/verification go
/// <strong>through the interface</strong> (<see cref="IRepository{T, TId}.AddAsync"/> +
/// <see cref="IRepository{T, TId}.SaveChangesAsync"/>), not a fixture's <c>DbContext</c>, so the
/// contract stays implementation-agnostic (Phase 2 addendum). The EF/DB repository is excluded from
/// mutation testing, so verbatim body preservation is not a kill-power concern here.
/// </para>
/// <para>
/// <strong>Phase-5 triage finding — FIXED in Phase 6 (2026-06-11).</strong> The two "not found" tests
/// now assert the original <c>IsFailure.ShouldBeTrue()</c>. First execution (Phase 5) had proved the
/// production <c>EfCoreRepository</c> returned a result that was <em>neither</em> success nor failure on
/// not-found (<c>IsSuccess=false, IsFailure=false, Value=null</c>): IndQuestResults treats a null value
/// as not-<c>IsSuccess</c> (a separate <c>IsSuccessMayBeNull</c> exists), so the impl's not-found guard
/// <c>if (result.IsSuccess &amp;&amp; result.Value is null)</c> was unreachable dead code and the intended
/// <c>WithFailure</c> conversion never fired, contradicting the interface XML doc. Phase 6 fixed the guard
/// to test <c>IsSuccessMayBeNull</c> (owner-gated; the only generic consumer
/// <c>FileMetadataQueryService</c> already routes not-found through its failure branch), so the real impl
/// now returns a proper failure on not-found, matching the reference fake
/// (<see cref="RepositoryMockFactory"/>) which always behaved correctly.
/// </para>
/// </remarks>
public abstract class RepositoryContract<T, TId>
    where T : class
{
    /// <summary>
    /// Creates the implementation under test. Called once per test; the implementation owns its
    /// fixture lifetime.
    /// </summary>
    /// <returns>The <see cref="IRepository{T, TId}"/> implementation to verify.</returns>
    protected abstract IRepository<T, TId> CreateSut();

    /// <summary>
    /// Creates a fresh, unique entity that <strong>satisfies</strong> <see cref="MatchingPredicate"/>.
    /// Successive calls must return distinct entities (distinct identifiers).
    /// </summary>
    /// <returns>A new matching entity.</returns>
    protected abstract T CreateMatchingEntity();

    /// <summary>
    /// Creates a fresh, unique entity that does <strong>not</strong> satisfy
    /// <see cref="MatchingPredicate"/>. Successive calls must return distinct entities.
    /// </summary>
    /// <returns>A new non-matching entity.</returns>
    protected abstract T CreateNonMatchingEntity();

    /// <summary>Gets the identifier of the supplied entity (the value <c>GetByIdAsync</c> looks up).</summary>
    /// <param name="entity">Entity whose identifier is required.</param>
    /// <returns>The entity's identifier.</returns>
    protected abstract TId IdOf(T entity);

    /// <summary>
    /// A predicate matched by <see cref="CreateMatchingEntity"/> instances and not by
    /// <see cref="CreateNonMatchingEntity"/> instances.
    /// </summary>
    /// <returns>The matching predicate.</returns>
    protected abstract Expression<Func<T, bool>> MatchingPredicate();

    /// <summary>A predicate satisfied by <strong>no</strong> seeded entity.</summary>
    /// <returns>The never-matching predicate.</returns>
    protected abstract Expression<Func<T, bool>> NeverMatchingPredicate();

    /// <summary>A projection selector used by the <c>SelectAsync</c> contract (e.g. a name field).</summary>
    /// <returns>A selector projecting an entity to a string.</returns>
    protected abstract Expression<Func<T, string>> NameSelector();

    /// <summary>Builds a specification wrapping <see cref="MatchingPredicate"/>.</summary>
    private ISpecification<T> MatchingSpecification() => new ContractSpecification<T>(MatchingPredicate());

    /// <summary>Builds a specification that matches no seeded entity.</summary>
    private ISpecification<T> NeverMatchingSpecification() => new ContractSpecification<T>(NeverMatchingPredicate());

    /// <summary>Seeds entities through the interface (Add + SaveChanges) so queries observe them.</summary>
    private static async Task SeedAsync(IRepository<T, TId> repository, params T[] entities)
    {
        foreach (var entity in entities)
        {
            (await repository.AddAsync(entity, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        (await repository.SaveChangesAsync(TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    //
    // GetByIdAsync Contract Tests
    //

    /// <summary>Contract: an existing entity is returned on success.</summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnSuccessWithEntity_WhenEntityExists()
    {
        // Arrange
        var repository = CreateSut();
        var entity = CreateMatchingEntity();
        await SeedAsync(repository, entity);

        // Act
        var result = await repository.GetByIdAsync(IdOf(entity), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with entity when found
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        IdOf(result.Value!).ShouldBe(IdOf(entity));
    }

    /// <summary>
    /// Contract: a missing entity is surfaced as a failure Result (ROP "not found" is not a successful
    /// retrieval). Asserts <c>IsFailure == true</c> — the Phase-5 triage defect that made the real impl
    /// return a neither-success-nor-failure result was fixed in Phase 6 (see the class remarks).
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnFailure_WhenEntityNotFound()
    {
        // Arrange - empty repository, look up an id that was never seeded
        var repository = CreateSut();
        var absentId = IdOf(CreateMatchingEntity());

        // Act
        var result = await repository.GetByIdAsync(absentId, TestContext.Current.CancellationToken);

        // Assert - Contract: a not-found lookup is a failure (no bogus entity, no false success)
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a cancelled result (never a throw).</summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnCancelled_WhenCancellationRequested()
    {
        // Arrange
        var repository = CreateSut();
        var entity = CreateMatchingEntity();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await repository.GetByIdAsync(IdOf(entity), cts.Token);

        // Assert - Contract: Must return Cancelled when cancellation requested
        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();
    }

    //
    // FindAsync Contract Tests
    //

    /// <summary>Contract: entities satisfying the predicate are returned.</summary>
    [Fact]
    public async Task FindAsync_ShouldReturnSuccessWithMatchingEntities_WhenPredicateMatches()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.FindAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with the matching entities (filtering applied)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(MatchingPredicate().Compile()).ShouldBeTrue();
    }

    /// <summary>Contract: no matches yields an empty (never null) success.</summary>
    [Fact]
    public async Task FindAsync_ShouldReturnSuccessWithEmptyList_WhenNoMatches()
    {
        // Arrange - only a non-matching entity is present
        var repository = CreateSut();
        await SeedAsync(repository, CreateNonMatchingEntity());

        // Act
        var result = await repository.FindAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with empty list when no matches
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    //
    // ExistsAsync Contract Tests
    //

    /// <summary>Contract: returns true when a matching entity exists.</summary>
    [Fact]
    public async Task ExistsAsync_ShouldReturnSuccessWithTrue_WhenEntityExists()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity());

        // Act
        var result = await repository.ExistsAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with true when entity exists
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    /// <summary>Contract: returns false when nothing matches.</summary>
    [Fact]
    public async Task ExistsAsync_ShouldReturnSuccessWithFalse_WhenEntityNotExists()
    {
        // Arrange - only a non-matching entity is present
        var repository = CreateSut();
        await SeedAsync(repository, CreateNonMatchingEntity());

        // Act
        var result = await repository.ExistsAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with false when entity doesn't exist
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    //
    // CountAsync Contract Tests
    //

    /// <summary>Contract: with no predicate, all entities are counted.</summary>
    [Fact]
    public async Task CountAsync_ShouldReturnSuccessWithCount_WhenNoPredicate()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.CountAsync(null, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with total count when no predicate
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
    }

    /// <summary>Contract: with a predicate, only matching entities are counted.</summary>
    [Fact]
    public async Task CountAsync_ShouldReturnSuccessWithFilteredCount_WhenPredicateProvided()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.CountAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with filtered count when predicate provided
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
    }

    //
    // ListAsync Contract Tests
    //

    /// <summary>Contract: lists every entity when no predicate is supplied.</summary>
    [Fact]
    public async Task ListAsync_ShouldReturnSuccessWithAllEntities_WhenNoPredicate()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.ListAsync(TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with all entities
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(3);
    }

    /// <summary>Contract: lists only matching entities when a predicate is supplied.</summary>
    [Fact]
    public async Task ListAsync_WithPredicate_ShouldReturnSuccessWithFilteredEntities()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.ListAsync(MatchingPredicate(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with filtered entities
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(MatchingPredicate().Compile()).ShouldBeTrue();
    }

    /// <summary>Contract: lists entities matching a specification.</summary>
    [Fact]
    public async Task ListAsync_WithSpecification_ShouldReturnSuccessWithMatchingEntities()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.ListAsync(MatchingSpecification(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with entities matching specification
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(MatchingPredicate().Compile()).ShouldBeTrue();
    }

    //
    // FirstOrDefaultAsync Contract Tests
    //

    /// <summary>Contract: returns the first entity matching a specification.</summary>
    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnSuccessWithEntity_WhenMatchFound()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity());

        // Act
        var result = await repository.FirstOrDefaultAsync(MatchingSpecification(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with first matching entity
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        MatchingPredicate().Compile()(result.Value!).ShouldBeTrue();
    }

    /// <summary>
    /// Contract: no match is surfaced as a failure Result (ROP "no match" is not a successful retrieval).
    /// Asserts <c>IsFailure == true</c> — same Phase-6-fixed not-found path as
    /// <see cref="GetByIdAsync_ShouldReturnFailure_WhenEntityNotFound"/> (see the class remarks).
    /// </summary>
    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnFailure_WhenNoMatchFound()
    {
        // Arrange - only a non-matching entity is present
        var repository = CreateSut();
        await SeedAsync(repository, CreateNonMatchingEntity());

        // Act
        var result = await repository.FirstOrDefaultAsync(NeverMatchingSpecification(), TestContext.Current.CancellationToken);

        // Assert - Contract: a no-match lookup is a failure (no bogus entity, no false success)
        result.IsFailure.ShouldBeTrue();
    }

    //
    // SelectAsync Contract Tests
    //

    /// <summary>Contract: projects matching entities into the selected shape.</summary>
    [Fact]
    public async Task SelectAsync_ShouldReturnSuccessWithProjectedResults()
    {
        // Arrange
        var repository = CreateSut();
        await SeedAsync(repository, CreateMatchingEntity(), CreateMatchingEntity(), CreateNonMatchingEntity());

        // Act
        var result = await repository.SelectAsync(MatchingPredicate(), NameSelector(), TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with projected results
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
    }

    //
    // AddAsync Contract Tests
    //

    /// <summary>Contract: a valid entity is staged successfully.</summary>
    [Fact]
    public async Task AddAsync_ShouldReturnSuccess_WhenEntityAdded()
    {
        // Arrange
        var repository = CreateSut();
        var entity = CreateMatchingEntity();

        // Act
        var result = await repository.AddAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is staged for persistence
        result.IsSuccess.ShouldBeTrue();
    }

    //
    // AddRangeAsync Contract Tests
    //

    /// <summary>Contract: a batch of valid entities is staged successfully.</summary>
    [Fact]
    public async Task AddRangeAsync_ShouldReturnSuccess_WhenEntitiesAdded()
    {
        // Arrange
        var repository = CreateSut();
        var entities = new List<T> { CreateMatchingEntity(), CreateMatchingEntity() };

        // Act
        var result = await repository.AddRangeAsync(entities, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entities are staged
        result.IsSuccess.ShouldBeTrue();
    }

    //
    // UpdateAsync Contract Tests
    //

    /// <summary>Contract: an existing entity can be marked for update.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldReturnSuccess_WhenEntityUpdated()
    {
        // Arrange
        var repository = CreateSut();
        var entity = CreateMatchingEntity();
        await SeedAsync(repository, entity);

        // Act
        var result = await repository.UpdateAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is marked for update
        result.IsSuccess.ShouldBeTrue();
    }

    //
    // RemoveAsync Contract Tests
    //

    /// <summary>
    /// Contract: an existing entity can be marked for removal, and once the change is persisted the
    /// entity is no longer retrievable (observable removal — Phase-5 strengthening over the blueprint's
    /// IsSuccess-only assertion, since severe drift is the coverage-adding phase).
    /// </summary>
    [Fact]
    public async Task RemoveAsync_ShouldReturnSuccess_WhenEntityRemoved()
    {
        // Arrange
        var repository = CreateSut();
        var entity = CreateMatchingEntity();
        await SeedAsync(repository, entity);

        // Act
        var result = await repository.RemoveAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is marked for removal
        result.IsSuccess.ShouldBeTrue();

        // Assert - Contract: after persisting, the removed entity is no longer retrievable
        (await repository.SaveChangesAsync(TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var afterRemoval = await repository.GetByIdAsync(IdOf(entity), TestContext.Current.CancellationToken);
        afterRemoval.IsSuccess.ShouldBeFalse();
    }

    //
    // RemoveRangeAsync Contract Tests
    //

    /// <summary>Contract: a batch of existing entities can be marked for removal.</summary>
    [Fact]
    public async Task RemoveRangeAsync_ShouldReturnSuccess_WhenEntitiesRemoved()
    {
        // Arrange
        var repository = CreateSut();
        var first = CreateMatchingEntity();
        var second = CreateMatchingEntity();
        await SeedAsync(repository, first, second);

        // Act
        var result = await repository.RemoveRangeAsync(new List<T> { first, second }, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entities are marked for removal
        result.IsSuccess.ShouldBeTrue();
    }

    //
    // SaveChangesAsync Contract Tests
    //

    /// <summary>Contract: persisting staged changes succeeds and reports the number written.</summary>
    [Fact]
    public async Task SaveChangesAsync_ShouldReturnSuccessWithCount_WhenChangesSaved()
    {
        // Arrange - stage three entities without saving
        var repository = CreateSut();
        (await repository.AddAsync(CreateMatchingEntity(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await repository.AddAsync(CreateMatchingEntity(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await repository.AddAsync(CreateMatchingEntity(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with number of affected rows
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
    }
}
