//-----------------------------------------------------------------------
// <copyright file="PlanningActionCandidate.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents one deterministic planning action before it is merged into persistence.
/// </summary>
public sealed class PlanningActionCandidate
{
  /// <summary>Gets or sets the stable generator code.</summary>
  public string SourceCode { get; set; } = string.Empty;

  /// <summary>Gets or sets the operational action type.</summary>
  public PlanningActionType ActionType { get; set; }

  /// <summary>Gets or sets the display priority where lower numbers come first.</summary>
  public int Priority { get; set; }

  /// <summary>Gets or sets the Bootstrap icon CSS class.</summary>
  public string IconCss { get; set; } = "bi bi-lightning-charge";

  /// <summary>Gets or sets the localized title key.</summary>
  public string TitleKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized detail key.</summary>
  public string DetailKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the concise evidence text.</summary>
  public string EvidenceText { get; set; } = string.Empty;

  /// <summary>Gets or sets the route opened by the primary action.</summary>
  public string DestinationHref { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized primary action label key.</summary>
  public string PrimaryActionKey { get; set; } = "Open";

  /// <summary>Gets or sets the optional local due date.</summary>
  public DateOnly? DueDate { get; set; }
}
