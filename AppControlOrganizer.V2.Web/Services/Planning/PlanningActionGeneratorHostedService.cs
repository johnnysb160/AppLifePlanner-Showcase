//-----------------------------------------------------------------------
// <copyright file="PlanningActionGeneratorHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using System.Diagnostics;
using AppControlOrganizer.V2.Web.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Context;

/// <summary>
/// Background hosted service that refreshes deterministic planning actions once per day.
/// </summary>
public sealed class PlanningActionGeneratorHostedService : BackgroundService
{
  private static readonly SemaphoreSlim OverlapGuard = new SemaphoreSlim(1, 1);
  private static readonly TimeSpan TickInterval = TimeSpan.FromDays(1);

  private readonly IServiceScopeFactory scopeFactory;
  private readonly ILogger<PlanningActionGeneratorHostedService> logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="PlanningActionGeneratorHostedService"/> class.
  /// </summary>
  /// <param name="scopeFactory">Factory used to create scoped DI scopes for each cycle.</param>
  /// <param name="logger">Logger for diagnostics.</param>
  public PlanningActionGeneratorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PlanningActionGeneratorHostedService> logger)
  {
    this.scopeFactory = scopeFactory;
    this.logger = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    this.logger.LogInformation(
      "PlanningActionGeneratorHostedService started. Interval: {Hours} hour(s).",
      TickInterval.TotalHours);

    await this.TryRunOnceAsync(stoppingToken);

    using PeriodicTimer timer = new PeriodicTimer(TickInterval);
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        bool waited = await timer.WaitForNextTickAsync(stoppingToken);
        if (waited)
        {
          await this.TryRunOnceAsync(stoppingToken);
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception exception)
      {
        this.logger.LogError(exception, "Unexpected error in planning action timer loop. Will retry on next tick.");
      }
    }

    this.logger.LogInformation("PlanningActionGeneratorHostedService stopped.");
  }

  private async Task TryRunOnceAsync(CancellationToken cancellationToken)
  {
    bool acquired = await OverlapGuard.WaitAsync(0, cancellationToken);
    if (!acquired)
    {
      this.logger.LogWarning("Skipping planning action tick: previous execution still running.");
      return;
    }

    try
    {
      await this.RunOnceAsync(cancellationToken);
    }
    finally
    {
      OverlapGuard.Release();
    }
  }

  private async Task RunOnceAsync(CancellationToken cancellationToken)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
    using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
    using IDisposable operationScope = LogContext.PushProperty("Operation", "Planning action generation");

    try
    {
      this.logger.LogInformation("Planning action generation tick started.");

      using IServiceScope scope = this.scopeFactory.CreateScope();
      IPlanningActionService service =
        scope.ServiceProvider.GetRequiredService<IPlanningActionService>();
      await service.GenerateForAllUsersAsync(cancellationToken);

      this.logger.LogInformation(
        "Planning action generation tick finished. Status={Status}. ElapsedMs={ElapsedMs}.",
        "Success",
        stopwatch.ElapsedMilliseconds);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      this.logger.LogError(
        exception,
        "Planning action generation tick failed. Status={Status}. ElapsedMs={ElapsedMs}.",
        "Failed",
        stopwatch.ElapsedMilliseconds);
      throw;
    }
  }
}
