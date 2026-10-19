//-----------------------------------------------------------------------
// <copyright file="AccessAnalyticsTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Analytics;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Analytics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Tests access analytics capture, enrichment, daily rollup, and retention pruning.
/// </summary>
public sealed class AccessAnalyticsTests
{
  private const string ChromeDesktopUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

  /// <summary>
  /// Nothing is captured outside Production, so local debugging never records anything.
  /// </summary>
  [Fact]
  public void RecorderIsNoOpOutsideProduction()
  {
    AccessAnalyticsRecorder recorder = new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Development"));

    recorder.Record(AccessEventKind.HttpRequest, "user-a", "203.0.113.5", ChromeDesktopUserAgent, "/", null);
    recorder.RecordFeatureUsage("transaction.created", "user-a");

    Assert.Empty(recorder.DrainPending());
  }

  /// <summary>
  /// Production capture queues events in order and draining empties the queue.
  /// </summary>
  [Fact]
  public void RecorderQueuesAndDrainsEventsInProduction()
  {
    AccessAnalyticsRecorder recorder = new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Production"));

    recorder.Record(AccessEventKind.PageView, "user-a", null, null, "transactions", "session-1");
    recorder.RecordFeatureUsage("transaction.created", "user-a");

    IReadOnlyList<PendingAccessEvent> drained = recorder.DrainPending();

    Assert.Equal(2, drained.Count);
    Assert.Equal(AccessEventKind.PageView, drained[0].Kind);
    Assert.Equal("transactions", drained[0].PathOrFeature);
    Assert.Equal(AccessEventKind.FeatureUsage, drained[1].Kind);
    Assert.Equal("transaction.created", drained[1].PathOrFeature);
    Assert.Empty(recorder.DrainPending());
  }

  /// <summary>
  /// The queue is bounded and drops the oldest events so a burst cannot grow memory without limit.
  /// </summary>
  [Fact]
  public void RecorderDropsOldestEventsWhenCapacityIsExceeded()
  {
    AccessAnalyticsRecorder recorder = new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Production"));

    for (int index = 0; index < 2050; index++)
    {
      recorder.Record(AccessEventKind.HttpRequest, null, null, null, $"p{index}", null);
    }

    IReadOnlyList<PendingAccessEvent> drained = recorder.DrainPending();

    Assert.Equal(2000, drained.Count);
    Assert.Equal("p50", drained[0].PathOrFeature);
    Assert.Equal("p2049", drained[^1].PathOrFeature);
  }

  /// <summary>
  /// Ingestion parses the browser and device, resolves the country, and never persists the raw IP or user id.
  /// </summary>
  [Fact]
  public async Task IngestEnrichesEventsAndNeverPersistsRawIdentifiers()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    AccessAnalyticsRecorder recorder = new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Production"));
    recorder.Record(AccessEventKind.HttpRequest, "user-a", "203.0.113.5", ChromeDesktopUserAgent, "/plan", null);

    await using ServiceProvider provider = BuildProvider(factory);
    AccessAnalyticsHostedService service = new AccessAnalyticsHostedService(
      recorder,
      provider.GetRequiredService<IServiceScopeFactory>(),
      new FakeGeoIpLookupService("BR"),
      NullLogger<AccessAnalyticsHostedService>.Instance);

    List<AccessEventEntity> stored = await RunUntilAsync(
      service,
      factory,
      context => context.AccessEvents.ToList(),
      rows => rows.Count == 1);

