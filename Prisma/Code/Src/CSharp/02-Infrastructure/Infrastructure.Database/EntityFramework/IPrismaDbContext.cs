using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

/// <summary>
/// Represents the database context interface for the Prisma application.
/// This interface abstracts Entity Framework Core database operations and provides access
/// to entity sets for file metadata, personas, SLA statuses, review cases, review decisions, and audit records.
/// </summary>
public interface IPrismaDbContext
{
    /// <summary>
    /// Gets or sets the FileMetadata entity set.
    /// Represents metadata information for processed files in the system.
    /// </summary>
    DbSet<FileMetadata> FileMetadata { get; set; }

    /// <summary>
    /// Gets or sets the Persona entity set.
    /// Represents persona information extracted from processed documents.
    /// </summary>
    DbSet<Persona> Persona { get; set; }

    /// <summary>
    /// Gets or sets the SLAStatus entity set.
    /// Represents Service Level Agreement status tracking for review cases.
    /// </summary>
    DbSet<SLAStatus> SLAStatus { get; set; }

    /// <summary>
    /// Gets or sets the ReviewCases entity set.
    /// Represents review cases that are being processed or have been processed.
    /// </summary>
    DbSet<ReviewCase> ReviewCases { get; set; }

    /// <summary>
    /// Gets or sets the ReviewDecisions entity set.
    /// Represents decisions made during the review process for cases.
    /// </summary>
    DbSet<ReviewDecision> ReviewDecisions { get; set; }

    /// <summary>
    /// Gets or sets the AuditRecords entity set.
    /// Represents audit trail records for tracking changes and operations in the system.
    /// </summary>
    DbSet<AuditRecord> AuditRecords { get; set; }

    /// <summary>
    /// Gets the Database facade for accessing database-specific operations.
    /// Provides access to database-level functionality such as transactions and raw SQL execution.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Saves all changes made in this context to the database synchronously.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    int SaveChanges();

    /// <summary>
    /// Saves all changes made in this context to the database synchronously.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether to accept all changes on success. If true, the state of all tracked entities will be set to Unchanged after saving.</param>
    /// <returns>The number of state entries written to the database.</returns>
    int SaveChanges(bool acceptAllChangesOnSuccess);

    /// <summary>
    /// Saves all changes made in this context to the database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves all changes made in this context to the database asynchronously.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether to accept all changes on success. If true, the state of all tracked entities will be set to Unchanged after saving.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken);

    /// <summary>
    /// Releases all resources used by the database context synchronously.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Releases all resources used by the database context asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    ValueTask DisposeAsync();

    /// <summary>
    /// Gets an EntityEntry for the given entity, providing access to change tracking information and operations.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to get the entry for.</param>
    /// <returns>An EntityEntry for the given entity.</returns>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Gets an EntityEntry for the given entity, providing access to change tracking information and operations.
    /// </summary>
    /// <param name="entity">The entity to get the entry for.</param>
    /// <returns>An EntityEntry for the given entity.</returns>
    EntityEntry Entry(object entity);

    /// <summary>
    /// Begins tracking the given entity in the Added state, which will cause it to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Begins tracking the given entity in the Added state, which will cause it to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry Add(object entity);

