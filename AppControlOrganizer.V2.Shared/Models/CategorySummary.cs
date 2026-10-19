//-----------------------------------------------------------------------
// <copyright file="CategorySummary.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Summary data for expense category breakdown.
/// </summary>
public class CategorySummary
{
  /// <summary>
  /// Gets or sets the transaction category.
  /// </summary>
  public Category Category { get; set; }

  /// <summary>
  /// Gets or sets the category name.
  /// </summary>
  public string CategoryName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the total amount for the category.
  /// </summary>
  public decimal Total { get; set; }

  /// <summary>
  /// Gets or sets the percentage of total.
  /// </summary>
  public decimal Percentage { get; set; }
}

/// <summary>
/// Summary data for account display in dashboard.
/// </summary>
public class AccountSummary
{
  /// <summary>
  /// Gets or sets the account identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Gets or sets the account name.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the account logo URL.
  /// </summary>
  public string? Logo { get; set; }

  /// <summary>
  /// Gets or sets the institution type.
  /// </summary>
  public InstitutionType InstitutionType { get; set; }

  /// <summary>
  /// Gets or sets the account currency.
  /// </summary>
  public Currency Currency { get; set; }

  /// <summary>
  /// Gets or sets the number of allocations.
  /// </summary>
  public int AllocationCount { get; set; }

  /// <summary>
  /// Gets or sets the number of cards.
  /// </summary>
  public int CardCount { get; set; }

  /// <summary>
  /// Gets or sets the account cash shown by the dashboard.
  /// </summary>
  public decimal Balance { get; set; }

  /// <summary>
  /// Gets or sets the allocation current value shown by the dashboard.
  /// </summary>
  public decimal TotalInvested { get; set; }
}
