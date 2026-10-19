//-----------------------------------------------------------------------
// <copyright file="AccessEventEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents one bounded-retention access analytics event (an HTTP request, an in-app page view,
/// or a named feature usage). Admin-only and global, like <see cref="MarketQuoteEntity"/>: it has no
/// owner and is not subject to a tenant query filter. Raw rows are pruned after a short retention
/// window; long-term trends are read from <see cref="AccessDailySummaryEntity"/> instead.
/// </summary>
public class AccessEventEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the kind of activity this event represents.
  /// </summary>
  public AccessEventKind Kind { get; set; }

  /// <summary>
  /// Gets or sets a short, non-reversible hash of the visitor identifier (user id when
  /// authenticated). Never a raw identifier.
  /// </summary>
  public string? UserIdHash { get; set; }

  /// <summary>
  /// Gets or sets a short, non-reversible hash of the client IP address. The raw IP address is
  /// never persisted.
  /// </summary>
  public string? IpHash { get; set; }

  /// <summary>
  /// Gets or sets the ISO alpha-2 country code resolved from the client IP address, when a local
  /// GeoIP database is available. Null when geolocation could not be resolved.
  /// </summary>
  public string? Country { get; set; }

  /// <summary>
  /// Gets or sets the parsed browser name and version (e.g. "Chrome 128"). Set for
  /// <see cref="AccessEventKind.HttpRequest"/> and <see cref="AccessEventKind.PageView"/> only.
  /// </summary>
  public string? Browser { get; set; }

  /// <summary>
  /// Gets or sets the parsed operating system name and version.
  /// </summary>
  public string? OperatingSystem { get; set; }

  /// <summary>
  /// Gets or sets the device type ("Desktop", "Mobile", "Tablet", or "Unknown").
  /// </summary>
  public string? DeviceType { get; set; }

  /// <summary>
  /// Gets or sets the route path for <see cref="AccessEventKind.HttpRequest"/> and
  /// <see cref="AccessEventKind.PageView"/> events, or the feature key for
  /// <see cref="AccessEventKind.FeatureUsage"/> events.
  /// </summary>
  public string? PathOrFeature { get; set; }

  /// <summary>
  /// Gets or sets the circuit-scoped session identifier, set for
  /// <see cref="AccessEventKind.PageView"/> and <see cref="AccessEventKind.FeatureUsage"/> events.
  /// </summary>
  public string? SessionId { get; set; }
}
