//-----------------------------------------------------------------------
// <copyright file="AccessAnalyticsCapturePolicy.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Decides which HTTP responses count as a page load for access analytics. The layout is rendered
/// statically, so every page navigation is a document request; machine-to-machine traffic (widget
/// polling, APIs, health checks, assets) must not be counted as a visit.
/// </summary>
public static class AccessAnalyticsCapturePolicy
{
  private static readonly string[] ExcludedPrefixes =
  [
    "/_blazor",
    "/_framework",
    "/api",
    "/health",
    "/swagger",
  ];

  /// <summary>
  /// Determines whether a completed request is a successful HTML page load worth recording.
  /// </summary>
  /// <param name="method">The HTTP method.</param>
  /// <param name="statusCode">The response status code.</param>
  /// <param name="contentType">The response content type, if any.</param>
  /// <param name="path">The request path.</param>
  /// <returns><c>true</c> when the request should be recorded as a page view.</returns>
  public static bool ShouldRecordPageLoad(string method, int statusCode, string? contentType, PathString path)
  {
    if (!HttpMethods.IsGet(method) || statusCode != StatusCodes.Status200OK)
    {
      return false;
    }

    if (contentType is null || !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    foreach (string prefix in ExcludedPrefixes)
    {
      if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
      {
        return false;
      }
    }

    return true;
  }
}
