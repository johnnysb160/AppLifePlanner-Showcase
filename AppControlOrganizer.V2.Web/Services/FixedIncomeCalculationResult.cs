//-----------------------------------------------------------------------
// <copyright file="FixedIncomeCalculationResult.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

/// <summary>
/// Represents a fixed-income yield calculation result and the BACEN period used to produce it.
/// </summary>
public sealed class FixedIncomeCalculationResult
{
  /// <summary>
  /// Gets or sets the calculated current unit value.
  /// </summary>
  public decimal CurrentUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the index source label used for the calculation.
  /// </summary>
  public string Source { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the first published index date used.
  /// </summary>
  public DateTime? FirstRateDate { get; set; }

  /// <summary>
  /// Gets or sets the last published index date used.
  /// </summary>
  public DateTime? LastRateDate { get; set; }

  /// <summary>
  /// Gets or sets the number of published rates used.
  /// </summary>
  public int RateCount { get; set; }

  /// <summary>
  /// Gets or sets the calculation period start date.
  /// </summary>
  public DateTime StartDate { get; set; }

  /// <summary>
  /// Gets or sets the calculation target date.
  /// </summary>
  public DateTime EndDate { get; set; }
}
