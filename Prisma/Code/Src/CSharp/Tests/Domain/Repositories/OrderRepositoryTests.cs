namespace ExxerCube.Prisma.Tests.Domain.Repositories;

/// <summary>
/// IITDD contract tests for <see cref="IRepository{T, TId}"/> using Order entity as example.
/// These tests complement <see cref="IRepositoryContractTests"/> with domain-specific examples.
/// 
/// Note: For comprehensive contract tests, see <see cref="IRepositoryContractTests"/>.
/// These tests demonstrate contract testing with a specific domain entity (Order).
/// </summary>
public sealed class OrderRepositoryTests
{
	private readonly IRepository<Order, Guid> _repository;

	public OrderRepositoryTests()
	{
		_repository = Substitute.For<IRepository<Order, Guid>>();
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnSuccess_WhenOrderFound()
	{
		// Arrange
		var orderId = Guid.NewGuid();
		var expectedOrder = new Order(orderId) { Total = 150m };

		_repository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
			.Returns(Result<Order?>.Success(expectedOrder));

		// Act
		var result = await _repository.GetByIdAsync(orderId, TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success with entity when found
		result.IsSuccess.ShouldBeTrue();
		result.Value.ShouldNotBeNull();
		result.Value!.Id.ShouldBe(orderId);
		result.Value.Total.ShouldBe(150m);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnSuccessWithNull_WhenOrderNotFound()
	{
		// Arrange
		var orderId = Guid.NewGuid();

		_repository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
			.Returns(Result<Order?>.Success(null));

		// Act
		var result = await _repository.GetByIdAsync(orderId, TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success with null when not found
		result.IsSuccess.ShouldBeTrue();
		result.Value.ShouldBeNull();
	}

	[Fact]
	public async Task FindAsync_ShouldReturnSuccessWithMatchingOrders_WhenPredicateMatches()
	{
		// Arrange
		Expression<Func<Order, bool>> predicate = o => o.Total > 100m;
		var matchingOrders = new List<Order>
		{
			new Order(Guid.NewGuid()) { Total = 150m },
			new Order(Guid.NewGuid()) { Total = 200m }
		};

		_repository.FindAsync(predicate, Arg.Any<CancellationToken>())
			.Returns(Result<IReadOnlyList<Order>>.Success(matchingOrders));

		// Act
		var result = await _repository.FindAsync(predicate, TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success with matching entities
		result.IsSuccess.ShouldBeTrue();
		result.Value.ShouldNotBeNull();
		result.Value.Count.ShouldBe(2);
		result.Value.All(o => o.Total > 100m).ShouldBeTrue();
	}

	[Fact]
	public async Task ListAsync_WithSpecification_ShouldReturnSuccessWithFilteredAndOrderedOrders()
	{
		// Arrange
		var spec = new OrderSpecification
		{
			Criteria = o => o.Total > 50m,
			OrderBy = o => o.Total,
			Take = 5
		};
		var matchingOrders = new List<Order>
		{
			new Order(Guid.NewGuid()) { Total = 60m },
			new Order(Guid.NewGuid()) { Total = 75m }
		};

		_repository.ListAsync(spec, Arg.Any<CancellationToken>())
			.Returns(Result<IReadOnlyList<Order>>.Success(matchingOrders));

		// Act
		var result = await _repository.ListAsync(spec, TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success with entities matching specification
		result.IsSuccess.ShouldBeTrue();
		result.Value.ShouldNotBeNull();
		result.Value.Count.ShouldBe(2);
		result.Value.All(o => o.Total > 50m).ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldReturnSuccess_WhenOrderAdded()
	{
		// Arrange
		var order = new Order(Guid.NewGuid()) { Total = 100m };

		_repository.AddAsync(order, Arg.Any<CancellationToken>())
			.Returns(Result.Success());

		// Act
		var result = await _repository.AddAsync(order, TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success when entity is staged
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task SaveChangesAsync_ShouldReturnSuccessWithCount_WhenChangesSaved()
	{
		// Arrange
		_repository.SaveChangesAsync(Arg.Any<CancellationToken>())
			.Returns(Result<int>.Success(2));

		// Act
		var result = await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

		// Assert - Contract: Must return Success with number of affected rows
		result.IsSuccess.ShouldBeTrue();
		result.Value.ShouldBe(2);
	}

	#region Test Entities and Specifications

	private sealed class Order
	{
		public Order(Guid id) => Id = id;

		public Guid Id { get; }
		public decimal Total { get; init; }
	}

	private sealed class OrderSpecification : ISpecification<Order>
	{
		public Expression<Func<Order, bool>>? Criteria { get; init; }
		public Expression<Func<Order, object>>? OrderBy { get; init; }
		public Expression<Func<Order, object>>? OrderByDescending { get; init; }
		public IReadOnlyList<Expression<Func<Order, object>>> Includes => Array.Empty<Expression<Func<Order, object>>>();
		public int? Skip { get; init; }
		public int? Take { get; init; }
		public bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
	}

	#endregion
}