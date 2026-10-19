//-----------------------------------------------------------------------
// <copyright file="AppSetting.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Simple key-value setting storage for application configuration flags.
/// </summary>
public class AppSetting
{
  /// <summary>
  /// The unique key for this setting.
  /// </summary>
  public string Key { get; set; } = string.Empty;

  /// <summary>
  /// The value of this setting.
  /// </summary>
  public string? Value { get; set; }
}
