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

	/// <summary>
	/// Initializes a new instance of the <see cref="Repository{TEntity}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context to use for operations.</param>
	/// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
	public Repository(IPrismaDbContext dbContext)
	{
		_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
		_dbSet = GetDbSet();
	}

	/// <summary>
	/// Gets the DbSet for the entity type from the database context.
	/// </summary>
	/// <returns>The DbSet for the entity type.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the DbSet cannot be found for the entity type.</exception>
	private DbSet<TEntity> GetDbSet()
	{
		var property = typeof(IPrismaDbContext)
			.GetProperties()
			.FirstOrDefault(p => p.PropertyType == typeof(DbSet<TEntity>));

		if (property == null)
		{
			throw new InvalidOperationException(
				$"DbSet<{typeof(TEntity).Name}> not found in IPrismaDbContext. " +
				$"Ensure the entity is registered in the database context.");
		}

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
		if (entity == null)
			throw new ArgumentNullException(nameof(entity));

		return _dbContext.Entry(entity);
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<EntityEntry<TEntity>>();

		if (entity == null)
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");

		try
		{
			var entry = await _dbContext.AddAsync(entity, cancellationToken).ConfigureAwait(false);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<EntityEntry<TEntity>>();
		}
		catch (Exception ex)
		{
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled();

		if (entities == null)
			return Result.WithFailure("Entities collection cannot be null");

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
				return Result.WithFailure("Entities collection cannot be empty");

			await _dbContext.AddRangeAsync(entityList.Cast<object>().ToArray(), cancellationToken).ConfigureAwait(false);
			return Result.Success();
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled();
		}
		catch (Exception ex)
		{
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
		if (entity == null)
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");

		try
		{
			var entry = _dbContext.Attach(entity);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
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
		if (entities == null)
			return Result.WithFailure("Entities collection cannot be null");

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
				return Result.WithFailure("Entities collection cannot be empty");

			_dbContext.AttachRange(entityList.Cast<object>().ToArray());
			return Result.Success();
		}
		catch (Exception ex)
		{
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
		if (entity == null)
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");

		try
		{
			var entry = _dbContext.Update(entity);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
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
		if (entities == null)
			return Result.WithFailure("Entities collection cannot be null");

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
				return Result.WithFailure("Entities collection cannot be empty");

			_dbContext.UpdateRange(entityList.Cast<object>().ToArray());
			return Result.Success();
		}
		catch (Exception ex)
		{
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
		if (entity == null)
			return Result<EntityEntry<TEntity>>.WithFailure("Entity cannot be null");

		try
		{
			var entry = _dbContext.Remove(entity);
			return Result<EntityEntry<TEntity>>.Success(entry);
		}
		catch (Exception ex)
		{
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
		if (entities == null)
			return Result.WithFailure("Entities collection cannot be null");

		try
		{
			var entityList = entities.ToList();
			if (!entityList.Any())
				return Result.WithFailure("Entities collection cannot be empty");

			_dbContext.RemoveRange(entityList.Cast<object>().ToArray());
			return Result.Success();
		}
		catch (Exception ex)
		{
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
		if (keyValues == null || keyValues.Length == 0)
			return Result<TEntity?>.WithFailure("Key values cannot be null or empty");

		try
		{
			var entity = _dbContext.Find<TEntity>(keyValues);
			return Result<TEntity?>.Success(entity);
		}
		catch (Exception ex)
		{
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<TEntity?>();

		if (keyValues == null || keyValues.Length == 0)
			return Result<TEntity?>.WithFailure("Key values cannot be null or empty");

		try
		{
			var entity = await _dbContext.FindAsync<TEntity>(keyValues, cancellationToken).ConfigureAwait(false);
			return Result<TEntity?>.Success(entity);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<TEntity?>();
		}
		catch (Exception ex)
		{
			return Result<TEntity?>.WithFailure($"Failed to find entity: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets a queryable collection of entities that can be used to build LINQ queries.
	/// </summary>
	/// <returns>A queryable collection of entities.</returns>
	public IQueryable<TEntity> GetQueryable()
	{
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
		if (predicate == null)
			throw new ArgumentNullException(nameof(predicate));

		return _dbSet.Where(predicate);
	}

	/// <summary>
	/// Checks if any entities exist in the database.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether any entities exist.</returns>
	public async Task<Result<bool>> AnyAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<bool>();

		try
		{
			var exists = await _dbSet.AnyAsync(cancellationToken).ConfigureAwait(false);
			return Result<bool>.Success(exists);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<bool>();
		}
		catch (Exception ex)
		{
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<bool>();

		if (predicate == null)
			return Result<bool>.WithFailure("Predicate cannot be null");

		try
		{
			var exists = await _dbSet.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
			return Result<bool>.Success(exists);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<bool>();
		}
		catch (Exception ex)
		{
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<int>();

		try
		{
			var count = await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false);
			return Result<int>.Success(count);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<int>();
		}
		catch (Exception ex)
		{
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
		if (cancellationToken.IsCancellationRequested)
			return ResultExtensions.Cancelled<int>();

		if (predicate == null)
			return Result<int>.WithFailure("Predicate cannot be null");

		try
		{
			var count = await _dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
			return Result<int>.Success(count);
		}
		catch (OperationCanceledException)
		{
			return ResultExtensions.Cancelled<int>();
		}
		catch (Exception ex)
		{
			return Result<int>.WithFailure($"Failed to count entities matching predicate: {ex.Message}");
		}
	}
}
