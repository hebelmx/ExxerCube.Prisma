using System.Linq.Expressions;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="RepositoryContract{T, TId}"/> — the reference-fake-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// <para>
/// Converted from the standalone <c>IRepositoryContractTests</c> (formerly
/// <c>Tests.Domain\Repositories</c>) in Phase 5 of the ITDD refactor and relocated to the
/// blueprint home (§3.1). Its <c>CreateSut()</c> returns a fresh in-memory reference store from
/// <see cref="RepositoryMockFactory"/> per test; the entity-shaped hooks fill the contract's generic
/// parameters with a simple <see cref="TestEntity"/>.
/// </para>
/// <para>
/// Four original mock tests assert <em>fault-injection</em> behaviour ("operation fails ⇒ surfaced as
/// a failure Result, not a throw") that no healthy persistence SUT can be made to produce
/// deterministically. They are <strong>not contract-grade</strong> (they cannot run against a real
/// repository without fault injection) so they stay here on the blueprint as self-contained
/// <c>[Fact]</c>s — the Phase-3 "blueprint-only tests stay on the blueprint" pattern. They are
/// preserved, never deleted (invariant 5).
/// </para>
/// </remarks>
public sealed class MockRepositoryContractTests : RepositoryContract<MockRepositoryContractTests.TestEntity, Guid>
{
    /// <summary>Simple test entity for contract testing.</summary>
    public sealed class TestEntity
    {
        /// <summary>Gets the unique identifier.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets the entity name (used by the projection contract).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the entity value.</summary>
        public decimal Value { get; init; }

        /// <summary>Gets a value indicating whether the entity is active (the matching predicate).</summary>
        public bool IsActive { get; init; }

        /// <summary>Gets the creation timestamp.</summary>
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }

    /// <inheritdoc />
    protected override IRepository<TestEntity, Guid> CreateSut()
        => RepositoryMockFactory.CreateContractConformingMock<TestEntity, Guid>(e => e.Id);

    /// <inheritdoc />
    protected override TestEntity CreateMatchingEntity()
        => new() { Id = Guid.NewGuid(), Name = $"Match-{Guid.NewGuid():N}", Value = 100m, IsActive = true };

    /// <inheritdoc />
    protected override TestEntity CreateNonMatchingEntity()
        => new() { Id = Guid.NewGuid(), Name = $"NoMatch-{Guid.NewGuid():N}", Value = 100m, IsActive = false };

    /// <inheritdoc />
    protected override Guid IdOf(TestEntity entity) => entity.Id;

    /// <inheritdoc />
    protected override Expression<Func<TestEntity, bool>> MatchingPredicate() => e => e.IsActive;

    /// <inheritdoc />
    protected override Expression<Func<TestEntity, bool>> NeverMatchingPredicate() => e => e.Value < 0m;

    /// <inheritdoc />
    protected override Expression<Func<TestEntity, string>> NameSelector() => e => e.Name;

    //
    // Blueprint-only fault-injection tests (not contract-grade — see class remarks).
    // Bodies preserved verbatim from the original IRepositoryContractTests.
    //

    /// <summary>Blueprint: an operation failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnFailure_WhenOperationFails()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entityId = Guid.NewGuid();

        repository.GetByIdAsync(entityId, Arg.Any<CancellationToken>())
            .Returns(Result<TestEntity?>.WithFailure("Database connection failed"));

        // Act
        var result = await repository.GetByIdAsync(entityId, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when operation fails
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain("Database connection failed");
    }

    /// <summary>Blueprint: a query failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task FindAsync_ShouldReturnFailure_WhenOperationFails()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.IsActive;

        repository.FindAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.WithFailure("Query execution failed"));

        // Act
        var result = await repository.FindAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when operation fails
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain("Query execution failed");
    }

    /// <summary>Blueprint: an add failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task AddAsync_ShouldReturnFailure_WhenOperationFails()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "NewEntity" };

        repository.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("Failed to add entity"));

        // Act
        var result = await repository.AddAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when operation fails
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain("Failed to add entity");
    }

    /// <summary>Blueprint: a save failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task SaveChangesAsync_ShouldReturnFailure_WhenSaveFails()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();

        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.WithFailure("Transaction failed"));

        // Act
        var result = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when save operation fails
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain("Transaction failed");
    }
}
