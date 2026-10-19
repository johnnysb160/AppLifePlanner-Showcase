//-----------------------------------------------------------------------
// <copyright file="GeoIpLookupService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using System.IO.Compression;
using System.Net;
using MaxMind.Db;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

/// <summary>
/// Reads a DB-IP Lite Country database (MMDB format). The file is baked into the Docker image at build
/// time (see <c>Dockerfile</c>); when it is absent, a single best-effort download into the temp folder
/// is attempted so a failed build step does not permanently disable country lookups.
/// </summary>
public sealed class GeoIpLookupService : IGeoIpLookupService, IDisposable
{
  private const string DatabaseFileName = "dbip-country-lite.mmdb";

  private static readonly string BakedDatabasePath = Path.Combine(AppContext.BaseDirectory, "GeoIP", DatabaseFileName);
  private static readonly string FallbackDatabasePath = Path.Combine(Path.GetTempPath(), DatabaseFileName);

  private readonly ILogger<GeoIpLookupService> logger;
  private readonly Lazy<DatabaseReader?> reader;

  /// <summary>
  /// Initializes a new instance of the <see cref="GeoIpLookupService"/> class.
  /// </summary>
  /// <param name="logger">Logger used to report where the database came from, or that it is missing.</param>
  public GeoIpLookupService(ILogger<GeoIpLookupService> logger)
  {
    this.logger = logger;
    reader = new Lazy<DatabaseReader?>(OpenDatabase);
  }

  /// <inheritdoc />
  public GeoIpStatus Status
  {
    get
    {
      if (!reader.IsValueCreated)
      {
        return GeoIpStatus.NotChecked;
      }

      return reader.Value is null ? GeoIpStatus.Missing : GeoIpStatus.Available;
    }
  }

  /// <inheritdoc />
  public bool TryLookup(string? ip, out string? country)
  {
    country = null;

    DatabaseReader? database = reader.Value;
    if (database is null || string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out IPAddress? address))
    {
      return false;
    }

    if (address.IsIPv4MappedToIPv6)
    {
      address = address.MapToIPv4();
    }

    try
    {
      country = database.Country(address).Country.IsoCode;
      return !string.IsNullOrWhiteSpace(country);
    }
    catch (AddressNotFoundException)
    {
      return false;
    }
    catch (InvalidDatabaseException)
    {
      return false;
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if (reader.IsValueCreated)
    {
      reader.Value?.Dispose();
    }
  }

  private DatabaseReader? OpenDatabase()
  {
    if (File.Exists(BakedDatabasePath) && TryOpen(BakedDatabasePath, out DatabaseReader? baked))
    {
      logger.LogInformation("GeoIP database loaded from the image at {Path}.", BakedDatabasePath);
      return baked;
    }

    if ((File.Exists(FallbackDatabasePath) || TryDownloadFallback()) && TryOpen(FallbackDatabasePath, out DatabaseReader? fallback))
    {
      logger.LogInformation("GeoIP database loaded from the temp fallback at {Path}.", FallbackDatabasePath);
      return fallback;
    }

    logger.LogWarning("GeoIP database not found at {Path} and the fallback download failed; country lookups are unavailable.", BakedDatabasePath);
    return null;
  }

  private bool TryOpen(string path, out DatabaseReader? database)
  {
    try
    {
      database = new DatabaseReader(path);
      return true;
    }
    catch (Exception exception) when (exception is InvalidDatabaseException or IOException)
    {
      logger.LogWarning(exception, "GeoIP database at {Path} is unreadable.", path);
      database = null;
      return false;
    }
  }

  private bool TryDownloadFallback()
  {
    DateTime now = DateTime.UtcNow;
    foreach (DateTime month in new[] { now, now.AddMonths(-1) })
    {
      string url = $"https://download.db-ip.com/free/dbip-country-lite-{month:yyyy-MM}.mmdb.gz";
      try
      {
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using Stream compressed = client.GetStreamAsync(url).GetAwaiter().GetResult();
        using GZipStream unzipped = new GZipStream(compressed, CompressionMode.Decompress);
        string partial = FallbackDatabasePath + ".part";
        using (FileStream target = File.Create(partial))
        {
          unzipped.CopyTo(target);
        }

        File.Move(partial, FallbackDatabasePath, overwrite: true);
        return true;
      }
      catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
      {
        logger.LogWarning(exception, "GeoIP fallback download failed for {Url}.", url);
      }
    }

    return false;
  }
}
