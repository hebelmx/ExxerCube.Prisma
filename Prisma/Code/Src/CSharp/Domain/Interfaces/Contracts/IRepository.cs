using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ExxerCube.Prisma.Domain.Interfaces.Contracts
{
    /// <summary>
    /// Provides the Prisma domain with a generic repository abstraction so aggregates can be
    /// queried and persisted without leaking infrastructure concerns.
    /// </summary>
    /// <typeparam name="T">The aggregate or entity type handled by the repository.</typeparam>
    /// <typeparam name="TId">The identifier type that uniquely represents an entity.</typeparam>
    public interface IRepository<T, in TId>
        where T : class
    {
        // 🔍 QUERIES

        /// <summary>
        /// Retrieves a single entity by its identifier, returning <c>null</c> when it
        /// does not exist in the backing store.
        /// </summary>
        /// <param name="id">Entity identifier to look for.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The matching entity or <c>null</c> if it is not found.</returns>
        Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds all entities satisfying the supplied predicate.
        /// </summary>
        /// <param name="predicate">Filter to apply server-side.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A read-only list with the entities that match the filter.</returns>
        Task<IReadOnlyList<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether any entity satisfies the provided predicate.
        /// </summary>
        /// <param name="predicate">Filter to evaluate.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns><c>true</c> when at least one entity matches; otherwise <c>false</c>.</returns>
        Task<bool> ExistsAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts how many entities meet the optional predicate constraint.
        /// </summary>
        /// <param name="predicate">Optional filter used before counting.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The number of entities found.</returns>
        Task<int> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves every entity tracked by the repository.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>All entities as a read-only list.</returns>
        Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the entities that satisfy the supplied predicate.
        /// </summary>
        /// <param name="predicate">Filter that narrows the returned set.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A filtered read-only list of entities.</returns>
        Task<IReadOnlyList<T>> ListAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        // 🧾 PROJECTIONS (for read-only DTOs, optional)
        /// <summary>
        /// Projects entities that match a predicate into read-only DTOs, allowing the data
        /// layer to perform the projection efficiently.
        /// </summary>
        /// <typeparam name="TResult">The shape of the projected records.</typeparam>
        /// <param name="predicate">Filter that determines the source rows.</param>
        /// <param name="selector">Selector describing the projection.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Projected results that satisfy the predicate.</returns>
        Task<IReadOnlyList<TResult>> SelectAsync<TResult>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TResult>> selector,
            CancellationToken cancellationToken = default);

        // ✏️ COMMANDS
        /// <summary>
        /// Adds a new entity instance to the underlying context.
        /// </summary>
        /// <param name="entity">Entity that needs to be staged for persistence.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A task that completes when the entity is staged.</returns>
        Task AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple entities in a single batch to improve throughput.
        /// </summary>
        /// <param name="entities">Entities that should be staged for persistence.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A task that completes when the entities are staged.</returns>
        Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an existing entity as modified so changes are tracked.
        /// </summary>
        /// <param name="entity">Entity instance with updated values.</param>
        void Update(T entity);

        /// <summary>
        /// Removes an entity instance from the persistence context.
        /// </summary>
        /// <param name="entity">Entity that should be deleted.</param>
        void Remove(T entity);

        /// <summary>
        /// Removes multiple entities as a single operation.
        /// </summary>
        /// <param name="entities">Entities that should be deleted.</param>
        void RemoveRange(IEnumerable<T> entities);

        /// <summary>
        /// Persists all pending changes tracked by the repository.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The number of state entries written to the data store.</returns>
        // 💾 UNIT OF WORK SUPPORT
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
