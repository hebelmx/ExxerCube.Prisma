using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

/// <summary>
/// Generic repository implementation for Entity Framework Core operations.
/// Provides a wrapper around DbContext methods with Result-based error handling.
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository manages.</typeparam>
public class Repository<TEntity> where TEntity : class
{
	private readonly IPrismaDbContext _dbContext;
	private readonly DbSet<TEntity> _dbSet;
	private readonly ILogger<Repository<TEntity>> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="Repository{TEntity}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context to use for operations.</param>
	/// <param name="logger">The logger instance for this repository.</param>
	/// <exception cref="ArgumentNullException">Thrown when dbContext or logger is null.</exception>
	public Repository(IPrismaDbContext dbContext, ILogger<Repository<TEntity>> logger)
	{
		_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dbSet = GetDbSet();
		_logger.LogDebug("Repository initialized for entity type: {EntityType}", typeof(TEntity).Name);
	}

	/// <summary>
	/// Gets the DbSet for the entity type from the database context.
	/// </summary>
	/// <returns>The DbSet for the entity type.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the DbSet cannot be found for the entity type.</exception>
	private DbSet<TEntity> GetDbSet()
	{
		_logger.LogDebug("Retrieving DbSet for entity type: {EntityType}", typeof(TEntity).Name);

		var property = typeof(IPrismaDbContext)
			.GetProperties()
			.FirstOrDefault(p => p.PropertyType == typeof(DbSet<TEntity>));

		if (property == null)
		{
			_logger.LogError(
				"DbSet not found for entity type {EntityType} in IPrismaDbContext. Ensure the entity is registered in the database context.",
				typeof(TEntity).Name);
			throw new InvalidOperationException(
				$"DbSet<{typeof(TEntity).Name}> not found in IPrismaDbContext. " +
				$"Ensure the entity is registered in the database context.");
		}

		_logger.LogDebug("Successfully retrieved DbSet for entity type: {EntityType}", typeof(TEntity).Name);
		return (DbSet<TEntity>)property.GetValue(_dbContext)!;
	}

	/// <summary>
	/// Gets an EntityEntry for the given entity, providing access to change tracking information and operations.
	/// </summary>
	/// <param name="entity">The entity to get the entry for.</param>
	/// <returns>An EntityEntry for the given entity.</returns>
	/// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
	public EntityEntry<TEntity> GetEntry(TEntity entity)
	{
		_logger.LogDebug("Getting EntityEntry for entity type: {EntityType}", typeof(TEntity).Name);

		if (entity == null)
		{
			_logger.LogWarning("GetEntry called with null entity for type: {EntityType}", typeof(TEntity).Name);
			throw new ArgumentNullException(nameof(entity));
		}

		var entry = _dbContext.Entry(entity);
		_logger.LogDebug("Successfully retrieved EntityEntry for entity type: {EntityType}, State: {State}",
			typeof(TEntity).Name, entry.State);
		return entry;
	}

