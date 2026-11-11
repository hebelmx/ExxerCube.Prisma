namespace ExxerCube.Prisma.Domain.Interfaces
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
        /// Retrieves a single entity by its identifier.
        /// </summary>
        /// <param name="id">Entity identifier to look for.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>
        /// A result that wraps the matching entity on success, or a failure result when the entity
        /// is not found (ROP-compliant: "not found" is treated as a failure case).
        /// </returns>
        Task<Result<T?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds all entities satisfying the supplied predicate.
        /// </summary>
        /// <param name="predicate">Filter to apply server-side.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result with the entities that match the filter.</returns>
        Task<Result<IReadOnlyList<T>>> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether any entity satisfies the provided predicate.
        /// </summary>
        /// <param name="predicate">Filter to evaluate.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result containing <c>true</c> when at least one entity matches; otherwise <c>false</c>.</returns>
        Task<Result<bool>> ExistsAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts how many entities meet the optional predicate constraint.
        /// </summary>
        /// <param name="predicate">Optional filter used before counting.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result containing the number of entities found.</returns>
        Task<Result<int>> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves every entity tracked by the repository.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result wrapping all entities.</returns>
        Task<Result<IReadOnlyList<T>>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the entities that satisfy the supplied predicate.
        /// </summary>
        /// <param name="predicate">Filter that narrows the returned set.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result with the filtered entities.</returns>
        Task<Result<IReadOnlyList<T>>> ListAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves entities that satisfy the provided specification (filters, ordering and includes).
        /// </summary>
        /// <param name="specification">Specification describing the query.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result with the entities that match the specification.</returns>
        Task<Result<IReadOnlyList<T>>> ListAsync(
            ISpecification<T> specification,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single entity that matches a specification.
        /// </summary>
        /// <param name="specification">Specification describing the query.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>
        /// A result with the entity on success, or a failure result when no entity matches
        /// (ROP-compliant: "not found" is treated as a failure case).
        /// </returns>
        Task<Result<T?>> FirstOrDefaultAsync(
            ISpecification<T> specification,
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
        /// <returns>A result wrapping the projected rows that satisfy the predicate.</returns>
        Task<Result<IReadOnlyList<TResult>>> SelectAsync<TResult>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TResult>> selector,
            CancellationToken cancellationToken = default);

        // ✏️ COMMANDS
        /// <summary>
        /// Adds a new entity instance to the underlying context.
        /// </summary>
        /// <param name="entity">Entity that needs to be staged for persistence.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result describing whether the entity was staged.</returns>
        Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple entities in a single batch to improve throughput.
        /// </summary>
        /// <param name="entities">Entities that should be staged for persistence.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result describing whether the entities were staged.</returns>
        Task<Result> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an existing entity as modified so changes are tracked.
        /// </summary>
        /// <param name="entity">Entity instance with updated values.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        Task<Result> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an entity instance from the persistence context.
        /// </summary>
        /// <param name="entity">Entity that should be deleted.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        Task<Result> RemoveAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple entities as a single operation.
        /// </summary>
        /// <param name="entities">Entities that should be deleted.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        Task<Result> RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists all pending changes tracked by the repository.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A result containing the number of state entries written to the data store.</returns>
        // 💾 UNIT OF WORK SUPPORT
        Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
