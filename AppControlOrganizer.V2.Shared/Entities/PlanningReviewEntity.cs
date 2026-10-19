//-----------------------------------------------------------------------
// <copyright file="PlanningReviewEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents a compact weekly or monthly planning review.
/// </summary>
public sealed class PlanningReviewEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the review type.</summary>
  public ReviewType ReviewType { get; set; }

  /// <summary>Gets or sets the review status.</summary>
  public PlanningReviewStatus Status { get; set; }

  /// <summary>Gets or sets the local review date.</summary>
  public DateOnly ReviewDate { get; set; }

  /// <summary>Gets or sets what advanced during the period.</summary>
  public string ProgressNotes { get; set; } = string.Empty;

  /// <summary>Gets or sets what blocked progress during the period.</summary>
  public string BlockerNotes { get; set; } = string.Empty;

  /// <summary>Gets or sets the key decision made during the review.</summary>
  public string DecisionNotes { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional financial snapshot identifier.</summary>
  public int? FinancialSnapshotId { get; set; }

  /// <summary>Gets or sets the completion instant in UTC.</summary>
  public DateTime? CompletedAtUtc { get; set; }
}
