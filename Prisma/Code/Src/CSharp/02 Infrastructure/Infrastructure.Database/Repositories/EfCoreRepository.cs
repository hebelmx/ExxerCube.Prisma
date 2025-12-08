using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Infrastructure.Database.Repositories;

/// <summary>
/// Entity Framework Core implementation of the domain repository abstraction.
/// </summary>
/// <typeparam name="T">Entity type handled by the repository.</typeparam>
/// <typeparam name="TId">Identifier type used to locate entities.</typeparam>
public sealed class EfCoreRepository<T, TId> : IRepository<T, TId>
    where T : class
{
    private readonly PrismaDbContext _dbContext;
    private readonly DbSet<T> _dbSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreRepository{T, TId}"/> class.
    /// </summary>
    /// <param name="dbContext">Database context backing the repository.</param>
    public EfCoreRepository(PrismaDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = _dbContext.Set<T>();
    }

    /// <inheritdoc />
    public async Task<Result<T?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<T?>();
        }

        if (id is null)
        {
            return Result<T?>.WithFailure("Identifier cannot be null");
        }

        var result = await ResultTryExtensions.TryAsync(
            async () =>
            {
                var valueTask = _dbSet.FindAsync(new object?[] { id }, cancellationToken);
                return await valueTask.AsTask().ConfigureAwait(false);
            },
            ex => $"Failed to retrieve {typeof(T).Name} by id: {ex.Message}");

        // ROP-compliant: "not found" is a failure case, not success with null
        if (result.IsSuccess && result.Value is null)
        {
            return Result<T?>.WithFailure($"Entity of type {typeof(T).Name} with id {id} not found");
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Task.FromResult(Result<IReadOnlyList<T>>.WithFailure("Predicate cannot be null"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<T>>());
        }

        return ResultTryExtensions.TryAsync(
            async () =>
            {
                var list = await _dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
                return (IReadOnlyList<T>)list;
            },
            ex => $"Failed to filter {typeof(T).Name} entities: {ex.Message}");
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Task.FromResult(Result<bool>.WithFailure("Predicate cannot be null"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<bool>());
        }

        return ResultTryExtensions.TryAsync(
            () => _dbSet.AnyAsync(predicate, cancellationToken),
            ex => $"Failed to determine if {typeof(T).Name} exists: {ex.Message}");
    }

    /// <inheritdoc />
    public Task<Result<int>> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<int>());
        }

        return ResultTryExtensions.TryAsync(
            () => predicate is null
                ? _dbSet.CountAsync(cancellationToken)
                : _dbSet.CountAsync(predicate, cancellationToken),
            ex => $"Failed to count {typeof(T).Name} entities: {ex.Message}");
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<T>>());
        }

        return ResultTryExtensions.TryAsync(
            async () =>
            {
                var list = await _dbSet.ToListAsync(cancellationToken).ConfigureAwait(false);
                return (IReadOnlyList<T>)list;
            },
            ex => $"Failed to list {typeof(T).Name} entities: {ex.Message}");
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Task.FromResult(Result<IReadOnlyList<T>>.WithFailure("Predicate cannot be null"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<T>>());
        }

        return ResultTryExtensions.TryAsync(
            async () =>
            {
                var list = await _dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
                return (IReadOnlyList<T>)list;
            },
            ex => $"Failed to list filtered {typeof(T).Name} entities: {ex.Message}");
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        if (specification is null)
        {
            return Task.FromResult(Result<IReadOnlyList<T>>.WithFailure("Specification cannot be null"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<T>>());
        }

        return ResultTryExtensions.TryAsync(
            async () =>
            {
                var query = SpecificationEvaluator<T>.GetQuery(_dbSet.AsQueryable(), specification);
                var data = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
                return (IReadOnlyList<T>)data;
            },
            ex => $"Failed to list {typeof(T).Name} entities by specification: {ex.Message}");
    }

    /// <inheritdoc />
    public async Task<Result<T?>> FirstOrDefaultAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        if (specification is null)
        {
            return Result<T?>.WithFailure("Specification cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<T?>();
        }

        var result = await ResultTryExtensions.TryAsync(
            () => SpecificationEvaluator<T>
                .GetQuery(_dbSet.AsQueryable(), specification)
                .FirstOrDefaultAsync(cancellationToken),
            ex => $"Failed to retrieve {typeof(T).Name} by specification: {ex.Message}");

        // ROP-compliant: "not found" is a failure case, not success with null
        if (result.IsSuccess && result.Value is null)
        {
            return Result<T?>.WithFailure($"No entity of type {typeof(T).Name} matching the specification was found");
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TResult>>> SelectAsync<TResult>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Task.FromResult(Result<IReadOnlyList<TResult>>.WithFailure("Predicate cannot be null"));
        }

        if (selector is null)
        {
            return Task.FromResult(Result<IReadOnlyList<TResult>>.WithFailure("Selector cannot be null"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<TResult>>());
        }

        return ResultTryExtensions.TryAsync(
            async () =>
            {
                var data = await _dbSet.Where(predicate)
                    .Select(selector)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return (IReadOnlyList<TResult>)data;
            },
            ex => $"Failed to project {typeof(T).Name} entities: {ex.Message}");
    }

    /// <inheritdoc />
    public async Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return Result.WithFailure("Entity cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var attempt = await ResultTryExtensions.TryAsync(
            async () =>
            {
                await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                return true;
            },
            ex => $"Failed to add {typeof(T).Name}: {ex.Message}");

        return attempt.Match(_ => Result.Success(), errors => Result.WithFailure(errors));
    }

    /// <inheritdoc />
    public async Task<Result> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (entities is null)
        {
            return Result.WithFailure("Entities collection cannot be null");
        }

        var list = entities.ToList();
        if (list.Count == 0)
        {
            return Result.WithFailure("Entities collection cannot be empty");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var attempt = await ResultTryExtensions.TryAsync(
            async () =>
            {
                await _dbContext.AddRangeAsync(list.Cast<object>().ToArray(), cancellationToken).ConfigureAwait(false);
                return true;
            },
            ex => $"Failed to add entities for {typeof(T).Name}: {ex.Message}");

        return attempt.Match(_ => Result.Success(), errors => Result.WithFailure(errors));
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return Result.WithFailure("Entity cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var attempt = await ResultTryExtensions.TryAsync(
            () =>
            {
                _dbSet.Update(entity);
                return Task.FromResult(true);
            },
            ex => $"Failed to update {typeof(T).Name}: {ex.Message}");

        return attempt.Match(_ => Result.Success(), errors => Result.WithFailure(errors));
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return Result.WithFailure("Entity cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var attempt = await ResultTryExtensions.TryAsync(
            () =>
            {
                _dbSet.Remove(entity);
                return Task.FromResult(true);
            },
            ex => $"Failed to remove {typeof(T).Name}: {ex.Message}");

        return attempt.Match(_ => Result.Success(), errors => Result.WithFailure(errors));
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (entities is null)
        {
            return Result.WithFailure("Entities collection cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var attempt = await ResultTryExtensions.TryAsync(
            () =>
            {
                _dbSet.RemoveRange(entities);
                return Task.FromResult(true);
            },
            ex => $"Failed to remove entities for {typeof(T).Name}: {ex.Message}");

        return attempt.Match(_ => Result.Success(), errors => Result.WithFailure(errors));
    }

    /// <inheritdoc />
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<int>());
        }

        return ResultTryExtensions.TryAsync(
            () => _dbContext.SaveChangesAsync(cancellationToken),
            ex => $"Failed to persist {typeof(T).Name} changes: {ex.Message}");
    }
}
