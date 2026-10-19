//-----------------------------------------------------------------------
// <copyright file="RoutineBlockEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents a reference block in a weekly routine.
/// </summary>
public sealed class RoutineBlockEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the parent template identifier.</summary>
  public int RoutineTemplateId { get; set; }

  /// <summary>Gets or sets the parent template.</summary>
  public RoutineTemplateEntity? RoutineTemplate { get; set; }

  /// <summary>Gets or sets the day of week.</summary>
  public DayOfWeek DayOfWeek { get; set; }

  /// <summary>Gets or sets the local start time.</summary>
  public TimeOnly StartTime { get; set; }

  /// <summary>Gets or sets the local end time.</summary>
  public TimeOnly EndTime { get; set; }

  /// <summary>Gets or sets the block title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets an optional short category.</summary>
  public string? Category { get; set; }

  /// <summary>Gets or sets a Bootstrap icon class.</summary>
  public string IconCss { get; set; } = "bi bi-circle";

  /// <summary>Gets or sets a CSS hexadecimal color.</summary>
  public string ColorHex { get; set; } = "#64748b";

  /// <summary>Gets or sets the display order.</summary>
  public int SortOrder { get; set; }
}
