//-----------------------------------------------------------------------
// <copyright file="IGeoIpLookupService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

/// <summary>
/// Describes whether a GeoIP database has been loaded, without forcing it to load.
/// </summary>
public enum GeoIpStatus
{
  /// <summary>No lookup has run yet, so availability is not known.</summary>
  NotChecked = 0,

  /// <summary>The database is loaded and lookups resolve countries.</summary>
  Available = 1,

  /// <summary>The database file is missing and could not be downloaded.</summary>
  Missing = 2,
}

/// <summary>
/// Resolves a client IP address to a country using a local GeoIP database. The database is baked into
/// the Docker image at build time; if the image lacks it, a one-time download to the temp folder is
/// attempted. Degrades gracefully to "unknown" when neither is available.
/// </summary>
public interface IGeoIpLookupService
{
  /// <summary>
  /// Gets the current availability of the GeoIP database. Reading this never triggers a load or download.
  /// </summary>
  GeoIpStatus Status { get; }

  /// <summary>
  /// Attempts to resolve the ISO alpha-2 country code for the given IP address.
  /// </summary>
  /// <param name="ip">The raw client IP address.</param>
  /// <param name="country">The resolved ISO alpha-2 country code, or null when unavailable.</param>
  /// <returns><c>true</c> when a country was resolved; otherwise <c>false</c>.</returns>
  bool TryLookup(string? ip, out string? country);
}
