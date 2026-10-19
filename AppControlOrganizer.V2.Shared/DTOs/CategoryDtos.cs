//-----------------------------------------------------------------------
// <copyright file="CategoryDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

/// <summary>
/// DTO for creating or updating a category.
/// </summary>
public sealed class CategoryDto
{
  /// <summary>
  /// Gets or sets the category name.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the CSS class for the category icon.
  /// </summary>
  public string IconCss { get; set; } = "bi bi-tag";

  /// <summary>
  /// Gets or sets a value indicating whether this category represents income.
  /// </summary>
  public bool IsIncome { get; set; }
}

/// <summary>
/// DTO representing category data for display.
/// </summary>
public sealed class CategoryDisplayDto
{
  /// <summary>
  /// Gets or sets the category identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Gets or sets the category name.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets an optional category code.
  /// </summary>
  public string? Code { get; set; }

  /// <summary>
  /// Gets or sets the CSS class for the category icon.
  /// </summary>
  public string IconCss { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether this category represents income.
  /// </summary>
  public bool IsIncome { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether this category is predefined by the system.
  /// </summary>
  public bool IsPredefined { get; set; }

  /// <summary>
  /// Gets or sets the number of times this category has been used.
  /// </summary>
  public int UsageCount { get; set; }
}
