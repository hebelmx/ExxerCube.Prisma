using System.Linq.Expressions;
using ExxerCube.Prisma.Domain.Interfaces.Contracts;
using IndQuestResults;

namespace Tests.Domain.Repositories;

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

        var result = await _repo.GetByIdAsync(orderId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetById_Should_Return_Failure_When_Not_Found()
    {
        var orderId = Guid.NewGuid();
        _repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Order?>.WithFailure("Order not found")));

        var result = await _repo.GetByIdAsync(orderId);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Order not found");
    }

    [Fact]
    public async Task FindAsync_Should_Filter_Correctly()
    {
        var spec = new TestSpec(o => o.Total > 100);
        _repo.FindAsync(spec.Criteria!, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<Order>>.Success(new List<Order>())));

        var result = await _repo.FindAsync(spec.Criteria!);

        result.IsSuccess.ShouldBeTrue();
    }

    private sealed class Order
    {
        public Order(Guid id) => Id = id;
        public Guid Id { get; }
        public decimal Total { get; init; }
    }

    private sealed class TestSpec : ISpecification<Order>
    {
        public TestSpec(Expression<Func<Order, bool>> criteria) => Criteria = criteria;
        public Expression<Func<Order, bool>>? Criteria { get; }
        public Expression<Func<Order, object>>? OrderBy => null;
        public Expression<Func<Order, object>>? OrderByDescending => null;
        public IReadOnlyList<Expression<Func<Order, object>>> Includes => Array.Empty<Expression<Func<Order, object>>>();
        public int? Skip => null;
        public int? Take => null;
    }
}
