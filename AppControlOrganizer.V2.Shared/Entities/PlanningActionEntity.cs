//-----------------------------------------------------------------------
// <copyright file="PlanningActionEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents one concrete action generated from the user's current planning and financial state.
/// </summary>
public sealed class PlanningActionEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the local date this action was last generated for.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets a stable generator code used for idempotency.</summary>
  public string SourceCode { get; set; } = string.Empty;

  /// <summary>Gets or sets the action type.</summary>
  public PlanningActionType ActionType { get; set; }

  /// <summary>Gets or sets the action status.</summary>
  public PlanningActionStatus Status { get; set; }

  /// <summary>Gets or sets the priority where lower numbers appear first.</summary>
  public int Priority { get; set; }

  /// <summary>Gets or sets the icon CSS class displayed in compact cards.</summary>
  public string IconCss { get; set; } = "bi bi-lightning-charge";

  /// <summary>Gets or sets the localized title key.</summary>
  public string TitleKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized short detail key.</summary>
  public string DetailKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional quantified evidence for the action.</summary>
  public string EvidenceText { get; set; } = string.Empty;

  /// <summary>Gets or sets the destination route for the primary action.</summary>
  public string DestinationHref { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized primary action label key.</summary>
  public string PrimaryActionKey { get; set; } = "Open";

  /// <summary>Gets or sets the optional local due date.</summary>
  public DateOnly? DueDate { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the most recent generator refresh.</summary>
  public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

  /// <summary>Gets or sets the UTC timestamp when the action status was resolved.</summary>
  public DateTime? ResolvedAtUtc { get; set; }
}
