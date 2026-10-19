//-----------------------------------------------------------------------
// <copyright file="TransactionRepository.cs" company="AppLifePlanner">
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
/// Provides data access operations for transaction entities within the LifePlanner application.
/// </summary>
public class TransactionRepository : BaseRepository<TransactionEntity, LifePlannerContext>, ITransactionRepository
{
  /// <summary>
  /// Initializes a new instance of the TransactionRepository class.
  /// </summary>
  public TransactionRepository(IDbContextFactory<LifePlannerContext> contextFactory) : base(contextFactory)
  {
  }

  /// <summary>
  /// Asynchronously retrieves a list of transaction entities, including their related category, ordered by date descending.
  /// </summary>
  public async Task<List<TransactionEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      return await NonDeletedOnly(dataset)
        .Include(a => a.CategoryEntity)
        .OrderByDescending(a => a.Date)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<DateTime?> SelectMostRecentDateAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      return await NonDeletedOnly(dataset)
        .MaxAsync(transaction => (DateTime?)transaction.Date, cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of transaction entities filtered by date range, including their related category.
  /// </summary>
  /// <remarks>
  /// Both bounds are calendar days. Stored dates carry a host-dependent time of day (00:00, 03:00 or 06:00, depending on
  /// the timezone of the process that saved them), and the date converter shifts local bounds to UTC, so the bounds are
  /// pinned to UTC midnight to match every stored day regardless of the current host timezone.
  /// </remarks>
  public async Task<List<TransactionEntity>?> SelectByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
  {
    DateTime rangeStart = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
    DateTime rangeEndExclusive = DateTime.SpecifyKind(endDate.Date.AddDays(1), DateTimeKind.Utc);
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.Date >= rangeStart && a.Date < rangeEndExclusive)
        .Include(a => a.CategoryEntity)
        .OrderByDescending(a => a.Date)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of transaction entities filtered by category identifier, including their related category.
  /// </summary>
  public async Task<List<TransactionEntity>?> SelectByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.CategoryId == categoryId)
        .Include(a => a.CategoryEntity)
        .OrderByDescending(a => a.Date)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Retrieves transactions filtered by the legacy Category enum directly in the database.
  /// Covers transactions where CategoryId is null (pre-migration records).
  /// </summary>
  public async Task<List<TransactionEntity>?> SelectByCategoryEnumAsync(Category category, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.Category == category)
        .Include(a => a.CategoryEntity)
        .OrderByDescending(a => a.Date)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Returns usage counts per CategoryId (FK-based) aggregated in DB.
  /// </summary>
  public async Task<Dictionary<int, int>> GetCategoryUsageCountsByIdAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      return await dbContext.Set<TransactionEntity>()
        .AsNoTracking()
        .Where(a => !a.Deleted && a.CategoryId.HasValue)
        .GroupBy(a => a.CategoryId!.Value)
        .Select(g => new { g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Returns usage counts per legacy Category enum for transactions without a CategoryId, aggregated in DB.
  /// </summary>
  public async Task<Dictionary<int, int>> GetCategoryUsageCountsByEnumAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      return await dbContext.Set<TransactionEntity>()
        .AsNoTracking()
        .Where(a => !a.Deleted && !a.CategoryId.HasValue)
        .GroupBy(a => (int)a.Category)
        .Select(g => new { g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Returns the total usage count for a single category (both FK and legacy enum) aggregated in DB.
  /// </summary>
  public async Task<int> GetUsageCountForCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<TransactionEntity> dataset = dbContext.Set<TransactionEntity>();
      int byId = await dataset.AsNoTracking()
        .CountAsync(a => !a.Deleted && a.CategoryId == categoryId, cancellationToken);
      int byEnum = await dataset.AsNoTracking()
        .CountAsync(a => !a.Deleted && !a.CategoryId.HasValue && (int)a.Category == categoryId, cancellationToken);
      return byId + byEnum;
    }, cancellationToken);
  }
}
