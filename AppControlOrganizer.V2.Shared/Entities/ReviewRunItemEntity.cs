//-----------------------------------------------------------------------
// <copyright file="ReviewRunItemEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Records the checked/unchecked state of one template item within a <see cref="ReviewRunEntity"/>.
  /// The item text is snapshotted so edits to the template do not alter historical runs.
  /// </summary>
  public sealed class ReviewRunItemEntity : BaseEntity
  {
    /// <summary>Gets or sets the parent review run identifier.</summary>
    public int ReviewRunId { get; set; }

    /// <summary>Navigation property to the parent review run.</summary>
    public ReviewRunEntity? ReviewRun { get; set; }

    /// <summary>Gets or sets the original template item identifier (nullable — item may have been deleted).</summary>
    public int? TemplateItemId { get; set; }

    /// <summary>Gets or sets the snapshotted item text at the time the run was started.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the sort order (matches the template item order at run creation).</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether this checklist item was marked as done.</summary>
    public bool Done { get; set; }
  }
}
