//-----------------------------------------------------------------------
// <copyright file="IBaseRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories.Base;

using AppControlOrganizer.V2.Shared.Entities;
using System.Linq.Expressions;

/// <summary>
/// Interface for the base repository.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IBaseRepository<TEntity> where TEntity : BaseEntity
{
  /// <summary>
  /// Fetches information by ID.
  /// </summary>
  /// <param name="id">The ID of the entity.</param>
  /// <returns>The entity with the specified ID.</returns>
  Task<TEntity?> SelectAsync(long id);

  /// <summary>
  /// Fetches all information.
  /// </summary>
  /// <returns>A collection of all entities.</returns>
  Task<ICollection<TEntity>> SelectAsync();

  /// <summary>
  /// Returns data via pagination based on a condition.
  /// </summary>
  /// <param name="condition">The condition to filter entities.</param>
  /// <returns>A collection of entities that match the condition.</returns>
  Task<ICollection<TEntity>?> SelectAsync(Expression<Func<TEntity, bool>> condition);

  /// <summary>
  /// Returns data via pagination based on a condition.
  /// </summary>
  /// <param name="condition">The condition to filter entities.</param>
  /// <param name="pageNumber">The page number.</param>
  /// <param name="pageSize">The size of the page.</param>
  /// <returns>A collection of entities that match the condition.</returns>
  Task<ICollection<TEntity>> SelectAsync(Expression<Func<TEntity, bool>> condition, int pageNumber, int pageSize);

  /// <summary>
  /// Asynchronously retrieves a single entity that matches the specified condition and is not marked as deleted.
  /// </summary>
  /// <remarks>Entities marked as deleted are excluded from the results. The method returns the first matching
  /// entity or <see langword="null"/> if none exist.</remarks>
  /// <param name="condition">An expression that defines the criteria used to select the entity. Only entities for which this condition evaluates
  /// to <see langword="true"/> will be considered.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the first entity that matches the
  /// condition and is not deleted, or <see langword="null"/> if no such entity is found.</returns>
  Task<TEntity?> SelectOneAsync(Expression<Func<TEntity, bool>> condition, CancellationToken cancellationToken);

  /// <summary>
  /// Asynchronously counts the number of entities that are not marked as deleted and optionally match a specified
  /// condition.
  /// </summary>
  /// <remarks>Entities are only counted if they are not marked as deleted. This method executes a database query
  /// and may incur network or I/O latency.</remarks>
  /// <param name="condition">An optional expression used to filter entities. Only entities that satisfy this condition are counted. If null, all
  /// entities not marked as deleted are counted.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the number of entities that match the
  /// specified condition and are not marked as deleted.</returns>
  Task<int> SelectCountAsync(CancellationToken cancellationToken, Expression<Func<TEntity, bool>>? condition = null);

  /// <summary>
  /// Asynchronously returns the first entity that is not marked as deleted, or null if no such entity exists.
  /// </summary>
  /// <remarks>Entities are considered non-deleted if their Deleted property is false. This method queries the
  /// underlying data source and does not return entities marked as deleted.</remarks>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the first non-deleted entity, or null
  /// if no matching entity is found.</returns>
  Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Asynchronously inserts a new entity of type TEntity into the database and updates the entity's identifier with the
  /// generated value.
  /// </summary>
  /// <remarks>This method uses EF Core to add the entity to the context and persist it via SaveChangesAsync.
  /// Navigation properties and shadow properties are handled by EF Core. After insertion, the entity's Id
  /// property is set to the generated identifier.</remarks>
  /// <param name="entity">The entity to insert into the database. Cannot be null and must have valid property values for insertion.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the entity type is not mapped in the DbContext or if no properties are found to insert.</exception>
  Task InsertAsync(TEntity entity);

  /// <summary>
  /// Updates data.
  /// </summary>
  /// <param name="item">The entity to update.</param>
  /// <returns>The updated entity.</returns>
  Task<TEntity> UpdateAsync(TEntity item);

  /// <summary>
  /// Returns true if a non-deleted entity with the given ID exists, without loading the full entity.
  /// Use this for existence-only checks to avoid the overhead of loading navigation properties.
  /// </summary>
  /// <param name="id">The ID to check.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Marks data for deletion.
  /// </summary>
  /// <param name="id">The ID of the entity to delete.</param>
  /// <returns>The deleted entity.</returns>
  Task<TEntity> DeleteAsync(long id);
}
