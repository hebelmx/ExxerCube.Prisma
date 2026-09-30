namespace ExxerCube.Prisma.Tests.Domain.Repositories;

/// <summary>
/// Test entity for OrderRepositoryTests.
/// </summary>
public sealed class Order
{
    public Order(Guid id) => Id = id;

    public Guid Id { get; }
    public decimal Total { get; init; }
}