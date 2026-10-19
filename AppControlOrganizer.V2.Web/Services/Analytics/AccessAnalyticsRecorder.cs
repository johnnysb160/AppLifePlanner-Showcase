//-----------------------------------------------------------------------
// <copyright file="AccessAnalyticsRecorder.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using System.Collections.Concurrent;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// One pending access analytics event, captured with raw (unhashed, unparsed) values. Enrichment
/// (User-Agent parsing, GeoIP lookup, hashing) happens later in <see cref="AccessAnalyticsHostedService"/>,
/// off the request path.
/// </summary>
/// <param name="Kind">The kind of activity being recorded.</param>
/// <param name="OccurredAtUtc">The UTC timestamp the event was captured at.</param>
/// <param name="UserId">The raw authenticated user id, or null when anonymous.</param>
/// <param name="Ip">The raw client IP address, or null when unavailable.</param>
/// <param name="UserAgent">The raw User-Agent header value, or null when unavailable.</param>
/// <param name="PathOrFeature">The route path, or the feature key for feature-usage events.</param>
/// <param name="SessionId">The circuit-scoped session identifier, when applicable.</param>
internal sealed record PendingAccessEvent(
  AccessEventKind Kind,
  DateTime OccurredAtUtc,
  string? UserId,
  string? Ip,
  string? UserAgent,
  string? PathOrFeature,
  string? SessionId);

/// <summary>
/// Bounded, non-blocking capture of access analytics events, mirroring the <see cref="Logging.InMemoryLogSink"/>
/// ring-buffer pattern already used for the "Logs e métricas" tab. A no-op outside Production, so
/// local debugging never records anything.
/// </summary>
public sealed class AccessAnalyticsRecorder : IAccessAnalyticsRecorder
{
  private const int Capacity = 2000;

  private readonly bool isEnabled;
  private readonly ConcurrentQueue<PendingAccessEvent> pending = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="AccessAnalyticsRecorder"/> class.
  /// </summary>
  /// <param name="environment">The hosting environment, used to gate capture to Production only.</param>
  public AccessAnalyticsRecorder(IWebHostEnvironment environment)
  {
    isEnabled = environment.IsProduction();
  }

  /// <inheritdoc />
  public void Record(AccessEventKind kind, string? userId, string? ip, string? userAgent, string? pathOrFeature, string? sessionId)
  {
    if (!isEnabled)
    {
      return;
    }

    pending.Enqueue(new PendingAccessEvent(kind, DateTime.UtcNow, userId, ip, userAgent, pathOrFeature, sessionId));

    while (pending.Count > Capacity && pending.TryDequeue(out _))
    {
    }
  }

  /// <inheritdoc />
  public void RecordFeatureUsage(string featureName, string? userId)
  {
    Record(AccessEventKind.FeatureUsage, userId, null, null, featureName, null);
  }

  /// <summary>
  /// Dequeues every currently pending event for enrichment and persistence. Called only by
  /// <see cref="AccessAnalyticsHostedService"/>.
  /// </summary>
  /// <returns>The events that were pending, in enqueue order.</returns>
  internal IReadOnlyList<PendingAccessEvent> DrainPending()
  {
    List<PendingAccessEvent> drained = new List<PendingAccessEvent>();
    while (pending.TryDequeue(out PendingAccessEvent? item))
    {
      drained.Add(item!);
    }

    return drained;
  }
}
