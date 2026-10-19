//-----------------------------------------------------------------------
// <copyright file="QuarterTaskEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Represents a single tactical checklist item within a quarterly plan.
  /// </summary>
  public sealed class QuarterTaskEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent quarter plan identifier.</summary>
    public int QuarterPlanId { get; set; }

    /// <summary>Navigation property to the parent quarter plan.</summary>
    public QuarterPlanEntity? QuarterPlan { get; set; }

    /// <summary>Gets or sets the task text / description.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the display sort order within the quarter plan.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether this task has been completed.</summary>
    public bool Done { get; set; }
  }
}
