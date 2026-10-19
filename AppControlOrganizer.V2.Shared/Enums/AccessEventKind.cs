//-----------------------------------------------------------------------
// <copyright file="AccessEventKind.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Distinguishes the kind of activity recorded by an access analytics event.
/// </summary>
public enum AccessEventKind
{
  /// <summary>
  /// A raw HTTP request served by the Web host.
  /// </summary>
  HttpRequest = 1,

  /// <summary>
  /// An in-app Blazor circuit navigation (no new HTTP request).
  /// </summary>
  PageView = 2,

  /// <summary>
  /// A named application feature was used (e.g. a transaction was created).
  /// </summary>
  FeatureUsage = 3
}
