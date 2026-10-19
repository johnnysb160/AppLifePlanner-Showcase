//-----------------------------------------------------------------------
// <copyright file="QuarterPlanEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents a quarterly execution plan (Q1–Q4 for a given year).
  /// </summary>
  public sealed class QuarterPlanEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional annual plan identifier.</summary>
    public int? AnnualPlanId { get; set; }

    /// <summary>Gets or sets the optional objective identifier.</summary>
    public int? ObjectiveId { get; set; }

    /// <summary>Gets or sets the calendar year (e.g. 2026).</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the quarter number (1–4).</summary>
    public int Quarter { get; set; }

    /// <summary>Gets or sets the single focus / theme for the quarter.</summary>
    public string Focus { get; set; } = string.Empty;

    /// <summary>Gets or sets the key deliverable for the quarter.</summary>
    public string? Delivery { get; set; }

    /// <summary>Gets or sets the target completion date (UTC).</summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>Gets or sets a description of the weekly execution cadence.</summary>
    public string? WeeklyCadence { get; set; }

    /// <summary>Gets or sets the KPI description for this quarter.</summary>
    public string? KpiDescription { get; set; }

    /// <summary>Gets or sets the RAG status (manual).</summary>
    public RagStatus Status { get; set; } = RagStatus.Amber;

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property — tactical checklist items for this quarter.</summary>
    public List<QuarterTaskEntity> Tasks { get; set; } = new();
  }
}
