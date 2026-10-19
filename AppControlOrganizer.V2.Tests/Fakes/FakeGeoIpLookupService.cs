//-----------------------------------------------------------------------
// <copyright file="FakeGeoIpLookupService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using AppControlOrganizer.V2.Web.Services.Analytics;

/// <summary>
/// Test double for GeoIP lookups that resolves every address to a fixed country.
/// </summary>
internal sealed class FakeGeoIpLookupService : IGeoIpLookupService
{
  private readonly string? countryCode;

  /// <summary>
  /// Initializes a new instance of the <see cref="FakeGeoIpLookupService"/> class.
  /// </summary>
  /// <param name="countryCode">The country returned for every lookup, or null to simulate an unknown address.</param>
  public FakeGeoIpLookupService(string? countryCode)
  {
    this.countryCode = countryCode;
  }

  /// <inheritdoc />
  public GeoIpStatus Status
  {
    get
    {
      return this.countryCode is null ? GeoIpStatus.Missing : GeoIpStatus.Available;
    }
  }

  /// <inheritdoc />
  public bool TryLookup(string? ip, out string? country)
  {
    country = this.countryCode;
    return this.countryCode is not null;
  }
}
