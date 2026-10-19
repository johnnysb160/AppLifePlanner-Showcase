//-----------------------------------------------------------------------
// <copyright file="IAccessAnalyticsRecorder.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Records access analytics events (HTTP requests, in-app page views, and named feature usage)
/// for the admin-only "Access Analytics" tab. A no-op outside the Production environment.
/// </summary>
public interface IAccessAnalyticsRecorder
{
  /// <summary>
  /// Enqueues an access analytics event for later, asynchronous enrichment and persistence.
  /// Never blocks and never throws.
  /// </summary>
  /// <param name="kind">The kind of activity being recorded.</param>
  /// <param name="userId">The raw authenticated user id, or null when anonymous.</param>
  /// <param name="ip">The raw client IP address, or null when unavailable.</param>
  /// <param name="userAgent">The raw User-Agent header value, or null when unavailable.</param>
  /// <param name="pathOrFeature">The route path, or the feature key for feature-usage events.</param>
  /// <param name="sessionId">The circuit-scoped session identifier, when applicable.</param>
  void Record(AccessEventKind kind, string? userId, string? ip, string? userAgent, string? pathOrFeature, string? sessionId);

  /// <summary>
  /// Convenience overload that enqueues a <see cref="AccessEventKind.FeatureUsage"/> event.
  /// </summary>
  /// <param name="featureName">A short, stable key identifying the feature that was used.</param>
  /// <param name="userId">The raw authenticated user id, or null when anonymous.</param>
  void RecordFeatureUsage(string featureName, string? userId);
}
