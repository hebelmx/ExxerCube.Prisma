namespace ExxerCube.Prisma.Tests.Domain.Repositories;

/// <summary>
/// IITDD contract tests for <see cref="IRepository{T, TId}"/> interface.
/// These tests define the behavioral contract that ANY implementation must satisfy.
///
/// These tests use mocks to validate the interface contract (WHAT), not implementation details (HOW).
/// Any valid implementation of IRepository must pass these contract tests.
///
/// Following IITDD principles:
/// - Tests validate interface contracts using mocks
/// - Tests are reusable across all implementations
/// - Tests validate WHAT the interface promises, not HOW it's implemented
/// - Tests follow Liskov Substitution Principle
/// </summary>
public sealed class IRepositoryContractTests
{
    // Test Entity

    /// <summary>
    /// Simple test entity for contract testing.
    /// </summary>
    public sealed class TestEntity
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Value { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }

    //

    // GetByIdAsync Contract Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSuccessWithEntity_WhenEntityExists()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entityId = Guid.NewGuid();
        var expectedEntity = new TestEntity { Id = entityId, Name = "Test", Value = 100m };

        repository.GetByIdAsync(entityId, Arg.Any<CancellationToken>())
            .Returns(Result<TestEntity?>.Success(expectedEntity));

        // Act
        var result = await repository.GetByIdAsync(entityId, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with entity when found
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Id.ShouldBe(entityId);
        result.Value.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFailure_WhenEntityNotFound()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entityId = Guid.NewGuid();

        // ROP-compliant: "not found" is a failure case, not success with null
        // NSubstitute auto-wraps Result<T> in Task<T> for async methods
        repository.GetByIdAsync(entityId, Arg.Any<CancellationToken>())
            .Returns(Result<TestEntity?>.WithFailure($"Entity with id {entityId} not found"));

        // Act
        var result = await repository.GetByIdAsync(entityId, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when entity not found (ROP-compliant)
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain($"Entity with id {entityId} not found");
    }

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

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCancelled_WhenCancellationRequested()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entityId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.GetByIdAsync(entityId, cts.Token)
            .Returns(ResultExtensions.Cancelled<TestEntity?>());

        // Act
        var result = await repository.GetByIdAsync(entityId, cts.Token);

        // Assert - Contract: Must return Cancelled when cancellation requested
        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();
    }

    //

    // FindAsync Contract Tests

