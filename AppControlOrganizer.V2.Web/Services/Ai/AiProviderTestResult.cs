//-----------------------------------------------------------------------
// <copyright file="AiProviderTestResult.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Ai;

/// <summary>
/// Represents the outcome of a single AI provider connectivity test.
/// </summary>
public sealed class AiProviderTestResult
{
  /// <summary>Gets a value indicating whether the connection test succeeded.</summary>
  public bool Succeeded { get; init; }

  /// <summary>Gets the human-readable result message (safe to display; never contains secrets).</summary>
  public string Message { get; init; } = string.Empty;

  /// <summary>Gets the UTC timestamp at which the test was performed.</summary>
  public DateTime TestedAtUtc { get; init; } = DateTime.UtcNow;
}
