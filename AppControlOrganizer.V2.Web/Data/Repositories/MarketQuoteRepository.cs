//-----------------------------------------------------------------------
// <copyright file="MarketQuoteRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Repositories.Base;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides data access operations for market quote entities within the LifePlanner application.
/// </summary>
public class MarketQuoteRepository : BaseRepository<MarketQuoteEntity, LifePlannerContext>, IMarketQuoteRepository
{
  /// <summary>
  /// Initializes a new instance of the MarketQuoteRepository class.
  /// </summary>
  public MarketQuoteRepository(IDbContextFactory<LifePlannerContext> contextFactory) : base(contextFactory)
  {
  }

  /// <summary>
  /// Asynchronously retrieves a list of market quotes ordered by retrieval time descending.
  /// </summary>
  public async Task<List<MarketQuoteEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<MarketQuoteEntity> dataset = dbContext.Set<MarketQuoteEntity>();
      return await NonDeletedOnly(dataset)
        .OrderByDescending(a => a.RetrievedAtUtc)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves market quotes filtered by a date range.
  /// </summary>
  public async Task<List<MarketQuoteEntity>?> SelectByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<MarketQuoteEntity> dataset = dbContext.Set<MarketQuoteEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.RetrievedAtUtc >= startDate && a.RetrievedAtUtc <= endDate)
        .OrderByDescending(a => a.RetrievedAtUtc)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves market quotes filtered by allocation type and date range.
  /// </summary>
  public async Task<List<MarketQuoteEntity>?> SelectByAllocationTypeAndDateRangeAsync(
    AllocationType allocationType,
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<MarketQuoteEntity> dataset = dbContext.Set<MarketQuoteEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.AllocationType == allocationType
                    && a.RetrievedAtUtc >= startDate
                    && a.RetrievedAtUtc <= endDate)
        .OrderByDescending(a => a.RetrievedAtUtc)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves market quotes for a specific allocation type.
  /// </summary>
  public async Task<List<MarketQuoteEntity>?> SelectByAllocationTypeAsync(AllocationType allocationType, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<MarketQuoteEntity> dataset = dbContext.Set<MarketQuoteEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.AllocationType == allocationType)
        .OrderByDescending(a => a.RetrievedAtUtc)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }
}
