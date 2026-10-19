//-----------------------------------------------------------------------
// <copyright file="ImapSettingEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Stores the IMAP credentials used to monitor a user's email account for bank transaction notifications.
/// One record per user; the password is stored encrypted at rest via EF Core value converter.
/// </summary>
public sealed class ImapSettingEntity : BaseEntity
{
  /// <summary>Gets or sets the ASP.NET Identity user ID that owns this record.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the IMAP server hostname (e.g. imap.gmail.com).</summary>
  public string ImapHost { get; set; } = string.Empty;

  /// <summary>Gets or sets the IMAP server port. Defaults to 993 (SSL).</summary>
  public int ImapPort { get; set; } = 993;

  /// <summary>Gets or sets the email address used to authenticate with the IMAP server.</summary>
  public string ImapEmail { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the application password for IMAP authentication.
  /// Stored encrypted at rest; decrypted transparently by the EF Core value converter on read.
  /// </summary>
  public string ImapPassword { get; set; } = string.Empty;

  /// <summary>Gets or sets how often the background service should poll this account, in hours. Defaults to 2.</summary>
  public int SyncIntervalHours { get; set; } = 2;

  /// <summary>Gets or sets the UTC timestamp of the most recent successful sync, or null if never synced.</summary>
  public DateTime? LastSyncedAt { get; set; }
}
