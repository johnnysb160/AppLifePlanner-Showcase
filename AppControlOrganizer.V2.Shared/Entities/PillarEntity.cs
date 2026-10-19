//-----------------------------------------------------------------------
// <copyright file="PillarEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Represents a strategic pillar (top-level OKR dimension, e.g. Health, Finance, Career, Relationships).
  /// </summary>
  public sealed class PillarEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the pillar name (e.g. "Health", "Finance").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional longer description of the pillar.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the display sort order (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the hex colour used to visually identify this pillar (e.g. "#0d6efd").</summary>
    public string ColorHex { get; set; } = "#6c757d";

    /// <summary>Gets or sets an optional Bootstrap-Icons CSS class for the pillar icon.</summary>
    public string? IconCss { get; set; }

    /// <summary>Navigation property — objectives that belong to this pillar.</summary>
    public List<ObjectiveEntity> Objectives { get; set; } = new();
  }
}
