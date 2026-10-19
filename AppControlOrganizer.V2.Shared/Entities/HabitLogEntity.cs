//-----------------------------------------------------------------------
// <copyright file="HabitLogEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Records whether a habit was performed on a given calendar day.
  /// One row per (HabitId, LogDate) pair — unique constraint enforced in DB.
  /// </summary>
  public sealed class HabitLogEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent habit identifier.</summary>
    public int HabitId { get; set; }

    /// <summary>Navigation property to the parent habit.</summary>
    public HabitEntity? Habit { get; set; }

    /// <summary>Gets or sets the UTC date of this log entry (time component is ignored — always midnight UTC).</summary>
    public DateTime LogDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the habit was done on <see cref="LogDate"/>.</summary>
    public bool Done { get; set; }
  }
}
