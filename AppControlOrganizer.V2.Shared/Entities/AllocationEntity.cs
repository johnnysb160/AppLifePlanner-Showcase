//-----------------------------------------------------------------------
// <copyright file="AllocationEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents an allocation of assets or investments, including details such as type, unit values, currencies, and
/// associated account information.
/// </summary>
/// <remarks>Use this entity to model individual allocations within an account, capturing relevant financial and
/// descriptive information. Optional properties allow for flexibility in representing incomplete or in-progress
/// allocations. The entity includes navigation to the owning account and supports multiple currencies and profit
/// types.</remarks>
public class AllocationEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the allocation name.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets initials or short code for the allocation (optional).
  /// </summary>
  public string? Initials { get; set; }

  /// <summary>
  /// Gets or sets the allocation type.
  /// </summary>
  public AllocationType AllocationType { get; set; }

  /// <summary>
  /// Gets or sets the buy unit value for the allocation.
  /// </summary>
  public decimal BuyUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the quantity of units.
  /// </summary>
  public decimal Quantity { get; set; }

  /// <summary>
  /// Gets or sets the current unit value (optional).
  /// </summary>
  public decimal? CurrentUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the primary currency for the allocation.
  /// </summary>
  public Currency Currency { get; set; } = Currency.BRL;

  /// <summary>
  /// Gets or sets an optional secondary currency.
  /// </summary>
  public Currency? SecondCurrency { get; set; }

  /// <summary>
  /// Gets or sets the profit type (optional).
  /// </summary>
  public string? ProfitType { get; set; }

  /// <summary>
  /// Gets or sets the profit percentage (optional).
  /// </summary>
  public decimal? ProfitPercentage { get; set; }

  /// <summary>
  /// Gets or sets the profit date (optional).
  /// </summary>
  public DateTime? ProfitDate { get; set; }

  /// <summary>
  /// Gets or sets application/investment term (optional).
  /// </summary>
  public string? ApplicationTerm { get; set; }

  /// <summary>
  /// Gets or sets the expiration date (optional).
  /// </summary>
  public DateTime? ExpirationDate { get; set; }

  /// <summary>
  /// Gets or sets additional annotations or notes (optional).
  /// </summary>
  public string? Annotations { get; set; }

  /// <summary>
  /// Gets or sets the foreign key to the owning account.
  /// </summary>
  public int AccountId { get; set; }

  /// <summary>
  /// Navigation property to the owning account.
  /// </summary>
  [ForeignKey(nameof(AccountId))]
  public AccountEntity Account { get; set; } = null!;

  /// <summary>Gets or sets the role this position plays in financial planning.</summary>
  public FinancialPurpose PlanningPurpose { get; set; } = FinancialPurpose.Unclassified;
}
