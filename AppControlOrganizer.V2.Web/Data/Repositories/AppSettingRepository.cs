//-----------------------------------------------------------------------
// <copyright file="AppSettingRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.ExceptionServices;

/// <summary>
/// Provides data access operations for application settings.
/// <para>
/// All public methods are protected by a private exponential-backoff retry helper
/// that mirrors the pattern used in <see cref="Repositories.Base.BaseRepository{TEntity,TContext}"/>
/// (AppSetting does not extend BaseEntity so it cannot inherit that class directly).
/// </para>
/// <para>
/// Individual settings are cached per key (cache key pattern: <c>"AppSetting.{key}"</c>, TTL 60 minutes).
/// The cache is invalidated for a specific key whenever that setting is updated or inserted.
/// </para>
/// </summary>
public class AppSettingRepository : IAppSettingRepository
{
  private const int MaxRetryAttempts = 3;
  private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly IMemoryCache memoryCache;

  /// <summary>
  /// Initializes a new instance of the AppSettingRepository class.
  /// </summary>
  public AppSettingRepository(IDbContextFactory<LifePlannerContext> contextFactory, IMemoryCache memoryCache)
  {
    this.contextFactory = contextFactory;
    this.memoryCache = memoryCache;
  }

  /// <summary>
  /// Retrieves an app setting by its key. The result is cached per key for <see cref="CacheTtl"/>.
  /// </summary>
  public Task<AppSetting?> SelectAsync(string key, CancellationToken cancellationToken = default)
  {
    string cacheKey = CacheKeyFor(key);
    if (memoryCache.TryGetValue(cacheKey, out AppSetting? cached))
    {
      return Task.FromResult(cached);
    }

    return FetchAndCacheAsync(key, cacheKey, cancellationToken);
  }

  /// <summary>
  /// Inserts a new app setting and caches the result.
  /// </summary>
  public async Task<AppSetting> InsertAsync(AppSetting item)
  {
    await ExecuteWithRetryAsync<object?>(async db =>
    {
      db.AppSettings.Add(item);
      await db.SaveChangesAsync();
      return null;
    });

    memoryCache.Set(CacheKeyFor(item.Key), item, CacheTtl);
    return item;
  }

  /// <summary>
  /// Updates an existing app setting (upsert by key) and refreshes the cache.
  /// </summary>
  public async Task<AppSetting> UpdateAsync(AppSetting item)
  {
    await ExecuteWithRetryAsync<object?>(async db =>
    {
      AppSetting? existing = await db.AppSettings.SingleOrDefaultAsync(s => s.Key == item.Key);
      if (existing == null)
      {
        db.AppSettings.Add(item);
      }
      else
      {
        existing.Value = item.Value;
      }

      await db.SaveChangesAsync();
      return null;
    });

    // Refresh the cached value so the next read doesn't go to the DB.
    memoryCache.Set(CacheKeyFor(item.Key), item, CacheTtl);
    return item;
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  private static string CacheKeyFor(string key) => $"AppSetting.{key}";

  /// <summary>
  /// Fetches the setting from the database, stores it in cache, and returns it.
  /// Separated from <see cref="SelectAsync"/> to keep the hot path (cache hit) allocation-free.
  /// </summary>
  private async Task<AppSetting?> FetchAndCacheAsync(string key, string cacheKey, CancellationToken cancellationToken)
  {
    AppSetting? setting = await ExecuteWithRetryAsync(
      db => db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken),
      cancellationToken);

    // Cache even null results to prevent repeated DB hits for missing keys.
    memoryCache.Set(cacheKey, setting, CacheTtl);
    return setting;
  }

  /// <summary>
  /// Executes <paramref name="operation"/> with exponential-backoff retry logic for transient
  /// database errors, mirroring the behaviour of BaseRepository.ExecuteWithRetryAsync.
  /// Delays: attempt 1 → 1 s, attempt 2 → 2 s (base × 2^(attempt-1)).
  /// </summary>
  private async Task<TResult> ExecuteWithRetryAsync<TResult>(
    Func<LifePlannerContext, Task<TResult>> operation,
    CancellationToken cancellationToken = default)
  {
    Exception? lastException = null;

    for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
    {
      try
      {
        await using LifePlannerContext db = contextFactory.CreateDbContext();
        return await operation(db);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex) when (attempt < MaxRetryAttempts)
      {
        lastException = ex;
        TimeSpan delay = TimeSpan.FromSeconds(BaseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
        await Task.Delay(delay, cancellationToken);
      }
      catch (Exception ex)
      {
        lastException = ex;
        break;
      }
    }

    if (lastException != null)
    {
      ExceptionDispatchInfo.Capture(lastException).Throw();
    }

    throw new InvalidOperationException("ExecuteWithRetryAsync completed without a result or captured exception.");
  }
}
