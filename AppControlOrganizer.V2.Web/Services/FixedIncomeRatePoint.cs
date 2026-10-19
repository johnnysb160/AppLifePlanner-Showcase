//-----------------------------------------------------------------------
// <copyright file="FixedIncomeRatePoint.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

/// <summary>
/// Represents one published fixed-income index rate.
/// </summary>
public sealed class FixedIncomeRatePoint
{
  /// <summary>
  /// Gets or sets the published rate date.
  /// </summary>
  public DateTime Date { get; set; }

  /// <summary>
  /// Gets or sets the published rate value as a percentage.
  /// </summary>
  public decimal RatePercentage { get; set; }
}
