//-----------------------------------------------------------------------
// <copyright file="ITodayWidgetSnapshotService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Widgets;

using AppControlOrganizer.V2.Shared.DTOs;

/// <summary>
/// Builds the compact Today read model consumed by local widgets.
/// </summary>
public interface ITodayWidgetSnapshotService
{
  /// <summary>Builds a localized snapshot for the supplied user and local time.</summary>
  Task<TodayWidgetSnapshotDto> BuildAsync(
    string userId,
    DateTime localNow,
    TimeSpan utcOffset,
    string language,
    CancellationToken cancellationToken = default);
}
