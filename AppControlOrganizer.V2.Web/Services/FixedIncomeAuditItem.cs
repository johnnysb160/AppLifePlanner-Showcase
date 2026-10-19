//-----------------------------------------------------------------------
// <copyright file="FixedIncomeAuditItem.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents the diagnostic status of one fixed-income allocation.
/// </summary>
public sealed class FixedIncomeAuditItem
{
  /// <summary>
  /// Gets or sets the audited allocation.
  /// </summary>
  public AllocationEntity Allocation { get; set; } = null!;

  /// <summary>
  /// Gets or sets whether the allocation could be calculated.
  /// </summary>
  public bool CanCalculate { get; set; }

  /// <summary>
  /// Gets or sets whether the saved value diverges from the expected value.
  /// </summary>
  public bool HasDivergence { get; set; }

  /// <summary>
  /// Gets or sets the applied total used by the system.
  /// </summary>
  public decimal AppliedTotal { get; set; }

  /// <summary>
  /// Gets or sets the current saved total used by the system.
  /// </summary>
  public decimal SavedCurrentTotal { get; set; }

  /// <summary>
  /// Gets or sets the expected total calculated from BACEN indexes.
  /// </summary>
  public decimal? ExpectedCurrentTotal { get; set; }

  /// <summary>
  /// Gets or sets the absolute difference between saved and expected totals.
  /// </summary>
  public decimal? Difference { get; set; }

  /// <summary>
  /// Gets or sets the percentage difference between saved and expected totals.
  /// </summary>
  public decimal? DifferencePercentage { get; set; }

  /// <summary>
  /// Gets or sets the source used for the calculation.
  /// </summary>
  public string Source { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the last BACEN rate date used.
  /// </summary>
  public DateTime? LastRateDate { get; set; }

  /// <summary>
  /// Gets or sets the number of rates used.
  /// </summary>
  public int RateCount { get; set; }

  /// <summary>
  /// Gets or sets the localized diagnostic key.
  /// </summary>
  public string DiagnosticKey { get; set; } = string.Empty;
}
