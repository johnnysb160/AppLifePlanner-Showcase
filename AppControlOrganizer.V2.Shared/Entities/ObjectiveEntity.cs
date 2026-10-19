//-----------------------------------------------------------------------
// <copyright file="ObjectiveEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents an OKR Objective — a qualitative goal that belongs to a strategic pillar.
  /// </summary>
  public sealed class ObjectiveEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent pillar identifier.</summary>
    public int PillarId { get; set; }

    /// <summary>Navigation property to the parent pillar.</summary>
    public PillarEntity? Pillar { get; set; }

    /// <summary>Gets or sets the optional annual plan identifier.</summary>
    public int? AnnualPlanId { get; set; }

    /// <summary>Gets or sets the objective title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the target completion deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Gets or sets the RAG status (manual).</summary>
    public RagStatus Status { get; set; } = RagStatus.Amber;

    /// <summary>Gets or sets the display sort order within the pillar.</summary>
    public int SortOrder { get; set; }

    /// <summary>Navigation property — key results that belong to this objective.</summary>
    public List<KeyResultEntity> KeyResults { get; set; } = new();
  }
}
