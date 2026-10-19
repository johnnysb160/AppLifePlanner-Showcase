//-----------------------------------------------------------------------
// <copyright file="LogSafe.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Logging;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Provides small helpers for safe structured logging.
/// </summary>
public static class LogSafe
{
  /// <summary>
  /// Creates a short stable hash for a value that should not be written raw to logs.
  /// </summary>
  /// <param name="value">Sensitive or user-specific value.</param>
  /// <returns>A short hash, or <c>none</c> when the value is blank.</returns>
  public static string Hash(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return "none";
    }

    byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes, 0, 8);
  }

  /// <summary>
  /// Creates a correlation identifier for a background operation.
  /// </summary>
  /// <returns>A compact correlation identifier.</returns>
  public static string NewCorrelationId()
  {
    return Guid.NewGuid().ToString("N");
  }
}
