//-----------------------------------------------------------------------
// <copyright file="WeeklyFocusEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents one of the user's limited weekly priorities.
/// </summary>
public sealed class WeeklyFocusEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the local date of the first day of the week.</summary>
  public DateOnly WeekStartDate { get; set; }

  /// <summary>Gets or sets the focus text.</summary>
  public string Text { get; set; } = string.Empty;

  /// <summary>Gets or sets the display order from one to three.</summary>
  public int SortOrder { get; set; }

  /// <summary>Gets or sets an optional objective link.</summary>
  public int? ObjectiveId { get; set; }

  /// <summary>Gets or sets an optional key-result link.</summary>
  public int? KeyResultId { get; set; }

  /// <summary>Gets or sets an optional quarter-task link.</summary>
  public int? QuarterTaskId { get; set; }

  /// <summary>Gets or sets an optional routine-block link used as short context.</summary>
  public int? RoutineBlockId { get; set; }
}
