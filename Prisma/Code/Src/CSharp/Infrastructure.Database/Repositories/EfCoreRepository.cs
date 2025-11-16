using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces.Contracts;
using IndQuestResults;
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

        ArgumentNullException.ThrowIfNull(id);

        try
        {
            var entity = await _dbSet.FindAsync(new object?[] { id }, cancellationToken).ConfigureAwait(false);
            return Result<T?>.Success(entity);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<T?>();
        }
        catch (Exception ex)
        {
            return Result<T?>.WithFailure(
                $"Failed to retrieve {typeof(T).Name} by id: {ex.Message}",
                default,
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<T>>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return await ExecuteQueryAsync(
            () => _dbSet.Where(predicate).ToListAsync(cancellationToken),
            cancellationToken,
            $"Failed to filter {typeof(T).Name} entities");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            var exists = await _dbSet.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
            return Result<bool>.Success(exists);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            return Result<bool>.WithFailure(
                $"Failed to determine if {typeof(T).Name} exists: {ex.Message}",
                default,
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<int>();
        }

        try
        {
            var count = predicate is null
                ? await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false)
                : await _dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);

            return Result<int>.Success(count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<int>();
        }
        catch (Exception ex)
        {
            return Result<int>.WithFailure(
                $"Failed to count {typeof(T).Name} entities: {ex.Message}",
                default,
                ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(CancellationToken cancellationToken = default)
        => ExecuteQueryAsync(() => _dbSet.ToListAsync(cancellationToken), cancellationToken, $"Failed to list {typeof(T).Name} entities");

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return ExecuteQueryAsync(
            () => _dbSet.Where(predicate).ToListAsync(cancellationToken),
            cancellationToken,
            $"Failed to list filtered {typeof(T).Name} entities");
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<T>>> ListAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return ExecuteQueryAsync(
            () => SpecificationEvaluator<T>
                .GetQuery(_dbSet.AsQueryable(), specification)
                .ToListAsync(cancellationToken),
            cancellationToken,
            $"Failed to list {typeof(T).Name} entities by specification");
    }

    /// <inheritdoc />
    public async Task<Result<T?>> FirstOrDefaultAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<T?>();
        }

        try
        {
            var entity = await SpecificationEvaluator<T>
                .GetQuery(_dbSet.AsQueryable(), specification)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<T?>.Success(entity);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<T?>();
        }
        catch (Exception ex)
        {
            return Result<T?>.WithFailure(
                $"Failed to retrieve {typeof(T).Name} by specification: {ex.Message}",
                default,
                ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TResult>>> SelectAsync<TResult>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(selector);

        return ExecuteQueryAsync(
            () => _dbSet.Where(predicate).Select(selector).ToListAsync(cancellationToken),
            cancellationToken,
            $"Failed to project {typeof(T).Name} entities");
    }

    /// <inheritdoc />
    public async Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (entity is null)
        {
            return Result.WithFailure("Entity cannot be null");
        }

        try
        {
            await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.WithFailure($"Failed to add {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        ArgumentNullException.ThrowIfNull(entities);
        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return Result.WithFailure("At least one entity is required");
        }

        try
        {
            await _dbSet.AddRangeAsync(entityList, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.WithFailure($"Failed to add entities for {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        if (entity is null)
        {
            return Task.FromResult(Result.WithFailure("Entity cannot be null"));
        }

        try
        {
            _dbSet.Update(entity);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.WithFailure($"Failed to update {typeof(T).Name}: {ex.Message}", ex));
        }
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        if (entity is null)
        {
            return Task.FromResult(Result.WithFailure("Entity cannot be null"));
        }

        try
        {
            _dbSet.Remove(entity);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.WithFailure($"Failed to remove {typeof(T).Name}: {ex.Message}", ex));
        }
    }

    /// <inheritdoc />
    public Task<Result> RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(entities);

        try
        {
            _dbSet.RemoveRange(entities);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.WithFailure($"Failed to remove entities for {typeof(T).Name}: {ex.Message}", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<int>();
        }

        try
        {
            var rows = await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<int>.Success(rows);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<int>();
        }
        catch (Exception ex)
        {
            return Result<int>.WithFailure(
                $"Failed to persist {typeof(T).Name} changes: {ex.Message}",
                default,
                ex);
        }
    }

    private static async Task<Result<IReadOnlyList<TItem>>> ExecuteQueryAsync<TItem>(
        Func<Task<IReadOnlyList<TItem>>> query,
        CancellationToken cancellationToken,
        string errorMessage)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<TItem>>();
        }

        try
        {
            var results = await query().ConfigureAwait(false);
            return Result<IReadOnlyList<TItem>>.Success(results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<TItem>>();
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TItem>>.WithFailure(
                $"{errorMessage}: {ex.Message}",
                default,
                ex);
        }
    }
}
