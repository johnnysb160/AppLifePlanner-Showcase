//-----------------------------------------------------------------------
// <copyright file="AllocationsStateContainer.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;

/// <summary>
/// Scoped state container for the Allocations page.
/// <para>
/// Holds the full (unfiltered) allocation list, the related account list, and the latest
/// market-quote snapshot fetched during the current Blazor circuit. When the user navigates
/// back to the Allocations page, the component restores this snapshot immediately and renders
/// without any loading delay, then optionally refreshes the data silently in the background.
/// </para>
/// <para>
/// The cached snapshot is considered fresh for <see cref="Ttl"/> (2 minutes). After that
/// window expires, the next page visit falls back to the normal full-load path and the
/// container is repopulated. Any write operation (save/delete) that triggers a data reload
/// also refreshes the container automatically.
/// </para>
/// </summary>
public sealed class AllocationsStateContainer
{
  private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

  private DateTime? loadedAt;

  /// <summary>
  /// Gets the cached, unfiltered allocation list ordered by name.
  /// <c>null</c> when the container has never been populated.
  /// </summary>
  public List<AllocationEntity>? Allocations { get; private set; }

  /// <summary>
  /// Gets the cached account list used by the allocations page.
  /// <c>null</c> when the container has never been populated.
  /// </summary>
  public List<AccountEntity>? Accounts { get; private set; }

  /// <summary>
  /// Gets the latest market-quote snapshot used for pricing calculations.
  /// <c>null</c> when the container has never been populated.
  /// </summary>
  public IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot>? LatestQuotes { get; private set; }

  /// <summary>
  /// Gets a value indicating whether all three data sets have been stored at least once.
  /// </summary>
  public bool HasData => Allocations != null && Accounts != null && LatestQuotes != null;

  /// <summary>
  /// Gets a value indicating whether the cached snapshot is older than <see cref="Ttl"/>.
  /// </summary>
  public bool IsStale => loadedAt == null || DateTime.UtcNow - loadedAt.Value > Ttl;

  /// <summary>
  /// Gets a value indicating whether there is a populated snapshot that is still within the TTL window.
  /// </summary>
  public bool HasFreshData => HasData && !IsStale;

  /// <summary>
  /// Stores a new snapshot and records the current UTC timestamp.
  /// </summary>
  /// <param name="allocations">The freshly loaded, unfiltered, name-ordered allocation list.</param>
  /// <param name="accounts">The freshly loaded account list.</param>
  /// <param name="latestQuotes">The latest market-quote snapshot.</param>
  public void Set(
    List<AllocationEntity> allocations,
    List<AccountEntity> accounts,
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes)
  {
    Allocations = allocations;
    Accounts = accounts;
    LatestQuotes = latestQuotes;
    loadedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Marks the cached snapshot as stale so the next page visit triggers a full database reload.
  /// </summary>
  public void Invalidate() => loadedAt = null;
}
