//-----------------------------------------------------------------------
// <copyright file="ImportProgressInfo.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

/// <summary>
/// Represents the progress status of an import operation, including the completion percentage and a descriptive
/// message.
/// </summary>
/// <remarks>Use this class to communicate the current state of an import process, such as when reporting progress
/// updates to a user interface or logging system. The Percent property indicates how much of the operation has
/// completed, while the Message property provides additional context or status information.</remarks>
public sealed class ImportProgressInfo
{
  /// <summary>
  /// Gets or sets the percentage value associated with the current instance.
  /// </summary>
  /// <remarks>The value must be between 0 and 100, inclusive. Setting a value outside this range will result in
  /// an exception.</remarks>
  public int Percent { get; set; }

  /// <summary>
  /// Gets or sets the message content associated with the current instance.
  /// </summary>
  public string Message { get; set; } = string.Empty;
}
