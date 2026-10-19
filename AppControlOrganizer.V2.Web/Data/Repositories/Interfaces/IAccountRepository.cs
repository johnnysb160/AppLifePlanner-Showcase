//-----------------------------------------------------------------------
// <copyright file="IAccountRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing account entities in a data store.
/// </summary>
/// <remarks>This interface extends <see cref="IBaseRepository{AccountEntity}"/>, providing additional
/// methods specific to account-related operations. Implementations should ensure thread safety if used in
/// multi-threaded scenarios.</remarks>
public interface IAccountRepository : IBaseRepository<AccountEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of account entities from the data source.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of account entities, or <see
  /// langword="null"/> if no accounts are found.</returns>
  Task<List<AccountEntity>?> SelectAsync(CancellationToken cancellationToken = default);
}
