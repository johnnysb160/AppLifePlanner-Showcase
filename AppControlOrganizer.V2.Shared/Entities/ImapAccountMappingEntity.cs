//-----------------------------------------------------------------------
// <copyright file="ImapAccountMappingEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Maps a sender identifier (bank display name or email pattern) to a specific financial account
/// for a given user, allowing the IMAP sync service to route parsed transactions to the correct account.
/// </summary>
public sealed class ImapAccountMappingEntity : BaseEntity
{
  /// <summary>Gets or sets the ASP.NET Identity user ID that owns this mapping.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the sender string used to match incoming emails via IMAP FROM search.
  /// Can be a display name fragment (e.g. "Nubank") or an email domain (e.g. "nubank.com.br").
  /// </summary>
  public string SenderIdentifier { get; set; } = string.Empty;

  /// <summary>Gets or sets the ID of the financial account to which matched transactions will be posted.</summary>
  public int AccountId { get; set; }

  /// <summary>Gets or sets the navigation property for the target financial account.</summary>
  public AccountEntity? Account { get; set; }
}
