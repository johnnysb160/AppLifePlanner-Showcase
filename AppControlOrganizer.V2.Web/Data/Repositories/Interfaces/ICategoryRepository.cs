//-----------------------------------------------------------------------
// <copyright file="ICategoryRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Repositories.Base;

/// <summary>
/// Defines a contract for accessing and managing category entities in a data store.
/// </summary>
/// <remarks>This interface extends <see cref="IBaseRepository{CategoryEntity}"/>, providing additional
/// methods specific to category-related operations. Implementations should ensure thread safety if used in
/// multi-threaded scenarios.</remarks>
public interface ICategoryRepository : IBaseRepository<CategoryEntity>
{
  /// <summary>
  /// Asynchronously retrieves a list of category entities from the data source.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of category entities, or <see
  /// langword="null"/> if no categories are found.</returns>
  Task<List<CategoryEntity>?> SelectAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a category entity by its unique code.
  /// </summary>
  /// <param name="code">The unique code of the category (e.g., "SALARY", "FOOD").</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the category entity with the
  /// specified code, or <see langword="null"/> if not found.</returns>
  Task<CategoryEntity?> SelectByCodeAsync(string code, CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a list of predefined category entities.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of predefined category entities,
  /// or <see langword="null"/> if no predefined categories are found.</returns>
  Task<List<CategoryEntity>?> SelectPredefinedAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously retrieves a list of income category entities.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of income category entities,
  /// or <see langword="null"/> if no income categories are found.</returns>
  Task<List<CategoryEntity>?> SelectIncomeCategoriesAsync(CancellationToken cancellationToken = default);
}
