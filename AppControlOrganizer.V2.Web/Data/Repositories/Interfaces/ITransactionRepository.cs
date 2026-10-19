//-----------------------------------------------------------------------
// <copyright file="ITransactionRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing transaction entities in a data store.
/// </summary>
/// <remarks>This interface extends <see cref="IBaseRepository{TransactionEntity}"/>, providing additional
/// methods specific to transaction-related operations. Implementations should ensure thread safety if used in
/// multi-threaded scenarios.</remarks>
public interface ITransactionRepository : IBaseRepository<TransactionEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of transaction entities from the data source, including their related category.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of transaction entities, or <see
  /// langword="null"/> if no transactions are found.</returns>
  Task<List<TransactionEntity>?> SelectAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the most recent transaction date without loading transaction rows.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The latest transaction date, or <see langword="null"/> when none exists.</returns>
  Task<DateTime?> SelectMostRecentDateAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a list of transaction entities filtered by date range.
  /// </summary>
  /// <param name="startDate">The start date of the range (inclusive).</param>
  /// <param name="endDate">The end date of the range (inclusive).</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of transaction entities
  /// within the specified date range, or <see langword="null"/> if no transactions are found.</returns>
  Task<List<TransactionEntity>?> SelectByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a list of transaction entities filtered by category identifier.
  /// </summary>
  /// <param name="categoryId">The category identifier to filter transactions.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of transaction entities
  /// belonging to the specified category, or <see langword="null"/> if no transactions are found.</returns>
  Task<List<TransactionEntity>?> SelectByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves transactions filtered by the legacy Category enum, directly in the database query.
  /// Covers transactions that predate the CategoryId FK migration (CategoryId is null).
  /// </summary>
  /// <param name="category">The legacy category enum value to filter by.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task<List<TransactionEntity>?> SelectByCategoryEnumAsync(Category category, CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns usage counts grouped by CategoryId (new FK). Aggregated in DB — avoids loading all transactions.
  /// </summary>
  Task<Dictionary<int, int>> GetCategoryUsageCountsByIdAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns usage counts grouped by the legacy Category enum for transactions without a CategoryId.
  /// Aggregated in DB — avoids loading all transactions.
  /// </summary>
  Task<Dictionary<int, int>> GetCategoryUsageCountsByEnumAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the total usage count for a single category (both FK and legacy enum).
  /// </summary>
  Task<int> GetUsageCountForCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
}
