using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Infrastructure.Database.Repositories;

/// <summary>
/// Applies an <see cref="ISpecification{T}"/> to an EF Core <see cref="IQueryable{T}"/>.
/// </summary>
/// <typeparam name="T">Entity type being queried.</typeparam>
public static class SpecificationEvaluator<T> where T : class
{
    /// <summary>
    /// Applies the provided specification to the supplied queryable.
    /// </summary>
    /// <param name="inputQuery">Source queryable.</param>
    /// <param name="specification">Specification to apply.</param>
    /// <returns>An <see cref="IQueryable{T}"/> representing the specification.</returns>
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        var query = inputQuery;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        foreach (var include in specification.Includes)
        {
            query = query.Include(include);
        }

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        if (specification.IsPagingEnabled)
        {
            if (specification.Skip.HasValue)
            {
                query = query.Skip(specification.Skip.Value);
            }

            if (specification.Take.HasValue)
            {
                query = query.Take(specification.Take.Value);
            }
        }

        return query;
    }
}