	/// <summary>
	/// Begins tracking the given entity in the Added state, which will cause it to be inserted into the database when SaveChanges is called.
	/// </summary>
	/// <param name="entity">The entity to add.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A result containing the EntityEntry for the entity or an error.</returns>
	public async Task<Result<EntityEntry<TEntity>>> AddAsync(
		TEntity entity,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Adding entity of type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("AddAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<EntityEntry<TEntity>>();
		}

		if (entity == null)
		{
			_logger.LogWarning("AddAsync called with null entity for type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");
		}

		try
		{
			var entry = await _dbContext.AddAsync(entity, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Successfully added entity of type: {EntityType}, State: {State}",
				typeof(TEntity).Name, entry.State);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("AddAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<EntityEntry<TEntity>>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to add entity of type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure($"Failed to add entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking multiple entities in the Added state, which will cause them to be inserted into the database when SaveChanges is called.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A result indicating success or failure.</returns>
	public async Task<Result> AddRangeAsync(
		IEnumerable<TEntity> entities,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Adding range of entities of type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("AddRangeAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled();
		}

		if (entities == null)
		{
			_logger.LogWarning("AddRangeAsync called with null entities collection for type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure("Entities collection cannot be null");
		}

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
			{
				_logger.LogWarning("AddRangeAsync called with empty entities collection for type: {EntityType}", typeof(TEntity).Name);
				return Result.WithFailure("Entities collection cannot be empty");
			}

			_logger.LogDebug("Adding {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			await _dbContext.AddRangeAsync(entityList.Cast<object>().ToArray(), cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Successfully added {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			return Result.Success();
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("AddRangeAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to add range of entities of type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure($"Failed to add entities: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking the given entity in the Unchanged state, which means it will not be inserted, updated, or deleted when SaveChanges is called.
	/// </summary>
	/// <param name="entity">The entity to attach.</param>
	/// <returns>A result containing the EntityEntry for the entity or an error.</returns>
	public Result<EntityEntry<TEntity>> Attach(TEntity entity)
	{
		_logger.LogDebug("Attaching entity of type: {EntityType}", typeof(TEntity).Name);

		if (entity == null)
		{
			_logger.LogWarning("Attach called with null entity for type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");
		}

		try
		{
			var entry = _dbContext.Attach(entity);
			_logger.LogInformation("Successfully attached entity of type: {EntityType}, State: {State}",
				typeof(TEntity).Name, entry.State);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to attach entity of type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure($"Failed to attach entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking multiple entities in the Unchanged state, which means they will not be inserted, updated, or deleted when SaveChanges is called.
	/// </summary>
	/// <param name="entities">The entities to attach.</param>
	/// <returns>A result indicating success or failure.</returns>
	public Result AttachRange(IEnumerable<TEntity> entities)
	{
		_logger.LogDebug("Attaching range of entities of type: {EntityType}", typeof(TEntity).Name);

		if (entities == null)
		{
			_logger.LogWarning("AttachRange called with null entities collection for type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure("Entities collection cannot be null");
		}

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
			{
				_logger.LogWarning("AttachRange called with empty entities collection for type: {EntityType}", typeof(TEntity).Name);
				return Result.WithFailure("Entities collection cannot be empty");
			}

			_logger.LogDebug("Attaching {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			_dbContext.AttachRange(entityList.Cast<object>().ToArray());
			_logger.LogInformation("Successfully attached {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			return Result.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to attach range of entities of type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure($"Failed to attach entities: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking the given entity in the Modified state, which will cause it to be updated in the database when SaveChanges is called.
	/// </summary>
	/// <param name="entity">The entity to update.</param>
	/// <returns>A result containing the EntityEntry for the entity or an error.</returns>
	public Result<EntityEntry<TEntity>> Update(TEntity entity)
	{
		_logger.LogDebug("Updating entity of type: {EntityType}", typeof(TEntity).Name);

		if (entity == null)
		{
			_logger.LogWarning("Update called with null entity for type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");
		}

		try
		{
			var entry = _dbContext.Update(entity);
			_logger.LogInformation("Successfully updated entity of type: {EntityType}, State: {State}",
				typeof(TEntity).Name, entry.State);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update entity of type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure($"Failed to update entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking multiple entities in the Modified state, which will cause them to be updated in the database when SaveChanges is called.
	/// </summary>
	/// <param name="entities">The entities to update.</param>
	/// <returns>A result indicating success or failure.</returns>
	public Result UpdateRange(IEnumerable<TEntity> entities)
	{
		_logger.LogDebug("Updating range of entities of type: {EntityType}", typeof(TEntity).Name);

		if (entities == null)
		{
			_logger.LogWarning("UpdateRange called with null entities collection for type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure("Entities collection cannot be null");
		}

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
			{
				_logger.LogWarning("UpdateRange called with empty entities collection for type: {EntityType}", typeof(TEntity).Name);
				return Result.WithFailure("Entities collection cannot be empty");
			}

			_logger.LogDebug("Updating {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			_dbContext.UpdateRange(entityList.Cast<object>().ToArray());
			_logger.LogInformation("Successfully updated {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			return Result.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update range of entities of type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure($"Failed to update entities: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking the given entity in the Deleted state, which will cause it to be deleted from the database when SaveChanges is called.
	/// </summary>
	/// <param name="entity">The entity to remove.</param>
	/// <returns>A result containing the EntityEntry for the entity or an error.</returns>
	public Result<EntityEntry<TEntity>> Remove(TEntity entity)
	{
		_logger.LogDebug("Removing entity of type: {EntityType}", typeof(TEntity).Name);

		if (entity == null)
		{
			_logger.LogWarning("Remove called with null entity for type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");
		}

		try
		{
			var entry = _dbContext.Remove(entity);
			_logger.LogInformation("Successfully removed entity of type: {EntityType}, State: {State}",
				typeof(TEntity).Name, entry.State);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to remove entity of type: {EntityType}", typeof(TEntity).Name);
			return Result<EntityEntry<TEntity>>.WithFailure($"Failed to remove entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Begins tracking multiple entities in the Deleted state, which will cause them to be deleted from the database when SaveChanges is called.
	/// </summary>
	/// <param name="entities">The entities to remove.</param>
	/// <returns>A result indicating success or failure.</returns>
	public Result RemoveRange(IEnumerable<TEntity> entities)
	{
		_logger.LogDebug("Removing range of entities of type: {EntityType}", typeof(TEntity).Name);

		if (entities == null)
		{
			_logger.LogWarning("RemoveRange called with null entities collection for type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure("Entities collection cannot be null");
		}

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
			{
				_logger.LogWarning("RemoveRange called with empty entities collection for type: {EntityType}", typeof(TEntity).Name);
				return Result.WithFailure("Entities collection cannot be empty");
			}

			_logger.LogDebug("Removing {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			_dbContext.RemoveRange(entityList.Cast<object>().ToArray());
			_logger.LogInformation("Successfully removed {Count} entities of type: {EntityType}", entityList.Count, typeof(TEntity).Name);
			return Result.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to remove range of entities of type: {EntityType}", typeof(TEntity).Name);
			return Result.WithFailure($"Failed to remove entities: {ex.Message}");
		}
	}

	/// <summary>
	/// Finds an entity with the given primary key values synchronously.
	/// </summary>
	/// <param name="keyValues">The values of the primary key for the entity to be found.</param>
	/// <returns>A result containing the entity found, or null if no entity with the given primary key values exists in the context.</returns>
	public Result<TEntity?> Find(params object?[]? keyValues)
	{
		_logger.LogDebug("Finding entity of type: {EntityType} with key values", typeof(TEntity).Name);

		if (keyValues == null || keyValues.Length == 0)
		{
			_logger.LogWarning("Find called with null or empty key values for entity type: {EntityType}", typeof(TEntity).Name);
			return Result<TEntity?>.WithFailure("Key values cannot be null or empty");
		}

		try
		{
			var entity = _dbContext.Find<TEntity>(keyValues);
			if (entity == null)
			{
				_logger.LogDebug("Entity not found for type: {EntityType} with provided key values", typeof(TEntity).Name);
			}
			else
			{
				_logger.LogDebug("Successfully found entity of type: {EntityType}", typeof(TEntity).Name);
			}
			return Result<TEntity?>.Success(entity);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to find entity of type: {EntityType}", typeof(TEntity).Name);
			return Result<TEntity?>.WithFailure($"Failed to find entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Finds an entity with the given primary key values asynchronously.
	/// </summary>
	/// <param name="keyValues">The values of the primary key for the entity to be found.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous find operation. The task result contains the entity found, or null if no entity with the given primary key values exists in the context.</returns>
	public async Task<Result<TEntity?>> FindAsync(
		object?[]? keyValues,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Finding entity asynchronously of type: {EntityType} with key values", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("FindAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<TEntity?>();
		}

		if (keyValues == null || keyValues.Length == 0)
		{
			_logger.LogWarning("FindAsync called with null or empty key values for entity type: {EntityType}", typeof(TEntity).Name);
			return Result<TEntity?>.WithFailure("Key values cannot be null or empty");
		}

		try
		{
			var entity = await _dbContext.FindAsync<TEntity>(keyValues, cancellationToken).ConfigureAwait(false);
			if (entity == null)
			{
				_logger.LogDebug("Entity not found asynchronously for type: {EntityType} with provided key values", typeof(TEntity).Name);
			}
			else
			{
				_logger.LogDebug("Successfully found entity asynchronously of type: {EntityType}", typeof(TEntity).Name);
			}
			return Result<TEntity?>.Success(entity);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("FindAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<TEntity?>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to find entity asynchronously of type: {EntityType}", typeof(TEntity).Name);
			return Result<TEntity?>.WithFailure($"Failed to find entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets a queryable collection of entities that can be used to build LINQ queries.
	/// </summary>
	/// <returns>A queryable collection of entities.</returns>
	public IQueryable<TEntity> GetQueryable()
	{
		_logger.LogDebug("Getting queryable collection for entity type: {EntityType}", typeof(TEntity).Name);
		return _dbSet;
	}

	/// <summary>
	/// Gets a queryable collection of entities filtered by the specified predicate.
	/// </summary>
	/// <param name="predicate">The predicate to filter entities.</param>
	/// <returns>A queryable collection of entities matching the predicate.</returns>
	/// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
	public IQueryable<TEntity> GetQueryable(Expression<Func<TEntity, bool>> predicate)
	{
		_logger.LogDebug("Getting filtered queryable collection for entity type: {EntityType}", typeof(TEntity).Name);

		if (predicate == null)
		{
			_logger.LogWarning("GetQueryable called with null predicate for entity type: {EntityType}", typeof(TEntity).Name);
			throw new ArgumentNullException(nameof(predicate));
		}

		return _dbSet.Where(predicate);
	}

	/// <summary>
	/// Checks if any entities exist in the database.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether any entities exist.</returns>
	public async Task<Result<bool>> AnyAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Checking if any entities exist for type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("AnyAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<bool>();
		}

		try
		{
			var exists = await _dbSet.AnyAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("AnyAsync result for entity type: {EntityType}, Exists: {Exists}", typeof(TEntity).Name, exists);
			return Result<bool>.Success(exists);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("AnyAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<bool>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if entities exist for type: {EntityType}", typeof(TEntity).Name);
			return Result<bool>.WithFailure($"Failed to check if entities exist: {ex.Message}");
		}
	}

	/// <summary>
	/// Checks if any entities match the specified predicate.
	/// </summary>
	/// <param name="predicate">The predicate to test entities against.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether any entities match the predicate.</returns>
	/// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
	public async Task<Result<bool>> AnyAsync(
		Expression<Func<TEntity, bool>> predicate,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Checking if any entities match predicate for type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("AnyAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<bool>();
		}

		if (predicate == null)
		{
			_logger.LogWarning("AnyAsync called with null predicate for entity type: {EntityType}", typeof(TEntity).Name);
			return Result<bool>.WithFailure("Predicate cannot be null");
		}

		try
		{
			var exists = await _dbSet.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("AnyAsync result for entity type: {EntityType}, Exists: {Exists}", typeof(TEntity).Name, exists);
			return Result<bool>.Success(exists);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("AnyAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<bool>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if entities match predicate for type: {EntityType}", typeof(TEntity).Name);
			return Result<bool>.WithFailure($"Failed to check if entities match predicate: {ex.Message}");
		}
	}

	/// <summary>
	/// Counts the total number of entities in the database.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the count of entities.</returns>
	public async Task<Result<int>> CountAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Counting entities for type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("CountAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<int>();
		}

		try
		{
			var count = await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("CountAsync result for entity type: {EntityType}, Count: {Count}", typeof(TEntity).Name, count);
			return Result<int>.Success(count);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("CountAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<int>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to count entities for type: {EntityType}", typeof(TEntity).Name);
			return Result<int>.WithFailure($"Failed to count entities: {ex.Message}");
		}
	}

	/// <summary>
	/// Counts the number of entities that match the specified predicate.
	/// </summary>
	/// <param name="predicate">The predicate to test entities against.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the count of matching entities.</returns>
	/// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
	public async Task<Result<int>> CountAsync(
		Expression<Func<TEntity, bool>> predicate,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Counting entities matching predicate for type: {EntityType}", typeof(TEntity).Name);

		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("CountAsync operation cancelled before starting for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<int>();
		}

		if (predicate == null)
		{
			_logger.LogWarning("CountAsync called with null predicate for entity type: {EntityType}", typeof(TEntity).Name);
			return Result<int>.WithFailure("Predicate cannot be null");
		}

		try
		{
			var count = await _dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("CountAsync result for entity type: {EntityType}, Count: {Count}", typeof(TEntity).Name, count);
			return Result<int>.Success(count);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("CountAsync operation cancelled for entity type: {EntityType}", typeof(TEntity).Name);
			return ResultExtensions.Cancelled<int>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to count entities matching predicate for type: {EntityType}", typeof(TEntity).Name);
			return Result<int>.WithFailure($"Failed to count entities matching predicate: {ex.Message}");
		}
	}
}
