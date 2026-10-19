//-----------------------------------------------------------------------
// <copyright file="AccessAnalyticsHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Analytics;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;
using UAParser;

/// <summary>
/// Background service that drains <see cref="AccessAnalyticsRecorder"/>'s pending queue into
/// <see cref="AccessEventEntity"/> rows (enriching each with parsed browser/device and resolved
/// country), and once per day builds the previous day's <see cref="AccessDailySummaryEntity"/> and
/// prunes raw rows past the retention window. Mirrors the tick/scope/cleanup shape of
/// <see cref="MarketQuoteUpdaterHostedService"/>.
/// </summary>
public sealed class AccessAnalyticsHostedService : BackgroundService
{
  private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
  private static readonly int RetentionDays = 30;
  private static readonly string[] BotMarkers = ["bot", "crawler", "spider", "http-client", "curl", "python-requests"];

  private readonly AccessAnalyticsRecorder recorder;
  private readonly IServiceScopeFactory scopeFactory;
  private readonly IGeoIpLookupService geoIp;
  private readonly ILogger<AccessAnalyticsHostedService> logger;
  private readonly Parser userAgentParser = Parser.GetDefault();

  private DateOnly? lastSummarizedTickDate;

  /// <summary>
  /// Initializes a new instance of the <see cref="AccessAnalyticsHostedService"/> class.
  /// </summary>
  /// <param name="recorder">The recorder whose pending queue is drained on every tick.</param>
  /// <param name="scopeFactory">Factory used to create a scoped <see cref="LifePlannerContext"/> per tick.</param>
  /// <param name="geoIp">The GeoIP lookup service used to resolve visitor country.</param>
  /// <param name="logger">Logger for tick outcomes and failures.</param>
  public AccessAnalyticsHostedService(
    AccessAnalyticsRecorder recorder,
    IServiceScopeFactory scopeFactory,
    IGeoIpLookupService geoIp,
    ILogger<AccessAnalyticsHostedService> logger)
  {
    this.recorder = recorder;
    this.scopeFactory = scopeFactory;
    this.geoIp = geoIp;
    this.logger = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
      using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
      using IDisposable operationScope = LogContext.PushProperty("Operation", "Access analytics ingest");

      try
      {
        await IngestPendingEventsAsync(stoppingToken);
        await RollUpAndPruneAsync(stoppingToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.LogError(ex, "Access analytics tick failed.");
      }

      await Task.Delay(TickInterval, stoppingToken);
    }
  }

  private async Task IngestPendingEventsAsync(CancellationToken stoppingToken)
  {
    IReadOnlyList<PendingAccessEvent> pending = recorder.DrainPending();
    if (pending.Count == 0)
    {
      return;
    }

    List<AccessEventEntity> entities = new List<AccessEventEntity>(pending.Count);
    foreach (PendingAccessEvent item in pending)
    {
      ClientInfo? clientInfo = string.IsNullOrWhiteSpace(item.UserAgent)
        ? null
        : userAgentParser.Parse(item.UserAgent);
      geoIp.TryLookup(item.Ip, out string? country);

      entities.Add(new AccessEventEntity
      {
        CreationDate = item.OccurredAtUtc,
        Kind = item.Kind,
        UserIdHash = LogSafe.Hash(item.UserId),
        IpHash = LogSafe.Hash(item.Ip),
        Country = country,
        Browser = clientInfo is null ? null : $"{clientInfo.UA.Family} {clientInfo.UA.Major}".Trim(),
        OperatingSystem = clientInfo is null ? null : $"{clientInfo.OS.Family} {clientInfo.OS.Major}".Trim(),
        DeviceType = ClassifyDeviceType(clientInfo),
        PathOrFeature = item.PathOrFeature,
        SessionId = item.SessionId,
      });
    }

    using IServiceScope scope = scopeFactory.CreateScope();
    IDbContextFactory<LifePlannerContext> contextFactory =
      scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();
    await using LifePlannerContext context = await contextFactory.CreateDbContextAsync(stoppingToken);
    context.AccessEvents.AddRange(entities);
    await context.SaveChangesAsync(stoppingToken);

    logger.LogInformation("Ingested {Count} access analytics events.", entities.Count);
  }

