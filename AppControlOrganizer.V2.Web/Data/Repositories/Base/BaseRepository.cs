//-----------------------------------------------------------------------
// <copyright file="BaseRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories.Base;

using AppControlOrganizer.V2.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;

/// <summary>
/// Base repository class providing common data access methods.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TContext">The type of the database context.</typeparam>
public abstract class BaseRepository<TEntity, TContext> : IBaseRepository<TEntity>
  where TEntity : BaseEntity
  where TContext : DbContext
{
  protected IDbContextFactory<TContext> dbContextFactory;
  protected readonly ILogger? logger;
  private readonly int maxRetryAttempts;
  private readonly TimeSpan baseDelay;

  /// <summary>
  /// Initializes a new instance of the <see cref="BaseRepository{TEntity, TContext}"/> class.
  /// </summary>
  /// <param name="contextFactory">The context factory.</param>
  public BaseRepository(IDbContextFactory<TContext> contextFactory, ILogger? logger = null, AppControlOrganizer.V2.Web.Helpers.ConfigurationsValues? cfg = null)
  {
    dbContextFactory = contextFactory;
    this.logger = logger;

    // Read configuration for retries if provided, otherwise use defaults
    if (cfg != null)
    {
      if (!int.TryParse(cfg.RetryAttempts, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxRetryAttempts))
      {
        maxRetryAttempts = 3;
      }

      if (!double.TryParse(cfg.RetryBaseDelaySeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out double baseDelaySeconds))
      {
        baseDelaySeconds = 1.0;
      }

      baseDelay = TimeSpan.FromSeconds(baseDelaySeconds);
    }
    else
    {
      maxRetryAttempts = 3;
      baseDelay = TimeSpan.FromSeconds(1);
    }
  }

  /// <summary>
  /// Selects all entities.
  /// </summary>
  /// <returns>A collection of entities.</returns>
  public virtual async Task<ICollection<TEntity>> SelectAsync()
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TEntity> dataset = dbContext.Set<TEntity>();
      // Read-only operation: use AsNoTracking to avoid change-tracking overhead
      return await dataset.AsNoTracking().Where(e => !e.Deleted).ToListAsync();
    });
  }

  /// <summary>
  /// Creates the context asynchronously.
  /// </summary>
  /// <returns>The created context.</returns>
  protected virtual async Task<TContext> CreateContextAsync()
  {
    return await dbContextFactory.CreateDbContextAsync();
  }

  /// <summary>
  /// Returns an AsNoTracking, non-deleted queryable for the entity set.
  /// Centralises the repetitive <c>.AsNoTracking().Where(e =&gt; !e.Deleted)</c> pattern.
  /// </summary>
  /// <param name="dataset">The DbSet to filter.</param>
  protected static IQueryable<TEntity> NonDeletedOnly(DbSet<TEntity> dataset)
  {
    return dataset.AsNoTracking().Where(e => !e.Deleted);
  }

  /// <summary>
  /// Executes a repository operation with retry logic for transient connection errors.
  /// </summary>
  protected async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<TContext, Task<TResult>> operation, CancellationToken cancellationToken = default)
  {
    Exception? lastException = null;
    for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
    {
      try
      {
        using TContext dbContext = await CreateContextAsync();
        return await operation(dbContext);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (DbException ex) when (attempt < maxRetryAttempts)
      {
        lastException = ex;
        logger?.LogWarning(ex, "Transient database connection error (attempt {Attempt}/{Max}). Will retry after delay.", attempt, maxRetryAttempts);
        TimeSpan delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
        await Task.Delay(delay, cancellationToken);
        continue;
      }
      catch (TimeoutException ex) when (attempt < maxRetryAttempts)
      {
        lastException = ex;
        logger?.LogWarning(ex, "Timeout during DB operation (attempt {Attempt}/{Max}). Will retry after delay.", attempt, maxRetryAttempts);
        TimeSpan delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
        await Task.Delay(delay, cancellationToken);
        continue;
      }
      catch (Exception ex) when (attempt < maxRetryAttempts)
      {
        lastException = ex;
        logger?.LogWarning(ex, "Exception during DB operation (attempt {Attempt}/{Max}). Will retry after delay.", attempt, maxRetryAttempts);
        TimeSpan delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
        await Task.Delay(delay, cancellationToken);
        continue;
      }
      catch (Exception ex)
      {
        lastException = ex;
        logger?.LogError(ex, "Final failure in DB operation after {Attempt}/{Max} attempts.", attempt, maxRetryAttempts);
        break;
      }
    }
    if (lastException != null)
    {
      ExceptionDispatchInfo.Capture(lastException).Throw();
    }
    throw new InvalidOperationException("ExecuteWithRetryAsync failed without capturing an exception.");
  }

  /// <summary>
  /// Selects an entity by its identifier.
  /// </summary>
  /// <param name="id">The identifier.</param>
  /// <returns>The selected entity.</returns>
  public virtual async Task<TEntity?> SelectAsync(long id)
  {
#pragma warning disable CS8603 // Possible null reference return.
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    // Single-entity read: no tracking required
    return await dataset.AsNoTracking().Where(e => !e.Deleted).SingleOrDefaultAsync(p => p.Id == id);
#pragma warning restore CS8603 // Possible null reference return.
  }

  /// <summary>
  /// Selects entities based on a condition.
  /// </summary>
  /// <param name="condition">The condition.</param>
  /// <returns>A collection of entities.</returns>
  public virtual async Task<ICollection<TEntity>?> SelectAsync(Expression<Func<TEntity, bool>> condition)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    // Read-only filtered query
    return await dataset.AsNoTracking().Where(e => !e.Deleted).Where(condition).ToListAsync<TEntity>();
  }

  /// <summary>
  /// Asynchronously retrieves a single entity that matches the specified condition and is not marked as deleted.
  /// </summary>
  /// <remarks>Entities marked as deleted are excluded from the results. The method returns the first matching
  /// entity or <see langword="null"/> if none exist.</remarks>
  /// <param name="condition">An expression that defines the criteria used to select the entity. Only entities for which this condition evaluates
  /// to <see langword="true"/> will be considered.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the first entity that matches the
  /// condition and is not deleted, or <see langword="null"/> if no such entity is found.</returns>
  public virtual async Task<TEntity?> SelectOneAsync(Expression<Func<TEntity, bool>> condition, CancellationToken cancellationToken)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    return await dataset
      .AsNoTracking()
      .Where(e => !e.Deleted)
      .Where(condition)
      .FirstOrDefaultAsync(cancellationToken);
  }

  /// <summary>
  /// Asynchronously counts the number of entities that are not marked as deleted and optionally match a specified
  /// condition.
  /// </summary>
  /// <remarks>Entities are only counted if they are not marked as deleted. This method executes a database query
  /// and may incur network or I/O latency.</remarks>
  /// <param name="condition">An optional expression used to filter entities. Only entities that satisfy this condition are counted. If null, all
  /// entities not marked as deleted are counted.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the number of entities that match the
  /// specified condition and are not marked as deleted.</returns>
  public virtual async Task<int> SelectCountAsync(CancellationToken cancellationToken, Expression<Func<TEntity, bool>>? condition = null)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    int result = 0;

    if (condition != null)
    {
      result = await dataset.Where(e => !e.Deleted).Where(condition).Select(e => e.Id).CountAsync<int>(cancellationToken);
    }
    else
    {
      result = await dataset.Where(e => !e.Deleted).Select(e => e.Id).CountAsync<int>(cancellationToken);
    }

    return result;
  }

  /// <summary>
  /// Asynchronously returns the first entity that is not marked as deleted, or null if no such entity exists.
  /// </summary>
  /// <remarks>Entities are considered non-deleted if their Deleted property is false. This method queries the
  /// underlying data source and does not return entities marked as deleted.</remarks>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the first non-deleted entity, or null
  /// if no matching entity is found.</returns>
  public virtual async Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    // Read-only: avoid change tracking
    TEntity? result = await dataset.AsNoTracking().Where(e => !e.Deleted).FirstOrDefaultAsync(cancellationToken);

    return result;
  }

  /// <summary>
  /// Prepares an entity to be saved.
  /// </summary>
  /// <typeparam name="TEntityGen">The type of the entity.</typeparam>
  /// <param name="context">The database context.</param>
  /// <param name="entity">The entity to prepare.</param>
  /// <returns>The prepared entity.</returns>
  protected virtual TEntityGen PrepareEntityToSave<TEntityGen>(TContext context, TEntityGen entity) where TEntityGen : BaseEntity
  {
    context.Entry(entity);
    return entity;
  }

  /// <summary>
  /// Prepares an entity to be saved.
  /// </summary>
  /// <typeparam name="TEntityGen">The type of the entity.</typeparam>
  /// <param name="context">The database context.</param>
  /// <param name="newEntity">The new entity.</param>
  /// <param name="oldEntity">The old entity.</param>
  /// <returns>The prepared entity.</returns>
  protected virtual TEntityGen PrepareEntityToSave<TEntityGen>(TContext context, TEntityGen newEntity, TEntityGen oldEntity) where TEntityGen : BaseEntity
  {
    newEntity.Id = oldEntity.Id;
    context.Entry(oldEntity).CurrentValues.SetValues(newEntity);
    return newEntity;
  }

  /// <summary>
  /// Updates an entity.
  /// </summary>
  /// <param name="item">The entity to update.</param>
  /// <returns>The updated entity.</returns>
  public virtual async Task<TEntity> UpdateAsync(TEntity item)
  {
    using TContext datacontext = await CreateContextAsync();
    DbSet<TEntity> dataset = datacontext.Set<TEntity>();
    TEntity? result = await dataset.SingleOrDefaultAsync(p => p.Id.Equals(item.Id));
    if (result == null)
    {
      return item;
    }

    item.CreationDate = result.CreationDate;

    PrepareEntityToSave(datacontext, item, result);
    await datacontext.SaveChangesAsync();

    return item;
  }

  /// <summary>
  /// Selects entities based on a condition with pagination.
  /// </summary>
  /// <param name="condition">The condition.</param>
  /// <param name="pageNumber">The page number.</param>
  /// <param name="pageSize">The page size.</param>
  /// <returns>A collection of entities.</returns>
  public virtual async Task<ICollection<TEntity>> SelectAsync(Expression<Func<TEntity, bool>> condition, int pageNumber, int pageSize)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    // Paginated, read-only query
    ICollection<TEntity> result = await dataset.AsNoTracking().Where(e => !e.Deleted).Where(condition)
      .Skip((pageNumber - 1) * pageSize).Take(pageSize)
      .ToListAsync<TEntity>();

    return result;
  }

  /// <summary>
  /// Returns true if a non-deleted entity with the given <paramref name="id"/> exists.
  /// Executes an AnyAsync query — no entity is loaded, no navigation properties are fetched.
  /// </summary>
  public virtual async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    return await dataset.AsNoTracking().AnyAsync(e => !e.Deleted && e.Id == id, cancellationToken);
  }

  /// <summary>
  /// Marks an entity for deletion.
  /// </summary>
  /// <param name="id">The identifier of the entity to delete.</param>
  /// <returns>The deleted entity.</returns>
  public virtual async Task<TEntity> DeleteAsync(long id)
  {
    using TContext dbContext = await CreateContextAsync();
    DbSet<TEntity> dataset = dbContext.Set<TEntity>();
    TEntity? entity = await dataset.SingleOrDefaultAsync(p => p.Id == id);
    if (entity == null)
    {
      return Activator.CreateInstance<TEntity>();
    }

    // Soft delete convention used across the project
    entity.Deleted = true;
    entity.Active = false;

    await dbContext.SaveChangesAsync();

    return entity;
  }

  /// <summary>
  /// Asynchronously inserts a new entity of type TEntity into the database using Entity Framework.
  /// </summary>
  /// <remarks>After the entity is inserted, the generated identifier is assigned to the entity's Id property
  /// by EF Core identity generation.</remarks>
  /// <param name="entity">The entity to insert. This parameter cannot be null and must have valid property values for insertion.</param>
  /// <returns>A task that represents the asynchronous insert operation.</returns>
  public virtual async Task InsertAsync(TEntity entity)
  {
    using TContext context = await dbContextFactory.CreateDbContextAsync();
    DbSet<TEntity> dataset = context.Set<TEntity>();
    await dataset.AddAsync(entity);
    await context.SaveChangesAsync();
  }
}
