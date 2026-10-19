//-----------------------------------------------------------------------
// <copyright file="FinancialDetailsPopoverRow.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

/// <summary>
/// Represents one line in a compact financial details popover.
/// </summary>
public sealed class FinancialDetailsPopoverRow
{
  /// <summary>Gets or sets the Bootstrap icon class.</summary>
  public string IconClass { get; set; } = "bi-dot";

  /// <summary>Gets or sets the row title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the row detail.</summary>
  public string Detail { get; set; } = string.Empty;

  /// <summary>Gets or sets the formatted amount.</summary>
  public string Amount { get; set; } = string.Empty;

  /// <summary>Gets or sets the visual tone.</summary>
  public string Tone { get; set; } = "neutral";
}
