//-----------------------------------------------------------------------
// <copyright file="CategoryEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Entity representing a transaction category. Supports both predefined (seeded) and user-defined categories.
/// Predefined categories have a stable Code used by imports; user-defined categories have null Code.
/// </summary>
public class CategoryEntity : BaseEntity
{
  /// <summary>
  /// Display name of the category (editable by user).
  /// </summary>
  [Required]
  [StringLength(100)]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Stable code for predefined categories (e.g., "SALARY", "FOOD"). Used by import mappers. User-defined categories
  /// have null Code. This value is immutable for seeded categories.
  /// </summary>
  [StringLength(50)]
  public string? Code { get; set; }

  /// <summary>
  /// Bootstrap icon CSS class (e.g., "bi bi-wallet2").
  /// </summary>
  [StringLength(50)]
  public string IconCss { get; set; } = "bi bi-tag";

  /// <summary>
  /// True if this category is typically used for income transactions.
  /// </summary>
  public bool IsIncome { get; set; }

  /// <summary>
  /// True if this is a predefined (seeded) category. Predefined categories cannot be deleted.
  /// </summary>
  public bool IsPredefined { get; set; }
}