    AccessEventEntity row = Assert.Single(stored);
    Assert.Equal(AccessEventKind.HttpRequest, row.Kind);
    Assert.Equal("/plan", row.PathOrFeature);
    Assert.Equal("BR", row.Country);
    Assert.StartsWith("Chrome", row.Browser);
    Assert.StartsWith("Windows", row.OperatingSystem);
    Assert.Equal("Desktop", row.DeviceType);
    Assert.NotEqual("203.0.113.5", row.IpHash);
    Assert.NotEqual("user-a", row.UserIdHash);
    Assert.False(string.IsNullOrEmpty(row.IpHash));
  }

  /// <summary>
  /// Device type is derived from the parsed User-Agent.
  /// </summary>
  /// <param name="userAgent">The User-Agent header value.</param>
  /// <param name="expectedDevice">The expected device type.</param>
  [Theory]
  [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "Mobile")]
  [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Mobile Safari/537.36", "Mobile")]
  [InlineData("Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "Tablet")]
  [InlineData(ChromeDesktopUserAgent, "Desktop")]
  [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", "Bot")]
  [InlineData("Go-http-client/2.0", "Bot")]
  public async Task IngestClassifiesDeviceType(string userAgent, string expectedDevice)
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    AccessAnalyticsRecorder recorder = new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Production"));
    recorder.Record(AccessEventKind.HttpRequest, null, "203.0.113.9", userAgent, "/", null);

    await using ServiceProvider provider = BuildProvider(factory);
    AccessAnalyticsHostedService service = new AccessAnalyticsHostedService(
      recorder,
      provider.GetRequiredService<IServiceScopeFactory>(),
      new FakeGeoIpLookupService(null),
      NullLogger<AccessAnalyticsHostedService>.Instance);

    List<AccessEventEntity> stored = await RunUntilAsync(
      service,
      factory,
      context => context.AccessEvents.ToList(),
      rows => rows.Count == 1);

    AccessEventEntity row = Assert.Single(stored);
    Assert.Equal(expectedDevice, row.DeviceType);
    Assert.Null(row.Country);
  }

  /// <summary>
  /// Sessions are derived per visitor with a 30-minute inactivity gap; expired raw rows are pruned.
  /// </summary>
  [Fact]
  public async Task RollupSummarizesYesterdayAndPrunesExpiredEvents()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    DateTime yesterdayMorning = DateTime.UtcNow.Date.AddDays(-1).AddHours(10);

    await using (LifePlannerContext seed = await factory.CreateDbContextAsync())
    {
      seed.AccessEvents.AddRange(
        NewEvent(AccessEventKind.PageView, yesterdayMorning, "ip-a"),
        NewEvent(AccessEventKind.PageView, yesterdayMorning.AddMinutes(3), "ip-a"),
        NewEvent(AccessEventKind.PageView, yesterdayMorning.AddMinutes(1), "ip-b"),
        NewEvent(AccessEventKind.FeatureUsage, yesterdayMorning.AddMinutes(2), "ip-a"),
        NewEvent(AccessEventKind.PageView, DateTime.UtcNow.AddDays(-45), "ip-c"));
      await seed.SaveChangesAsync();
    }

    List<AccessDailySummaryEntity> summaries = await RunRollupAsync(factory, expectedRawEvents: 4, expectedSummaries: 1);

    AccessDailySummaryEntity summary = Assert.Single(summaries);
    Assert.Equal(DateOnly.FromDateTime(yesterdayMorning), summary.SummaryDate);
    Assert.Equal(2, summary.UniqueVisitors);
    Assert.Equal(2, summary.Sessions);
    Assert.Equal(3, summary.PageViews);
    Assert.Equal(1, summary.FeatureUsageCount);
    Assert.Equal(90000L, summary.AvgSessionDurationMs);
  }

  /// <summary>
  /// A sleeping instance still summarizes every missed day that raw events cover.
  /// </summary>
  [Fact]
  public async Task RollupCatchesUpOnMissedDays()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    DateTime twoDaysAgo = DateTime.UtcNow.Date.AddDays(-2).AddHours(9);
    DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(9);

    await using (LifePlannerContext seed = await factory.CreateDbContextAsync())
    {
      seed.AccessEvents.AddRange(
        NewEvent(AccessEventKind.PageView, twoDaysAgo, "ip-a"),
        NewEvent(AccessEventKind.PageView, yesterday, "ip-a"));
      await seed.SaveChangesAsync();
    }

    List<AccessDailySummaryEntity> summaries = await RunRollupAsync(factory, expectedRawEvents: 2, expectedSummaries: 2);

    Assert.Equal(2, summaries.Count);
    Assert.All(summaries, item => Assert.Equal(1, item.PageViews));
  }

  /// <summary>
  /// A gap longer than 30 minutes starts a new session, and a single page load has no measurable duration.
  /// </summary>
  [Fact]
  public void SessionizerSplitsSessionsOnInactivityGap()
  {
    DateTime start = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
    List<AccessEventEntity> events = new List<AccessEventEntity>
    {
      NewEvent(AccessEventKind.PageView, start, "ip-a"),
      NewEvent(AccessEventKind.PageView, start.AddMinutes(10), "ip-a"),
      NewEvent(AccessEventKind.PageView, start.AddMinutes(90), "ip-a"),
      NewEvent(AccessEventKind.FeatureUsage, start.AddMinutes(5), "ip-a"),
    };

    IReadOnlyList<long> durations = AccessSessionizer.BuildSessionDurationsMs(events);

    Assert.Equal(2, durations.Count);
    Assert.Contains(600000L, durations);
    Assert.Contains(0L, durations);
  }

  /// <summary>
  /// A visitor is the IP hash combined with the browser and operating system.
  /// </summary>
  [Fact]
  public void SessionizerCountsVisitorsByIpBrowserAndSystem()
  {
    DateTime now = DateTime.UtcNow;
    AccessEventEntity firefox = NewEvent(AccessEventKind.PageView, now, "ip-a");
    firefox.Browser = "Firefox 130";

    List<AccessEventEntity> events = new List<AccessEventEntity>
    {
      NewEvent(AccessEventKind.PageView, now, "ip-a"),
      NewEvent(AccessEventKind.PageView, now.AddMinutes(1), "ip-a"),
      firefox,
      NewEvent(AccessEventKind.PageView, now, "ip-b"),
      NewEvent(AccessEventKind.FeatureUsage, now, "ip-z"),
    };

    Assert.Equal(3, AccessSessionizer.CountUniqueVisitors(events));
  }

  /// <summary>
  /// Only successful HTML page loads count as visits; APIs, widget polling, assets, and redirects do not.
  /// </summary>
  /// <param name="method">The HTTP method.</param>
  /// <param name="status">The response status.</param>
  /// <param name="contentType">The response content type.</param>
  /// <param name="path">The request path.</param>
  /// <param name="expected">Whether the request is recorded.</param>
  [Theory]
  [InlineData("GET", 200, "text/html; charset=utf-8", "/transactions", true)]
  [InlineData("GET", 200, "text/html", "/", true)]
  [InlineData("GET", 200, "application/json", "/api/widget/today", false)]
  [InlineData("GET", 200, "text/html", "/api/widget/today", false)]
  [InlineData("POST", 200, "text/html", "/auth/login-local", false)]
  [InlineData("GET", 302, "text/html", "/auth/logout", false)]
  [InlineData("GET", 200, "text/css", "/css/app.css", false)]
  [InlineData("GET", 200, "text/plain", "/health", false)]
  [InlineData("GET", 200, "text/html", "/_blazor/negotiate", false)]
  [InlineData("GET", 200, null, "/", false)]
  public void CapturePolicyRecordsOnlyPageLoads(string method, int status, string? contentType, string path, bool expected)
  {
    Assert.Equal(expected, AccessAnalyticsCapturePolicy.ShouldRecordPageLoad(method, status, contentType, new PathString(path)));
  }

  private static AccessEventEntity NewEvent(AccessEventKind kind, DateTime occurredAtUtc, string ipHash)
  {
    return new AccessEventEntity
    {
      Kind = kind,
      CreationDate = occurredAtUtc,
      IpHash = ipHash,
      Browser = "Chrome 128",
      OperatingSystem = "Windows 10",
      PathOrFeature = "test",
    };
  }

  private static async Task<List<AccessDailySummaryEntity>> RunRollupAsync(SqliteLifePlannerFactory factory, int expectedRawEvents, int expectedSummaries)
  {
    await using ServiceProvider provider = BuildProvider(factory);
    AccessAnalyticsHostedService service = new AccessAnalyticsHostedService(
      new AccessAnalyticsRecorder(new FakeWebHostEnvironment("Production")),
      provider.GetRequiredService<IServiceScopeFactory>(),
      new FakeGeoIpLookupService(null),
      NullLogger<AccessAnalyticsHostedService>.Instance);

    return await RunUntilAsync(
      service,
      factory,
      context => context.AccessEvents.Count() == expectedRawEvents
        ? context.AccessDailySummaries.ToList()
        : new List<AccessDailySummaryEntity>(),
      rows => rows.Count == expectedSummaries);
  }

  private static ServiceProvider BuildProvider(SqliteLifePlannerFactory factory)
  {
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<IDbContextFactory<LifePlannerContext>>(factory);
    return services.BuildServiceProvider();
  }

  private static async Task<List<T>> RunUntilAsync<T>(
    AccessAnalyticsHostedService service,
    SqliteLifePlannerFactory factory,
    Func<LifePlannerContext, List<T>> read,
    Func<List<T>, bool> done)
  {
    await service.StartAsync(CancellationToken.None);
    try
    {
      DateTime deadline = DateTime.UtcNow.AddSeconds(15);
      List<T> rows = new List<T>();
      while (DateTime.UtcNow < deadline)
      {
        await using LifePlannerContext context = await factory.CreateDbContextAsync();
        rows = read(context);
        if (done(rows))
        {
          return rows;
        }

        await Task.Delay(50);
      }

      return rows;
    }
    finally
    {
      await service.StopAsync(CancellationToken.None);
    }
  }
}
