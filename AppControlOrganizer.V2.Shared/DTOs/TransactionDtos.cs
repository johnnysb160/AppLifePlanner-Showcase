//-----------------------------------------------------------------------
// <copyright file="TransactionDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

using System.ComponentModel.DataAnnotations;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents the data required to create a new Transaction, including date, category, value, description, and income status.
/// </summary>
public class CreateTransactionDto
{
  /// <summary>
  /// Gets or sets the Transaction date.
  /// </summary>
  [Required(ErrorMessage = "Date is required")]
  public DateTime Date { get; set; } = DateTime.Today;

  /// <summary>
  /// Gets or sets the Transaction category.
  /// </summary>
  [Required(ErrorMessage = "Category is required")]
  public Category Category { get; set; }

  /// <summary>
  /// Gets or sets the monetary value associated with the Transaction.
  /// </summary>
  [Required(ErrorMessage = "Value is required")]
  [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than zero")]
  public decimal Value { get; set; }

  /// <summary>
  /// Gets or sets the Transaction description.
  /// </summary>
  [Required(ErrorMessage = "Description is required")]
  [StringLength(500, ErrorMessage = "Description must be at most 500 characters")]
  public string Description { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether this Transaction is income (true) or expense (false).
  /// </summary>
  public bool Income { get; set; }

  /// <summary>
  /// Gets or sets the optional account ID to link this transaction for balance sync.
  /// </summary>
  public int? AccountId { get; set; }

  /// <summary>Gets or sets the transaction's optional impact on planning calculations.</summary>
  public TransactionPlanningImpact PlanningImpact { get; set; }
}

/// <summary>
/// Represents the data required to update an existing Transaction.
/// </summary>
/// <remarks>This data transfer object is typically used in update operations to modify Transaction details such
/// as date, category, value, description, and income status. All required fields must be provided to ensure successful
/// validation.</remarks>
public class UpdateTransactionDto
{
  /// <summary>
  /// Gets or sets the Transaction date.
  /// </summary>
  [Required(ErrorMessage = "Date is required")]
  public DateTime Date { get; set; }

  /// <summary>
  /// Gets or sets the Transaction category.
  /// </summary>
  [Required(ErrorMessage = "Category is required")]
  public Category Category { get; set; }

  /// <summary>
  /// Gets or sets the monetary value associated with the Transaction.
  /// </summary>
  [Required(ErrorMessage = "Value is required")]
  [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than zero")]
  public decimal Value { get; set; }

  /// <summary>
  /// Gets or sets the Transaction description.
  /// </summary>
  [Required(ErrorMessage = "Description is required")]
  [StringLength(500, ErrorMessage = "Description must be at most 500 characters")]
  public string Description { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether this Transaction is income (true) or expense (false).
  /// </summary>
  public bool Income { get; set; }

  /// <summary>
  /// Gets or sets the optional account ID to link this transaction for balance sync.
  /// </summary>
  public int? AccountId { get; set; }

  /// <summary>Gets or sets the transaction's optional impact on planning calculations.</summary>
  public TransactionPlanningImpact PlanningImpact { get; set; }
}
