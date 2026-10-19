//-----------------------------------------------------------------------
// <copyright file="AnnualPlanEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Stores the decisions and assumptions for a user's annual plan.
/// </summary>
public sealed class AnnualPlanEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the plan year.</summary>
  public int Year { get; set; }

  /// <summary>Gets or sets the short theme for the year.</summary>
  public string Theme { get; set; } = string.Empty;

  /// <summary>Gets or sets the target net worth.</summary>
  public decimal TargetNetWorth { get; set; }

  /// <summary>Gets or sets the planned monthly income.</summary>
  public decimal? PlannedMonthlyIncome { get; set; }

  /// <summary>Gets or sets the desired monthly operating-spending ceiling.</summary>
  public decimal? MonthlySpendingCeiling { get; set; }

  /// <summary>Gets or sets planned annual expenses that are not part of the ordinary monthly baseline.</summary>
  public decimal PlannedAnnualExpenses { get; set; }

  /// <summary>Gets or sets an optional monthly contribution override.</summary>
  public decimal? PlannedMonthlyContribution { get; set; }

  /// <summary>Gets or sets planned one-off net cash flows before year end.</summary>
  public decimal PlannedOneOffNetFlows { get; set; }

  /// <summary>Gets or sets the desired emergency-reserve duration in months.</summary>
  public int EmergencyReserveMonths { get; set; } = 6;

  /// <summary>Gets or sets the version of the one-time reference data already applied to this plan.</summary>
  public int InitialDataVersion { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the most recent change.</summary>
  public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