    /// <summary>
    /// Begins tracking the given entity in the Added state asynchronously, which will cause it to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous add operation. The task result contains the EntityEntry for the entity.</returns>
    ValueTask<EntityEntry<TEntity>> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken) where TEntity : class;

    /// <summary>
    /// Begins tracking the given entity in the Added state asynchronously, which will cause it to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous add operation. The task result contains the EntityEntry for the entity.</returns>
    ValueTask<EntityEntry> AddAsync(object entity, CancellationToken cancellationToken);

    /// <summary>
    /// Begins tracking the given entity in the Unchanged state, which means it will not be inserted, updated, or deleted when SaveChanges is called.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to attach.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry<TEntity> Attach<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Begins tracking the given entity in the Unchanged state, which means it will not be inserted, updated, or deleted when SaveChanges is called.
    /// </summary>
    /// <param name="entity">The entity to attach.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry Attach(object entity);

    /// <summary>
    /// Begins tracking the given entity in the Modified state, which will cause it to be updated in the database when SaveChanges is called.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Begins tracking the given entity in the Modified state, which will cause it to be updated in the database when SaveChanges is called.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry Update(object entity);

    /// <summary>
    /// Begins tracking the given entity in the Deleted state, which will cause it to be deleted from the database when SaveChanges is called.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity to remove.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Begins tracking the given entity in the Deleted state, which will cause it to be deleted from the database when SaveChanges is called.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    /// <returns>The EntityEntry for the entity. The entry provides access to change tracking information and operations.</returns>
    EntityEntry Remove(object entity);

    /// <summary>
    /// Begins tracking multiple entities in the Added state, which will cause them to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    void AddRange(params object[] entities);

    /// <summary>
    /// Begins tracking multiple entities in the Added state, which will cause them to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    void AddRange(IEnumerable<object> entities);

    /// <summary>
    /// Begins tracking multiple entities in the Added state asynchronously, which will cause them to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    Task AddRangeAsync(params object[] entities);

    /// <summary>
    /// Begins tracking multiple entities in the Added state asynchronously, which will cause them to be inserted into the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    Task AddRangeAsync(IEnumerable<object> entities, CancellationToken cancellationToken);

    /// <summary>
    /// Begins tracking multiple entities in the Unchanged state, which means they will not be inserted, updated, or deleted when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to attach.</param>
    void AttachRange(params object[] entities);

    /// <summary>
    /// Begins tracking multiple entities in the Unchanged state, which means they will not be inserted, updated, or deleted when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to attach.</param>
    void AttachRange(IEnumerable<object> entities);

    /// <summary>
    /// Begins tracking multiple entities in the Modified state, which will cause them to be updated in the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    void UpdateRange(params object[] entities);

    /// <summary>
    /// Begins tracking multiple entities in the Modified state, which will cause them to be updated in the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    void UpdateRange(IEnumerable<object> entities);

    /// <summary>
    /// Begins tracking multiple entities in the Deleted state, which will cause them to be deleted from the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to remove.</param>
    void RemoveRange(params object[] entities);

    /// <summary>
    /// Begins tracking multiple entities in the Deleted state, which will cause them to be deleted from the database when SaveChanges is called.
    /// </summary>
    /// <param name="entities">The entities to remove.</param>
    void RemoveRange(IEnumerable<object> entities);

    /// <summary>
    /// Finds an entity with the given primary key values synchronously.
    /// </summary>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>The entity found, or null if no entity with the given primary key values exists in the context.</returns>
    object? Find(Type entityType, params object?[]? keyValues);

    /// <summary>
    /// Finds an entity with the given primary key values synchronously.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>The entity found, or null if no entity with the given primary key values exists in the context.</returns>
    TEntity? Find<TEntity>(params object?[]? keyValues) where TEntity : class;

    /// <summary>
    /// Finds an entity with the given primary key values asynchronously.
    /// </summary>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the entity found, or null if no entity with the given primary key values exists in the context.</returns>
    ValueTask<object?> FindAsync(Type entityType, params object?[]? keyValues);

    /// <summary>
    /// Finds an entity with the given primary key values asynchronously.
    /// </summary>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the entity found, or null if no entity with the given primary key values exists in the context.</returns>
    ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken);

    /// <summary>
    /// Finds an entity with the given primary key values asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the entity found, or null if no entity with the given primary key values exists in the context.</returns>
    ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues) where TEntity : class;

    /// <summary>
    /// Finds an entity with the given primary key values asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the entity found, or null if no entity with the given primary key values exists in the context.</returns>
    ValueTask<TEntity?> FindAsync<TEntity>(object?[]? keyValues, CancellationToken cancellationToken) where TEntity : class;
}