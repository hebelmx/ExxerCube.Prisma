namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Represents a reusable description of how to query an entity, including filtering, sorting,
/// eager loading and paging rules that repositories can honor.
/// </summary>
/// <typeparam name="T">The entity type the specification targets.</typeparam>
public interface ISpecification<T>
    where T : class
{
    /// <summary>Gets the predicate used to filter the result set; <c>null</c> disables filtering.</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>Gets an expression used for ascending ordering of the results.</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>Gets an expression used for descending ordering of the results.</summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>Gets the navigation expressions that should be eagerly loaded.</summary>
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Gets the number of rows that should be skipped to support paging.</summary>
    int? Skip { get; }

    /// <summary>Gets the number of rows that should be taken after <see cref="Skip"/>.</summary>
    int? Take { get; }

    /// <summary>Gets a value indicating whether paging should be applied.</summary>
    bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
}