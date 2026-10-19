//-----------------------------------------------------------------------
// <copyright file="IMarketQuoteRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing market quote entities in a data store.
/// </summary>
public interface IMarketQuoteRepository : IBaseRepository<MarketQuoteEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of market quotes ordered by retrieval time descending.
  /// </summary>
  Task<List<MarketQuoteEntity>?> SelectAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves market quotes filtered by a date range.
  /// </summary>
  Task<List<MarketQuoteEntity>?> SelectByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves market quotes filtered by allocation type and date range.
  /// </summary>
  Task<List<MarketQuoteEntity>?> SelectByAllocationTypeAndDateRangeAsync(
    AllocationType allocationType,
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves market quotes for a specific allocation type.
  /// </summary>
  Task<List<MarketQuoteEntity>?> SelectByAllocationTypeAsync(AllocationType allocationType, CancellationToken cancellationToken = default);
}
