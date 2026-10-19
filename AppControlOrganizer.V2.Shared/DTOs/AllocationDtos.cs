//-----------------------------------------------------------------------
// <copyright file="AllocationDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

using System.ComponentModel.DataAnnotations;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents the data required to create a new allocation, including financial details and associated account information.
/// </summary>
public class CreateAllocationDto
{
  /// <summary>
  /// Gets or sets the allocation name.
  /// </summary>
  [Required(ErrorMessage = "Name is required")]
  [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets initials or short code for the allocation (optional).
  /// </summary>
  public string? Initials { get; set; }

  /// <summary>
  /// Gets or sets the allocation type.
  /// </summary>
  [Required(ErrorMessage = "Allocation type is required")]
  public AllocationType AllocationType { get; set; }

  /// <summary>Gets or sets the optional planning purpose used by canonical financial totals.</summary>
  public FinancialPurpose PlanningPurpose { get; set; }

  /// <summary>
  /// Gets or sets the buy unit value for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Buy unit value is required")]
  [Range(0.01, double.MaxValue, ErrorMessage = "Buy unit value must be greater than zero")]
  public decimal BuyUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the quantity of units for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Quantity is required")]
  [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
  public decimal Quantity { get; set; }

  /// <summary>
  /// Gets or sets the current unit value (optional).
  /// </summary>
  public decimal? CurrentUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the currency for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Currency is required")]
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
  /// Gets or sets the application term or investment term (optional).
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
  /// Gets or sets the identifier of the associated account.
  /// </summary>
  [Required(ErrorMessage = "Account is required")]
  public int AccountId { get; set; }
}

/// <summary>
/// Represents the data required to update an existing allocation, including allocation details, financial values, and
/// related metadata.
/// </summary>
/// <remarks>This data transfer object is typically used in update operations to modify allocation records. All
/// required fields must be provided to ensure a valid update. Optional fields may be omitted if no changes are needed
/// for those values.</remarks>
public class UpdateAllocationDto
{
  /// <summary>
  /// Gets or sets the allocation name.
  /// </summary>
  [Required(ErrorMessage = "Name is required")]
  [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets initials or short code for the allocation (optional).
  /// </summary>
  public string? Initials { get; set; }

  /// <summary>
  /// Gets or sets the allocation type.
  /// </summary>
  [Required(ErrorMessage = "Allocation type is required")]
  public AllocationType AllocationType { get; set; }

  /// <summary>Gets or sets the optional planning purpose used by canonical financial totals.</summary>
  public FinancialPurpose PlanningPurpose { get; set; }

  /// <summary>
  /// Gets or sets the buy unit value for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Buy unit value is required")]
  [Range(0.01, double.MaxValue, ErrorMessage = "Buy unit value must be greater than zero")]
  public decimal BuyUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the quantity of units for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Quantity is required")]
  [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
  public decimal Quantity { get; set; }

  /// <summary>
  /// Gets or sets the current unit value (optional).
  /// </summary>
  public decimal? CurrentUnitValue { get; set; }

  /// <summary>
  /// Gets or sets the currency for the allocation.
  /// </summary>
  [Required(ErrorMessage = "Currency is required")]
  public Currency Currency { get; set; }

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
  /// Gets or sets the application term or investment term (optional).
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
  /// Gets or sets the identifier of the associated account.
  /// </summary>
  [Required(ErrorMessage = "Account is required")]
  public int AccountId { get; set; }
}
