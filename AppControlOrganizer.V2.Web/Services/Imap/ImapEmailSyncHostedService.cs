//-----------------------------------------------------------------------
// <copyright file="ImapEmailSyncHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Imap;

using System.Diagnostics;
using AppControlOrganizer.V2.Web.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Context;

/// <summary>
/// Background hosted service that periodically invokes <see cref="IImapEmailSyncService"/>
/// to poll all configured IMAP accounts and import unread bank transaction emails.
/// Uses a non-overlapping semaphore to prevent concurrent sync runs.
/// </summary>
public sealed class ImapEmailSyncHostedService : BackgroundService
{
  private static readonly SemaphoreSlim OverlapGuard = new SemaphoreSlim(1, 1);
  private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

  private readonly IServiceScopeFactory scopeFactory;
  private readonly ILogger<ImapEmailSyncHostedService> logger;

  /// <summary>
  /// Initializes a new instance of <see cref="ImapEmailSyncHostedService"/>.
  /// </summary>
  /// <param name="scopeFactory">Factory used to create scoped DI scopes for each sync cycle.</param>
  /// <param name="logger">Logger for diagnostic output.</param>
  public ImapEmailSyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ImapEmailSyncHostedService> logger)
  {
    this.scopeFactory = scopeFactory;
    this.logger = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    logger.LogInformation(
      "ImapEmailSyncHostedService started. Interval: {Hours} hour(s).",
      TickInterval.TotalHours);

    // Run once on startup so the first sync does not wait for the full interval
    await TryRunOnceAsync(stoppingToken);

    using PeriodicTimer timer = new PeriodicTimer(TickInterval);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        bool waited = await timer.WaitForNextTickAsync(stoppingToken);
        if (waited)
        {
          await TryRunOnceAsync(stoppingToken);
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Unexpected error in IMAP sync timer loop. Will retry on next tick.");
      }
    }

    logger.LogInformation("ImapEmailSyncHostedService stopped.");
  }

  // ── Private helpers ─────────────────────────────────────────────────────

  private async Task TryRunOnceAsync(CancellationToken ct)
  {
    bool acquired = await OverlapGuard.WaitAsync(0, ct);
    if (!acquired)
    {
      logger.LogWarning("Skipping IMAP sync tick: previous execution still running.");
      return;
    }

    try
    {
      await RunOnceAsync(ct);
    }
    finally
    {
      OverlapGuard.Release();
    }
  }

  private async Task RunOnceAsync(CancellationToken ct)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
    using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
    using IDisposable operationScope = LogContext.PushProperty("Operation", "IMAP sync");

    try
    {
      logger.LogInformation("IMAP email sync tick started.");

      using IServiceScope scope = scopeFactory.CreateScope();
      IImapEmailSyncService syncService =
        scope.ServiceProvider.GetRequiredService<IImapEmailSyncService>();

      await syncService.SyncAllUsersAsync(ct);

      logger.LogInformation(
        "IMAP email sync tick finished. Status={Status}. ElapsedMs={ElapsedMs}.",
        "Success",
        stopwatch.ElapsedMilliseconds);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(
        ex,
        "IMAP email sync tick failed. Status={Status}. ElapsedMs={ElapsedMs}.",
        "Failed",
        stopwatch.ElapsedMilliseconds);
      throw;
    }
  }
}
