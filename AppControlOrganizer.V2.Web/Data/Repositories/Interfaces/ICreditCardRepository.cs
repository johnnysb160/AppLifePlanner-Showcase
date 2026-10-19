//-----------------------------------------------------------------------
// <copyright file="ICreditCardRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing credit card entities in a data store.
/// </summary>
/// <remarks>This interface extends <see cref="IBaseRepository{CreditCardEntity}"/>, providing additional
/// methods specific to credit card-related operations. Implementations should ensure thread safety if used in
/// multi-threaded scenarios.</remarks>
public interface ICreditCardRepository : IBaseRepository<CreditCardEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of credit card entities from the data source, including their related account.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of credit card entities, or <see
  /// langword="null"/> if no credit cards are found.</returns>
  Task<List<CreditCardEntity>?> SelectAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves credit cards for a specific account, filtered in the database.
  /// Avoids loading all cards and filtering in memory.
  /// </summary>
  /// <param name="accountId">The account ID to filter by.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task<List<CreditCardEntity>?> SelectByAccountAsync(int accountId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieve all credit cards including soft-deleted ones. Intended for maintenance tasks.
  /// </summary>
  Task<List<CreditCardEntity>?> SelectAllIncludingDeletedAsync(CancellationToken cancellationToken = default);
}
