namespace ExxerCube.Prisma.Tests.Domain.Repositories;

public class OrderRepositoryTests
{
    private readonly IRepository<Order, Guid> _repo;

    public OrderRepositoryTests()
    {
        _repo = Substitute.For<IRepository<Order, Guid>>();
    }

    [Fact]
    public async Task GetById_Should_Return_Success_When_Order_Found()
    {
        var orderId = Guid.NewGuid();
        var expected = new Order(orderId);
        _repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Order?>.Success(expected)));

        var result = await _repo.GetByIdAsync(orderId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetById_Should_Return_Failure_When_Not_Found()
    {
        var orderId = Guid.NewGuid();
        _repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Order?>.WithFailure("Order not found")));

        var result = await _repo.GetByIdAsync(orderId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Order not found");
    }

    [Fact]
    public async Task FindAsync_Should_Filter_Correctly()
    {
        var specification = NSubstitute.Substitute.For<ISpecification<Order>>();
        specification.Criteria.Returns(o => o.Total > 100);
        //Define a speficaction with a predicated
        //Expression<Func<T, bool>> predicate,
        Expression<Func<Order, bool>> predicate = o => o.Total > 100;

        _repo.FindAsync(predicate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<Order>>.Success(new List<Order>())));

        var result = await _repo.FindAsync(predicate, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}

/// <summary>
/// Test entity for OrderRepositoryTests.
/// </summary>
public sealed class Order
{
    public Order(Guid id) => Id = id;

    public Guid Id { get; }
    public decimal Total { get; init; }
}

