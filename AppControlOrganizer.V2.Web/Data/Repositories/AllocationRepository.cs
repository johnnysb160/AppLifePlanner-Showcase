//-----------------------------------------------------------------------
// <copyright file="AllocationRepository.cs" company="AppLifePlanner">
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
/// Provides data access operations for allocation entities within the LifePlanner application.
/// </summary>
/// <remarks>This repository encapsulates methods for retrieving and managing allocation data using Entity Framework
/// Core. It is typically used to abstract database interactions and should be instantiated with a valid context
/// factory. Thread safety depends on the underlying context factory implementation.</remarks>
public class AllocationRepository : BaseRepository<AllocationEntity, LifePlannerContext>, IAllocationRepository
{
  /// <summary>
  /// Initializes a new instance of the AllocationRepository class.
  /// </summary>
  public AllocationRepository(IDbContextFactory<LifePlannerContext> contextFactory) : base(contextFactory)
  {
  }

  /// <summary>
  /// Asynchronously retrieves a list of allocation entities, including their related account, ordered by name.
  /// </summary>
  /// <remarks>The returned list includes only allocations that are not marked as deleted. The operation
  /// is performed using a new database context instance.</remarks>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of allocation entities with related
  /// account included. Returns null if no allocations are found.</returns>
  public async Task<List<AllocationEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<AllocationEntity> dataset = dbContext.Set<AllocationEntity>();
      return await NonDeletedOnly(dataset)
        .Include(a => a.Account)
        .OrderBy(a => a.Name)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of allocation entities filtered by account identifier, including their related account.
  /// </summary>
  /// <remarks>The returned list includes only allocations that are not marked as deleted and belong to the specified account.
  /// The operation is performed using a new database context instance.</remarks>
  /// <param name="accountId">The account identifier to filter allocations.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of allocation entities
  /// belonging to the specified account. Returns null if no allocations are found.</returns>
  public async Task<List<AllocationEntity>?> SelectByAccountAsync(int accountId, CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<AllocationEntity> dataset = dbContext.Set<AllocationEntity>();
      return await NonDeletedOnly(dataset)
        .Where(a => a.AccountId == accountId)
        .Include(a => a.Account)
        .OrderBy(a => a.Name)
        .ToListAsync(cancellationToken);
    }, cancellationToken);
  }

  /// <summary>
  /// Marks the specified allocation as deleted and inactive asynchronously.
  /// </summary>
  /// <remarks>The allocation is not physically removed from the database; instead, its status is updated to
  /// indicate deletion. If no allocation with the specified identifier exists, a new allocation entity is
  /// returned.</remarks>
  /// <param name="id">The unique identifier of the allocation to delete.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the updated allocation entity if found;
  /// otherwise, a new instance of <see cref="AllocationEntity"/>.</returns>
  public override async Task<AllocationEntity> DeleteAsync(long id)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    DbSet<AllocationEntity> dataset = dbContext.Set<AllocationEntity>();
    AllocationEntity? alloc = await dataset.SingleOrDefaultAsync(a => a.Id == id);
    if (alloc == null)
    {
      return Activator.CreateInstance<AllocationEntity>();
    }

    alloc.Deleted = true;
    alloc.Active = false;

    await dbContext.SaveChangesAsync();

    return alloc;
  }
}
