//-----------------------------------------------------------------------
// <copyright file="EndpointHelper.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

/// <summary>
/// Shared helpers for Minimal API endpoint handlers. Centralises common response patterns
/// so each endpoint does not repeat the same null-check / Results factory code.
/// </summary>
public static class EndpointHelper
{
  /// <summary>
  /// Returns <see cref="Results.Ok{T}"/> when <paramref name="entity"/> is not null,
  /// or <see cref="Results.NotFound"/> with a JSON message body otherwise.
  /// </summary>
  /// <typeparam name="T">Entity type (must be a reference type).</typeparam>
  /// <param name="entity">The entity to wrap, or null.</param>
  /// <param name="notFoundMessage">The message to include in the 404 body.</param>
  public static IResult OkOrNotFound<T>(T? entity, string notFoundMessage) where T : class
  {
    return entity is null
      ? Results.NotFound(new { message = notFoundMessage })
      : Results.Ok(entity);
  }

  /// <summary>
  /// Returns a <see cref="Results.NoContent"/> response when the entity was found and deleted,
  /// or <see cref="Results.NotFound"/> when it was not.
  /// </summary>
  /// <typeparam name="T">Entity type (must be a reference type).</typeparam>
  /// <param name="entity">The entity that was looked up before deletion, or null.</param>
  /// <param name="notFoundMessage">The message to include in the 404 body.</param>
  public static IResult NoContentOrNotFound<T>(T? entity, string notFoundMessage) where T : class
  {
    return entity is null
      ? Results.NotFound(new { message = notFoundMessage })
      : Results.NoContent();
  }

  /// <summary>
  /// Safely coalesces a nullable list, ordering by the given key, and returns the result.
  /// Avoids the repeating pattern of <c>(list ?? new List&lt;T&gt;()).OrderBy(...).ToList()</c>.
  /// </summary>
  /// <typeparam name="T">Element type.</typeparam>
  /// <typeparam name="TKey">Order-by key type.</typeparam>
  /// <param name="list">Nullable list from the repository.</param>
  /// <param name="orderBy">Ordering selector.</param>
  public static List<T> SafeOrder<T, TKey>(List<T>? list, Func<T, TKey> orderBy)
  {
    return (list ?? new List<T>()).OrderBy(orderBy).ToList();
  }
}
