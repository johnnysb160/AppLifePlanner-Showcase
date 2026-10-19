//-----------------------------------------------------------------------
// <copyright file="DatabaseHealthService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

/// <summary>
/// Singleton that holds the current database health status for UI consumption.
/// </summary>
public sealed class DatabaseHealthService
{
  private readonly object lockObj = new();

  /// <summary>
  /// Gets whether the app is currently using the secondary Neon database.
  /// </summary>
  public bool IsUsingStandby { get; private set; }

  /// <summary>
  /// Gets whether the app is currently using the secondary Neon database.
  /// </summary>
  public bool IsUsingFallback
  {
    get
    {
      return IsUsingStandby;
    }
  }

  /// <summary>
  /// Gets whether the primary Supabase database is reachable.
  /// </summary>
  public bool IsPrimaryReachable { get; private set; }

  /// <summary>
  /// Gets whether the secondary Neon database is reachable.
  /// </summary>
  public bool IsStandbyReachable { get; private set; }

  /// <summary>
  /// Gets whether the recovery workflow has been triggered via GitHub API.
  /// </summary>
  public bool IsRecoveryTriggered { get; private set; }

  /// <summary>
  /// Gets the UTC timestamp of the last health probe.
  /// </summary>
  public DateTime? LastProbeUtc { get; private set; }

  /// <summary>
  /// Event raised when the health status changes.
  /// </summary>
  public event Action? OnStatusChanged;

  /// <summary>
  /// Updates the health status from the background probe.
  /// </summary>
  public void Update(bool isUsingStandby, bool isPrimaryReachable, bool isStandbyReachable)
  {
    lock (lockObj)
    {
      bool changed = IsUsingStandby != isUsingStandby
        || IsPrimaryReachable != isPrimaryReachable
        || IsStandbyReachable != isStandbyReachable;
      IsUsingStandby = isUsingStandby;
      IsPrimaryReachable = isPrimaryReachable;
      IsStandbyReachable = isStandbyReachable;
      LastProbeUtc = DateTime.UtcNow;

      if (changed)
      {
        OnStatusChanged?.Invoke();
      }
    }
  }

  /// <summary>
  /// Marks that the recovery workflow has been dispatched.
  /// </summary>
  public void MarkRecoveryTriggered()
  {
    lock (lockObj)
    {
      IsRecoveryTriggered = true;
      OnStatusChanged?.Invoke();
    }
  }
}
