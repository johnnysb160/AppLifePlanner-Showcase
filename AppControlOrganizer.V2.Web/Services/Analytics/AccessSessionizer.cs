//-----------------------------------------------------------------------
// <copyright file="AccessSessionizer.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Derives visitors and sessions from page-view events without cookies or client identifiers. A
/// visitor is an IP hash plus browser and operating system; a session is a run of that visitor's page
/// views with no gap longer than <see cref="InactivityGap"/>. Duration is measured between the first
/// and last page load of the session, so a single-page visit has no measurable duration.
/// </summary>
public static class AccessSessionizer
{
  /// <summary>The longest pause between two page loads that still belongs to the same session.</summary>
  public static readonly TimeSpan InactivityGap = TimeSpan.FromMinutes(30);

  /// <summary>
  /// Builds the visitor key for an event.
  /// </summary>
  /// <param name="item">The access event.</param>
  /// <returns>A key identifying the visitor.</returns>
  public static string VisitorKey(AccessEventEntity item)
  {
    return $"{item.IpHash}|{item.Browser}|{item.OperatingSystem}";
  }

  /// <summary>
  /// Counts distinct visitors among the page views.
  /// </summary>
  /// <param name="events">The events to inspect; only page views are considered.</param>
  /// <returns>The number of distinct visitors.</returns>
  public static int CountUniqueVisitors(IEnumerable<AccessEventEntity> events)
  {
    return events
      .Where(item => item.Kind == AccessEventKind.PageView)
      .Select(VisitorKey)
      .Distinct()
      .Count();
  }

  /// <summary>
  /// Builds the duration of every session, in milliseconds.
  /// </summary>
  /// <param name="events">The events to inspect; only page views are considered.</param>
  /// <returns>One duration per session.</returns>
  public static IReadOnlyList<long> BuildSessionDurationsMs(IEnumerable<AccessEventEntity> events)
  {
    List<long> durations = new List<long>();

    foreach (IGrouping<string, AccessEventEntity> visitor in events
      .Where(item => item.Kind == AccessEventKind.PageView)
      .GroupBy(VisitorKey))
    {
      DateTime? sessionStart = null;
      DateTime? previous = null;

      foreach (DateTime at in visitor.Select(item => item.CreationDate).OrderBy(value => value))
      {
        if (previous is not null && at - previous.Value > InactivityGap)
        {
          durations.Add((long)(previous.Value - sessionStart!.Value).TotalMilliseconds);
          sessionStart = null;
        }

        sessionStart ??= at;
        previous = at;
      }

      if (sessionStart is not null && previous is not null)
      {
        durations.Add((long)(previous.Value - sessionStart.Value).TotalMilliseconds);
      }
    }

    return durations;
  }
}