  private static string ClassifyDeviceType(ClientInfo? clientInfo)
  {
    if (clientInfo is null)
    {
      return "Unknown";
    }

    string device = clientInfo.Device.Family ?? string.Empty;
    string os = clientInfo.OS.Family ?? string.Empty;
    string browser = clientInfo.UA.Family ?? string.Empty;

    if (device.Equals("Spider", StringComparison.OrdinalIgnoreCase) || BotMarkers.Any(marker => browser.Contains(marker, StringComparison.OrdinalIgnoreCase)))
    {
      return "Bot";
    }

    if (device.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
        device.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
    {
      return "Tablet";
    }

    if (os.Contains("iOS", StringComparison.OrdinalIgnoreCase) ||
        os.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
        device.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
    {
      return "Mobile";
    }

    return "Desktop";
  }

  private async Task RollUpAndPruneAsync(CancellationToken stoppingToken)
  {
    DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (lastSummarizedTickDate == today)
    {
      return;
    }

    using IServiceScope scope = scopeFactory.CreateScope();
    IDbContextFactory<LifePlannerContext> contextFactory =
      scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();
    await using LifePlannerContext context = await contextFactory.CreateDbContextAsync(stoppingToken);

    DateOnly yesterday = today.AddDays(-1);
    DateOnly oldestKept = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-RetentionDays));

    // Catch up on every missing day still covered by raw events, so a sleeping instance never loses a day.
    HashSet<DateOnly> summarized = (await context.AccessDailySummaries
      .Where(s => s.SummaryDate >= oldestKept)
      .Select(s => s.SummaryDate)
      .ToListAsync(stoppingToken)).ToHashSet();

    for (DateOnly day = oldestKept; day <= yesterday; day = day.AddDays(1))
    {
      if (!summarized.Contains(day))
      {
        await BuildDailySummaryAsync(context, day, stoppingToken);
      }
    }

    DateTime cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);

    // Left as a direct DbContext operation to ensure a hard delete (ExecuteDeleteAsync), matching
    // MarketQuoteUpdaterHostedService.CleanupOldQuotesAsync, since the repository layer only soft-deletes.
    int deletedCount = await context.AccessEvents
      .Where(e => e.CreationDate < cutoffUtc)
      .ExecuteDeleteAsync(stoppingToken);

    if (deletedCount > 0)
    {
      logger.LogInformation("Pruned {Count} access analytics events older than {CutoffDate:u}.", deletedCount, cutoffUtc);
    }

    lastSummarizedTickDate = today;
  }

  private async Task BuildDailySummaryAsync(LifePlannerContext context, DateOnly summaryDate, CancellationToken stoppingToken)
  {
    DateTime rangeStartUtc = summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    DateTime rangeEndUtc = rangeStartUtc.AddDays(1);

    List<AccessEventEntity> dayEvents = await context.AccessEvents
      .Where(e => e.CreationDate >= rangeStartUtc && e.CreationDate < rangeEndUtc)
      .ToListAsync(stoppingToken);

    if (dayEvents.Count == 0)
    {
      return;
    }

    IReadOnlyList<long> sessionDurations = AccessSessionizer.BuildSessionDurationsMs(dayEvents);

    context.AccessDailySummaries.Add(new AccessDailySummaryEntity
    {
      SummaryDate = summaryDate,
      UniqueVisitors = AccessSessionizer.CountUniqueVisitors(dayEvents),
      Sessions = sessionDurations.Count,
      PageViews = dayEvents.Count(e => e.Kind == AccessEventKind.PageView),
      FeatureUsageCount = dayEvents.Count(e => e.Kind == AccessEventKind.FeatureUsage),
      AvgSessionDurationMs = sessionDurations.Count > 0 ? (long)sessionDurations.Average() : 0,
    });

    await context.SaveChangesAsync(stoppingToken);
    logger.LogInformation("Built access analytics daily summary for {Date:yyyy-MM-dd}.", summaryDate);
  }
}
