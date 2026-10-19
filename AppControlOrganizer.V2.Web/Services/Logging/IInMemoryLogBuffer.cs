//-----------------------------------------------------------------------
// <copyright file="IInMemoryLogBuffer.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Logging;

/// <summary>
/// Exposes recently rendered log lines captured in-process for read-only diagnostics.
/// </summary>
public interface IInMemoryLogBuffer
{
  /// <summary>
  /// Gets the most recent rendered log lines, oldest first.
  /// </summary>
  /// <returns>A snapshot of the currently buffered lines.</returns>
  IReadOnlyList<string> GetRecentLines();
}
