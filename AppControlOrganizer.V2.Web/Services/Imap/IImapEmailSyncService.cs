//-----------------------------------------------------------------------
// <copyright file="IImapEmailSyncService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Imap;

/// <summary>
/// Defines the contract for the service that polls all configured IMAP accounts,
/// parses incoming bank notification emails, and persists the extracted transactions.
/// </summary>
public interface IImapEmailSyncService
{
  /// <summary>
  /// Iterates every user whose configured sync interval has elapsed, connects to their IMAP
  /// server, and processes unread emails from each mapped sender.
  /// Failures for a single user or message are isolated and logged without stopping the overall sync.
  /// </summary>
  /// <param name="cancellationToken">Token used to cooperatively cancel the operation.</param>
  /// <returns>A task that completes when all due users have been processed.</returns>
  Task SyncAllUsersAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Forces an immediate sync for the user identified by <paramref name="ownerUserId"/>,
  /// bypassing the configured sync interval. Used by the manual "Sync Now" action in the UI.
  /// </summary>
  /// <param name="ownerUserId">The ASP.NET Identity user ID whose IMAP account should be synced.</param>
  /// <param name="cancellationToken">Token used to cooperatively cancel the operation.</param>
  /// <returns>A task that completes when the user's sync is finished.</returns>
  Task SyncSingleUserAsync(string ownerUserId, CancellationToken cancellationToken = default);
}
