//-----------------------------------------------------------------------
// <copyright file="RiskEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents a pre-mortem risk with probability, impact, early-warning signals and contingency plans.
  /// </summary>
  public sealed class RiskEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the risk title / short description.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional longer description of the risk.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the estimated probability of the risk materialising.</summary>
    public RiskProbability Probability { get; set; } = RiskProbability.Low;

    /// <summary>Gets or sets the estimated impact if the risk materialises.</summary>
    public RiskImpact Impact { get; set; } = RiskImpact.Medium;

    /// <summary>Gets or sets the early-warning signal / leading indicator text.</summary>
    public string? EarlyWarning { get; set; }

    /// <summary>Gets or sets the contingency / mitigation plan.</summary>
    public string? Contingency { get; set; }

    /// <summary>Gets or sets the fallback plan B if the contingency fails.</summary>
    public string? PlanB { get; set; }

    /// <summary>Gets or sets an optional link to a pillar for context.</summary>
    public int? PillarId { get; set; }

    /// <summary>Navigation property to the associated pillar.</summary>
    public PillarEntity? Pillar { get; set; }

    /// <summary>Gets or sets the RAG status (manual — tracks whether the risk is active or resolved).</summary>
    public RagStatus Status { get; set; } = RagStatus.Amber;

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
  }
}
