//-----------------------------------------------------------------------
// <copyright file="DatabaseHealthCheckHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using System.Diagnostics;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Logging;
using Npgsql;
using Serilog.Context;

/// <summary>
/// Background service that periodically probes Supabase and Neon availability.
/// </summary>
public sealed class DatabaseHealthCheckHostedService : BackgroundService
{
  private static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(30);
  private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
  private static readonly int ProbeTimeoutSeconds = 5;

  private readonly DatabaseHealthService healthService;
  private readonly ConfigurationsValues config;
  private readonly ILogger<DatabaseHealthCheckHostedService> logger;

  public DatabaseHealthCheckHostedService(
    DatabaseHealthService healthService,
    ConfigurationsValues config,
    ILogger<DatabaseHealthCheckHostedService> logger)
  {
    this.healthService = healthService;
    this.config = config;
    this.logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    bool initialSupabaseProbe = await ProbeSupabaseAsync(stoppingToken);
    bool initialNeonProbe = await ProbeNeonAsync(stoppingToken);
    bool isUsingStandby = ConfigurationIOC.IsRuntimeUsingStandby;

    healthService.Update(isUsingStandby, initialSupabaseProbe, initialNeonProbe);

    await Task.Delay(InitialDelay, stoppingToken);

    while (!stoppingToken.IsCancellationRequested)
    {
      Stopwatch stopwatch = Stopwatch.StartNew();
      using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
      using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
      using IDisposable operationScope = LogContext.PushProperty("Operation", "Database health probe");

      try
      {
        bool supabaseReachable = await ProbeSupabaseAsync(stoppingToken);
        bool neonReachable = await ProbeNeonAsync(stoppingToken);
        isUsingStandby = ConfigurationIOC.IsRuntimeUsingStandby;

        healthService.Update(isUsingStandby, supabaseReachable, neonReachable);

        if (isUsingStandby)
        {
          logger.LogWarning(
            "Database health probe finished. Status={Status}. PrimaryReachable={PrimaryReachable}. StandbyReachable={StandbyReachable}. RuntimeUsingStandby={RuntimeUsingStandby}. ElapsedMs={ElapsedMs}.",
            "Standby",
            supabaseReachable,
            neonReachable,
            isUsingStandby,
            stopwatch.ElapsedMilliseconds);
        }
        else if (supabaseReachable && neonReachable)
        {
          logger.LogInformation(
            "Database health probe finished. Status={Status}. PrimaryReachable={PrimaryReachable}. StandbyReachable={StandbyReachable}. RuntimeUsingStandby={RuntimeUsingStandby}. ElapsedMs={ElapsedMs}.",
            "Healthy",
            supabaseReachable,
            neonReachable,
            isUsingStandby,
            stopwatch.ElapsedMilliseconds);
        }
        else if (supabaseReachable)
        {
          logger.LogInformation(
            "Database health probe finished. Status={Status}. PrimaryReachable={PrimaryReachable}. StandbyReachable={StandbyReachable}. RuntimeUsingStandby={RuntimeUsingStandby}. ElapsedMs={ElapsedMs}.",
            "PrimaryOnly",
            supabaseReachable,
            neonReachable,
            isUsingStandby,
            stopwatch.ElapsedMilliseconds);
        }
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.LogError(
          ex,
          "Database health probe failed. Status={Status}. ElapsedMs={ElapsedMs}.",
          "Failed",
          stopwatch.ElapsedMilliseconds);
      }

      await Task.Delay(ProbeInterval, stoppingToken);
    }
  }

  private async Task<bool> ProbeNeonAsync(CancellationToken ct)
  {
    return await ProbeConnectionAsync(config.NeonConnectionString, ct);
  }

  private async Task<bool> ProbeSupabaseAsync(CancellationToken ct)
  {
    return await ProbeConnectionAsync(config.SupabaseConnectionString, ct);
  }

  private async Task<bool> ProbeConnectionAsync(string connectionString, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      return false;
    }

    try
    {
      NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(connectionString)
      {
        Timeout = ProbeTimeoutSeconds
      };

      using NpgsqlConnection conn = new NpgsqlConnection(builder.ToString());
      await conn.OpenAsync(ct);
      using NpgsqlCommand cmd = new NpgsqlCommand("SELECT 1", conn);
      await cmd.ExecuteScalarAsync(ct);
      return true;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch
    {
      return false;
    }
  }
}
