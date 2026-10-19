//-----------------------------------------------------------------------
// <copyright file="KeyResultEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents a measurable Key Result within an OKR Objective.
  /// </summary>
  public sealed class KeyResultEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent objective identifier.</summary>
    public int ObjectiveId { get; set; }

    /// <summary>Navigation property to the parent objective.</summary>
    public ObjectiveEntity? Objective { get; set; }

    /// <summary>Gets or sets the key result title / description.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the unit label for the metric (e.g. "kg", "R$", "%", "h/week").</summary>
    public string? MetricUnit { get; set; }

    /// <summary>Gets or sets the starting baseline value.</summary>
    public decimal Baseline { get; set; }

    /// <summary>Gets or sets the target value to achieve.</summary>
    public decimal Target { get; set; }

    /// <summary>Gets or sets the current / latest measured value.</summary>
    public decimal Current { get; set; }

    /// <summary>Gets or sets a free-text weekly progress note.</summary>
    public string? WeeklyNote { get; set; }

    /// <summary>Gets or sets the target completion deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Gets or sets the RAG status (manual).</summary>
    public RagStatus Status { get; set; } = RagStatus.Amber;

    /// <summary>Gets or sets the display sort order within the objective.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the automatic financial metric source, or manual for user-entered progress.</summary>
    public FinancialMetricSource MetricSource { get; set; } = FinancialMetricSource.Manual;
  }
}
