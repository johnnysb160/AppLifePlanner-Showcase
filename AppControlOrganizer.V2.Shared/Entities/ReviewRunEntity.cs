//-----------------------------------------------------------------------
// <copyright file="ReviewRunEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents a single executed review session (a "run" of a review template).
  /// Stores per-domain scores and free-text notes for the session.
  /// </summary>
  public sealed class ReviewRunEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the template used for this run.</summary>
    public int TemplateId { get; set; }

    /// <summary>Navigation property to the review template.</summary>
    public ReviewTemplateEntity? Template { get; set; }

    /// <summary>Gets or sets the review cadence type (snapshot from template at run time).</summary>
    public ReviewType ReviewType { get; set; } = ReviewType.Weekly;

    /// <summary>Gets or sets the UTC date on which the review was performed.</summary>
    public DateTime RunDate { get; set; }

    /// <summary>Gets or sets general notes / reflections for this review.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the Energy score (0–10).</summary>
    public int? ScoreEnergy { get; set; }

    /// <summary>Gets or sets the Focus / productivity score (0–10).</summary>
    public int? ScoreFocus { get; set; }

    /// <summary>Gets or sets the Finances score (0–10).</summary>
    public int? ScoreFinances { get; set; }

    /// <summary>Gets or sets the Relationships / social score (0–10).</summary>
    public int? ScoreRelations { get; set; }

    /// <summary>Navigation property — the checklist results for this run.</summary>
    public List<ReviewRunItemEntity> Items { get; set; } = new();
  }
}
