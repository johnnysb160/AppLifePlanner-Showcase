//-----------------------------------------------------------------------
// <copyright file="IAllocationRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing allocation entities in a data store.
/// </summary>
/// <remarks>This interface extends <see cref="IBaseRepository{AllocationEntity}"/>, providing additional
/// methods specific to allocation-related operations. Implementations should ensure thread safety if used in
/// multi-threaded scenarios.</remarks>
public interface IAllocationRepository : IBaseRepository<AllocationEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of allocation entities from the data source, including their related account.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of allocation entities, or <see
  /// langword="null"/> if no allocations are found.</returns>
  Task<List<AllocationEntity>?> SelectAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a list of allocation entities filtered by account identifier.
  /// </summary>
  /// <param name="accountId">The account identifier to filter allocations.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of allocation entities
  /// belonging to the specified account, or <see langword="null"/> if no allocations are found.</returns>
  Task<List<AllocationEntity>?> SelectByAccountAsync(int accountId, CancellationToken cancellationToken = default);
}
