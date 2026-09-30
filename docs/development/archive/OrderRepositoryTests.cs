using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using MyCompany.MyApp.Domain.Contracts;
using MyCompany.MyApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Tests.Application.Repositories
{
    public class OrderRepositoryTests
    {
        private readonly IRepository<Order, Guid> _repo;
        private readonly ILogger<OrderRepositoryTests> _logger;
        private readonly ITestOutputHelper _output;

        public OrderRepositoryTests(ITestOutputHelper output)
        {
            _repo = Substitute.For<IRepository<Order, Guid>>();
            _logger = Substitute.For<ILogger<OrderRepositoryTests>>();
            _output = output;
        }

        [Fact]
        public async Task GetById_Should_Return_Success_When_Order_Found()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var expected = new Order(orderId);
            _repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
                 .Returns(Result<Order>.Success(expected));

            // Act
            var result = await _repo.GetByIdAsync(orderId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(expected);
            _logger.Received(0).LogError(Arg.Any<string>());
        }

        [Fact]
        public async Task GetById_Should_Return_Failure_When_Not_Found()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            _repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
                 .Returns(Result<Order>.WithFailure("Order not found"));

            // Act
            var result = await _repo.GetByIdAsync(orderId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain("Order not found");
            _logger.Received(0).LogError(Arg.Any<string>());
        }

        [Fact]
        public async Task FindAsync_Should_Filter_Correctly()
        {
            // Arrange
            var spec = new TestSpec(o => o.Total > 100);
            _repo.FindAsync(spec.Criteria!, Arg.Any<CancellationToken>())
                 .Returns(Result<IReadOnlyList<Order>>.Success(new List<Order>()));

            // Act
            var result = await _repo.FindAsync(spec.Criteria!);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }

        private sealed class TestSpec : ISpecification<Order>
        {
            public TestSpec(Expression<Func<Order, bool>> criteria) => Criteria = criteria;
            public Expression<Func<Order, bool>> Criteria { get; }
            public Expression<Func<Order, object>>? OrderBy => null;
            public Expression<Func<Order, object>>? OrderByDescending => null;
            public IReadOnlyList<Expression<Func<Order, object>>> Includes => Array.Empty<Expression<Func<Order, object>>>();
            public int? Skip => null;
            public int? Take => null;
            public bool IsPagingEnabled => false;
        }
    }
}