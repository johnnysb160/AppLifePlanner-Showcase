//-----------------------------------------------------------------------
// <copyright file="DemoDataRefreshHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Demo;

using System.Diagnostics;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Logging;
using Serilog.Context;

/// <summary>
/// Keeps the anonymous showcase current while the application stays online.
/// </summary>
/// <remarks>
/// Startup seeding alone leaves the showcase frozen at the deployment date: months pass, the
/// "this month" screens empty out and the habit streaks stop at a wall. This service re-runs the
/// seeder once a day, which re-anchors the time-based groups and restores anything that went missing.
/// </remarks>
public sealed class DemoDataRefreshHostedService : BackgroundService
{
  private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

  private readonly IServiceScopeFactory scopeFactory;
  private readonly ILogger<DemoDataRefreshHostedService> logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="DemoDataRefreshHostedService"/> class.
  /// </summary>
  /// <param name="scopeFactory">Factory used to resolve a scoped database context per run.</param>
  /// <param name="logger">Logger for the maintenance run.</param>
  public DemoDataRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoDataRefreshHostedService> logger)
  {
    this.scopeFactory = scopeFactory;
    this.logger = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // Startup already seeds once, so the first maintenance run waits a full interval.
    using PeriodicTimer timer = new PeriodicTimer(Interval);

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
        using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
        using IDisposable operationScope = LogContext.PushProperty("Operation", "Demo data refresh");

        try
        {
          using IServiceScope scope = this.scopeFactory.CreateScope();
          LifePlannerContext db = scope.ServiceProvider.GetRequiredService<LifePlannerContext>();
          await DemoDataSeeder.SeedAsync(db, this.logger, stoppingToken);
          this.logger.LogInformation(
            "Demo data refresh finished. Status={Status}. ElapsedMs={ElapsedMs}.",
            "Success",
            stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
          this.logger.LogError(
            exception,
            "Demo data refresh failed. Status={Status}. ElapsedMs={ElapsedMs}.",
            "Failed",
            stopwatch.ElapsedMilliseconds);
        }
      }
    }
    catch (OperationCanceledException)
    {
      // Normal shutdown.
    }
  }
}
