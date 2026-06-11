using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Minimal <see cref="ISpecification{T}"/> used by <see cref="RepositoryContract{T, TId}"/> to
/// exercise the specification-based query members. Wraps a single filter predicate; ordering,
/// includes and paging are unused by the contract.
/// </summary>
/// <typeparam name="T">The entity type the specification targets.</typeparam>
internal sealed class ContractSpecification<T> : ISpecification<T>
    where T : class
{
    /// <summary>Initializes a new specification with the supplied filter predicate.</summary>
    /// <param name="criteria">The filter predicate.</param>
    public ContractSpecification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; }

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderBy => null;

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderByDescending => null;

    /// <inheritdoc />
    public IReadOnlyList<Expression<Func<T, object>>> Includes => Array.Empty<Expression<Func<T, object>>>();

    /// <inheritdoc />
    public int? Skip => null;

    /// <inheritdoc />
    public int? Take => null;
}
