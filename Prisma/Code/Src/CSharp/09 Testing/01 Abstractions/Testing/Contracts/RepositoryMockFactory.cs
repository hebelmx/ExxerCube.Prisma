using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming reference fake that backs the blueprint instance of
/// <see cref="RepositoryContract{T, TId}"/>.
/// </summary>
/// <remarks>
/// The repository contract asserts real persistence outcomes (seed-then-query, filtered counts,
/// projection, save-then-list) which canned stubs cannot satisfy across calls. Following ADR-005 §6,
/// the factory is a <em>reference fake</em>: an in-memory store implementing the documented
/// <see cref="IRepository{T, TId}"/> semantics (the same validation/cancellation ordering the
/// production EF-backed <c>EfCoreRepository</c> uses). Each call returns a fresh, isolated store.
/// </remarks>
public static class RepositoryMockFactory
{
    /// <summary>
    /// Creates an <see cref="IRepository{T, TId}"/> reference fake that satisfies every test in
    /// <see cref="RepositoryContract{T, TId}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type the repository persists.</typeparam>
    /// <typeparam name="TId">The identifier type used to locate entities.</typeparam>
    /// <param name="idSelector">Extracts an entity's identifier (used by <c>GetByIdAsync</c>).</param>
    /// <returns>The configured reference fake backed by a fresh in-memory store.</returns>
    public static IRepository<T, TId> CreateContractConformingMock<T, TId>(Func<T, TId> idSelector)
        where T : class
        => new InMemoryReferenceRepository<T, TId>(idSelector);

    private sealed class InMemoryReferenceRepository<T, TId> : IRepository<T, TId>
        where T : class
    {
        private readonly List<T> _committed = new();
        private readonly List<T> _pending = new();
        private readonly Func<T, TId> _idSelector;

        public InMemoryReferenceRepository(Func<T, TId> idSelector)
            => _idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));

        public Task<Result<T?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<T?>());
            }

            if (id is null)
            {
                return Task.FromResult(Result<T?>.WithFailure("Identifier cannot be null"));
            }

            var match = _committed.Concat(_pending)
                .FirstOrDefault(e => EqualityComparer<TId>.Default.Equals(_idSelector(e), id));

            return Task.FromResult(match is null
                ? Result<T?>.WithFailure($"Entity with id {id} not found")
                : Result<T?>.Success(match));
        }

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

            IReadOnlyList<T> list = _committed.Where(predicate.Compile()).ToList();
            return Task.FromResult(Result<IReadOnlyList<T>>.Success(list));
        }

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

            return Task.FromResult(Result<bool>.Success(_committed.Any(predicate.Compile())));
        }

        public Task<Result<int>> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<int>());
            }

            var count = predicate is null
                ? _committed.Count
                : _committed.Count(predicate.Compile());

            return Task.FromResult(Result<int>.Success(count));
        }

        public Task<Result<IReadOnlyList<T>>> ListAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<T>>());
            }

            IReadOnlyList<T> list = _committed.ToList();
            return Task.FromResult(Result<IReadOnlyList<T>>.Success(list));
        }

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

            IReadOnlyList<T> list = _committed.Where(predicate.Compile()).ToList();
            return Task.FromResult(Result<IReadOnlyList<T>>.Success(list));
        }

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

            IReadOnlyList<T> list = ApplySpecification(specification).ToList();
            return Task.FromResult(Result<IReadOnlyList<T>>.Success(list));
        }

        public Task<Result<T?>> FirstOrDefaultAsync(
            ISpecification<T> specification,
            CancellationToken cancellationToken = default)
        {
            if (specification is null)
            {
                return Task.FromResult(Result<T?>.WithFailure("Specification cannot be null"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<T?>());
            }

            var match = ApplySpecification(specification).FirstOrDefault();

            return Task.FromResult(match is null
                ? Result<T?>.WithFailure("No entity matching the specification was found")
                : Result<T?>.Success(match));
        }

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

            IReadOnlyList<TResult> projected = _committed
                .Where(predicate.Compile())
                .Select(selector.Compile())
                .ToList();

            return Task.FromResult(Result<IReadOnlyList<TResult>>.Success(projected));
        }

        public Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is null)
            {
                return Task.FromResult(Result.WithFailure("Entity cannot be null"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            _pending.Add(entity);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities is null)
            {
                return Task.FromResult(Result.WithFailure("Entities collection cannot be null"));
            }

            var list = entities.ToList();
            if (list.Count == 0)
            {
                return Task.FromResult(Result.WithFailure("Entities collection cannot be empty"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            _pending.AddRange(list);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is null)
            {
                return Task.FromResult(Result.WithFailure("Entity cannot be null"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result> RemoveAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is null)
            {
                return Task.FromResult(Result.WithFailure("Entity cannot be null"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            _committed.Remove(entity);
            _pending.Remove(entity);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities is null)
            {
                return Task.FromResult(Result.WithFailure("Entities collection cannot be null"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            foreach (var entity in entities.ToList())
            {
                _committed.Remove(entity);
                _pending.Remove(entity);
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<int>());
            }

            var written = _pending.Count;
            _committed.AddRange(_pending);
            _pending.Clear();
            return Task.FromResult(Result<int>.Success(written));
        }

        private IEnumerable<T> ApplySpecification(ISpecification<T> specification)
        {
            IEnumerable<T> query = _committed;

            if (specification.Criteria is not null)
            {
                query = query.Where(specification.Criteria.Compile());
            }

            if (specification.OrderBy is not null)
            {
                query = query.OrderBy(specification.OrderBy.Compile());
            }
            else if (specification.OrderByDescending is not null)
            {
                query = query.OrderByDescending(specification.OrderByDescending.Compile());
            }

            if (specification.Skip.HasValue)
            {
                query = query.Skip(specification.Skip.Value);
            }

            if (specification.Take.HasValue)
            {
                query = query.Take(specification.Take.Value);
            }

            return query;
        }
    }
}