    [Fact]
    public async Task FindAsync_ShouldReturnSuccessWithMatchingEntities_WhenPredicateMatches()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.IsActive;
        var matchingEntities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "Active1", IsActive = true },
            new TestEntity { Id = Guid.NewGuid(), Name = "Active2", IsActive = true }
        };

        repository.FindAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.Success(matchingEntities));

        // Act
        var result = await repository.FindAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with matching entities
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(e => e.IsActive).ShouldBeTrue();
    }

    [Fact]
    public async Task FindAsync_ShouldReturnSuccessWithEmptyList_WhenNoMatches()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.IsActive;

        repository.FindAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.Success(new List<TestEntity>()));

        // Act
        var result = await repository.FindAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with empty list when no matches
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

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

    //

    // ExistsAsync Contract Tests

    [Fact]
    public async Task ExistsAsync_ShouldReturnSuccessWithTrue_WhenEntityExists()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.Name == "Test";

        repository.ExistsAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        // Act
        var result = await repository.ExistsAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with true when entity exists
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnSuccessWithFalse_WhenEntityNotExists()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.Name == "NonExistent";

        repository.ExistsAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        // Act
        var result = await repository.ExistsAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with false when entity doesn't exist
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    //

    // CountAsync Contract Tests

    [Fact]
    public async Task CountAsync_ShouldReturnSuccessWithCount_WhenNoPredicate()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();

        repository.CountAsync(null, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(5));

        // Act
        var result = await repository.CountAsync(null, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with total count when no predicate
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnSuccessWithFilteredCount_WhenPredicateProvided()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.IsActive;

        repository.CountAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(3));

        // Act
        var result = await repository.CountAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with filtered count when predicate provided
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
    }

    //

    // ListAsync Contract Tests

    [Fact]
    public async Task ListAsync_ShouldReturnSuccessWithAllEntities_WhenNoPredicate()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var allEntities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "Entity1" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Entity2" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Entity3" }
        };

        repository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.Success(allEntities));

        // Act
        var result = await repository.ListAsync(TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with all entities
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ListAsync_WithPredicate_ShouldReturnSuccessWithFilteredEntities()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.Value > 50m;
        var filteredEntities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "HighValue", Value = 100m }
        };

        repository.ListAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.Success(filteredEntities));

        // Act
        var result = await repository.ListAsync(predicate, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with filtered entities
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Value.ShouldBeGreaterThan(50m);
    }

    [Fact]
    public async Task ListAsync_WithSpecification_ShouldReturnSuccessWithMatchingEntities()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var spec = new TestSpecification
        {
            Criteria = e => e.IsActive,
            OrderBy = e => e.Name,
            Take = 2
        };
        var matchingEntities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "A", IsActive = true },
            new TestEntity { Id = Guid.NewGuid(), Name = "B", IsActive = true }
        };

        repository.ListAsync(spec, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TestEntity>>.Success(matchingEntities));

        // Act
        var result = await repository.ListAsync(spec, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with entities matching specification
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(e => e.IsActive).ShouldBeTrue();
    }

    //

    // FirstOrDefaultAsync Contract Tests

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnSuccessWithEntity_WhenMatchFound()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var spec = new TestSpecification { Criteria = e => e.Name == "Target" };
        var matchingEntity = new TestEntity { Id = Guid.NewGuid(), Name = "Target" };

        repository.FirstOrDefaultAsync(spec, Arg.Any<CancellationToken>())
            .Returns(Result<TestEntity?>.Success(matchingEntity));

        // Act
        var result = await repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with first matching entity
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Name.ShouldBe("Target");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnFailure_WhenNoMatchFound()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var spec = new TestSpecification { Criteria = e => e.Name == "NonExistent" };

        // ROP-compliant: "not found" is a failure case, not success with null
        // Note: FirstOrDefault is a query operation that may legitimately return null,
        // but in ROP, we treat "no match found" as a failure to maintain consistency
        // NSubstitute auto-wraps Result<T> in Task<T> for async methods
        repository.FirstOrDefaultAsync(spec, Arg.Any<CancellationToken>())
            .Returns(Result<TestEntity?>.WithFailure("No entity matching the specification was found"));

        // Act
        var result = await repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Failure when no match found (ROP-compliant)
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain("No entity matching the specification was found");
    }

    //

    // SelectAsync Contract Tests

    [Fact]
    public async Task SelectAsync_ShouldReturnSuccessWithProjectedResults()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        Expression<Func<TestEntity, bool>> predicate = e => e.IsActive;
        Expression<Func<TestEntity, string>> selector = e => e.Name;
        var projectedNames = new List<string> { "Name1", "Name2" };

        repository.SelectAsync(predicate, selector, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success(projectedNames));

        // Act
        var result = await repository.SelectAsync(predicate, selector, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with projected results
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain("Name1");
        result.Value.ShouldContain("Name2");
    }

    //

    // AddAsync Contract Tests

    [Fact]
    public async Task AddAsync_ShouldReturnSuccess_WhenEntityAdded()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "NewEntity" };

        repository.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await repository.AddAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is staged for persistence
        result.IsSuccess.ShouldBeTrue();
    }

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

    //

    // AddRangeAsync Contract Tests

    [Fact]
    public async Task AddRangeAsync_ShouldReturnSuccess_WhenEntitiesAdded()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "Entity1" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Entity2" }
        };

        repository.AddRangeAsync(entities, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await repository.AddRangeAsync(entities, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entities are staged
        result.IsSuccess.ShouldBeTrue();
    }

    //

    // UpdateAsync Contract Tests

    [Fact]
    public async Task UpdateAsync_ShouldReturnSuccess_WhenEntityUpdated()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "UpdatedEntity" };

        repository.UpdateAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await repository.UpdateAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is marked for update
        result.IsSuccess.ShouldBeTrue();
    }

    //

    // RemoveAsync Contract Tests

    [Fact]
    public async Task RemoveAsync_ShouldReturnSuccess_WhenEntityRemoved()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "ToRemove" };

        repository.RemoveAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await repository.RemoveAsync(entity, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entity is marked for removal
        result.IsSuccess.ShouldBeTrue();
    }

    //

    // RemoveRangeAsync Contract Tests

    [Fact]
    public async Task RemoveRangeAsync_ShouldReturnSuccess_WhenEntitiesRemoved()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "ToRemove1" },
            new TestEntity { Id = Guid.NewGuid(), Name = "ToRemove2" }
        };

        repository.RemoveRangeAsync(entities, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await repository.RemoveRangeAsync(entities, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success when entities are marked for removal
        result.IsSuccess.ShouldBeTrue();
    }

    //

    // SaveChangesAsync Contract Tests

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnSuccessWithCount_WhenChangesSaved()
    {
        // Arrange
        var repository = Substitute.For<IRepository<TestEntity, Guid>>();

        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(3));

        // Act
        var result = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Contract: Must return Success with number of affected rows
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
    }

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

    //

    // Specification Implementation

    private sealed class TestSpecification : ISpecification<TestEntity>
    {
        public Expression<Func<TestEntity, bool>>? Criteria { get; init; }
        public Expression<Func<TestEntity, object>>? OrderBy { get; init; }
        public Expression<Func<TestEntity, object>>? OrderByDescending { get; init; }
        public IReadOnlyList<Expression<Func<TestEntity, object>>> Includes => Array.Empty<Expression<Func<TestEntity, object>>>();
        public int? Skip { get; init; }
        public int? Take { get; init; }
        public bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
    }

    //
}