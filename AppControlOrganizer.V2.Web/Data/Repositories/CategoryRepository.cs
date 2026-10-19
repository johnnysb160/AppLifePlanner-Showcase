//-----------------------------------------------------------------------
// <copyright file="CategoryRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Repositories.Base;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides data access operations for category entities within the LifePlanner application.
/// </summary>
public class CategoryRepository : BaseRepository<CategoryEntity, LifePlannerContext>, ICategoryRepository
{
  /// <summary>
  /// Initializes a new instance of the CategoryRepository class.
  /// </summary>
  public CategoryRepository(IDbContextFactory<LifePlannerContext> contextFactory) : base(contextFactory)
  {
  }

  /// <summary>
  /// Asynchronously retrieves a list of category entities, ordered by name.
  /// </summary>
  public async Task<List<CategoryEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<CategoryEntity> dataset = dbContext.Set<CategoryEntity>();
      return await NonDeletedOnly(dataset).OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a category entity by its unique code.
  /// </summary>
  public async Task<CategoryEntity?> SelectByCodeAsync(string code, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<CategoryEntity> dataset = dbContext.Set<CategoryEntity>();
      return await NonDeletedOnly(dataset).Where(c => c.Code == code).FirstOrDefaultAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of predefined category entities, ordered by name.
  /// </summary>
  public async Task<List<CategoryEntity>?> SelectPredefinedAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<CategoryEntity> dataset = dbContext.Set<CategoryEntity>();
      return await NonDeletedOnly(dataset).Where(c => c.IsPredefined).OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of income category entities, ordered by name.
  /// </summary>
  public async Task<List<CategoryEntity>?> SelectIncomeCategoriesAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<CategoryEntity> dataset = dbContext.Set<CategoryEntity>();
      return await NonDeletedOnly(dataset).Where(c => c.IsIncome).OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }, cancellationToken);
  }
}
