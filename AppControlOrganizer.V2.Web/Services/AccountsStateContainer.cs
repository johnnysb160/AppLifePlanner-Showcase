//-----------------------------------------------------------------------
// <copyright file="AccountsStateContainer.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Scoped state container for the Accounts page.
/// <para>
/// Holds the account and credit-card lists fetched during the current Blazor circuit so that
/// navigating back to the Accounts page can render instantly from the in-memory snapshot
/// instead of waiting for another round-trip to the remote database.
/// </para>
/// <para>
/// The cached snapshot is considered fresh for <see cref="Ttl"/> (2 minutes). After that
/// window expires, the next page visit falls back to the normal full-load path and the
/// container is repopulated. Any write operation (save/delete) that triggers a data reload
/// also refreshes the container automatically.
/// </para>
/// </summary>
public sealed class AccountsStateContainer
{
  private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

  private DateTime? loadedAt;

  /// <summary>
  /// Gets the cached account list. <c>null</c> when the container has never been populated.
  /// </summary>
  public List<AccountEntity>? Accounts { get; private set; }

  /// <summary>
  /// Gets the cached credit-card list. <c>null</c> when the container has never been populated.
  /// </summary>
  public List<CreditCardEntity>? Cards { get; private set; }

  /// <summary>
  /// Gets a value indicating whether both lists have been stored at least once.
  /// </summary>
  public bool HasData => Accounts != null && Cards != null;

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
  /// <param name="accounts">The freshly loaded account list.</param>
  /// <param name="cards">The freshly loaded credit-card list.</param>
  public void Set(List<AccountEntity> accounts, List<CreditCardEntity> cards)
  {
    Accounts = accounts;
    Cards = cards;
    loadedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Marks the cached snapshot as stale so the next page visit triggers a full database reload.
  /// </summary>
  public void Invalidate() => loadedAt = null;
}
