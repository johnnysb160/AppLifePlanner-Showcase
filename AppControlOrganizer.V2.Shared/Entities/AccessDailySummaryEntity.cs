//-----------------------------------------------------------------------
// <copyright file="AccessDailySummaryEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents one permanent, per-day rollup of access analytics, kept indefinitely so long-range
/// trend charts stay cheap after the underlying <see cref="AccessEventEntity"/> raw rows for that
/// day have been pruned. Admin-only and global, like <see cref="MarketQuoteEntity"/>.
/// </summary>
public class AccessDailySummaryEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the UTC calendar date this summary covers. Unique across the table.
  /// </summary>
  public DateOnly SummaryDate { get; set; }

  /// <summary>
  /// Gets or sets the number of distinct visitor IP hashes observed on this date.
  /// </summary>
  public int UniqueVisitors { get; set; }

  /// <summary>
  /// Gets or sets the number of distinct in-app circuit sessions observed on this date.
  /// </summary>
  public int Sessions { get; set; }

  /// <summary>
  /// Gets or sets the number of in-app page views recorded on this date.
  /// </summary>
  public int PageViews { get; set; }

  /// <summary>
  /// Gets or sets the number of named feature-usage events recorded on this date.
  /// </summary>
  public int FeatureUsageCount { get; set; }

  /// <summary>
  /// Gets or sets the average session duration, in milliseconds, for sessions observed on this date.
  /// </summary>
  public long AvgSessionDurationMs { get; set; }
}
