//-----------------------------------------------------------------------
// <copyright file="FinancialSnapshotEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Stores an aggregate, non-sensitive financial position snapshot for planning history.
/// </summary>
public sealed class FinancialSnapshotEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the local snapshot date.</summary>
  public DateOnly SnapshotDate { get; set; }

  /// <summary>Gets or sets the UTC instant at which the snapshot was calculated.</summary>
  public DateTime CalculatedAtUtc { get; set; }

  /// <summary>Gets or sets total canonical net worth.</summary>
  public decimal NetWorth { get; set; }

  /// <summary>Gets or sets the amount valued from fresh or explicit current values.</summary>
  public decimal ConfirmedValue { get; set; }

  /// <summary>Gets or sets the amount valued through fallback data.</summary>
  public decimal EstimatedValue { get; set; }

  /// <summary>Gets or sets operating cash.</summary>
  public decimal OperatingCash { get; set; }

  /// <summary>Gets or sets the emergency reserve.</summary>
  public decimal EmergencyReserve { get; set; }

  /// <summary>Gets or sets idle cash.</summary>
  public decimal IdleCash { get; set; }

  /// <summary>Gets or sets long-term invested assets.</summary>
  public decimal LongTermInvestments { get; set; }

  /// <summary>Gets or sets operating income for the snapshot month.</summary>
  public decimal MonthlyOperatingIncome { get; set; }

  /// <summary>Gets or sets operating expenses for the snapshot month.</summary>
  public decimal MonthlyOperatingExpense { get; set; }

  /// <summary>Gets or sets contributions for the snapshot month.</summary>
  public decimal MonthlyContributions { get; set; }

  /// <summary>Gets or sets the number of positions that could not be valued.</summary>
  public int UnpricedPositionCount { get; set; }
}
