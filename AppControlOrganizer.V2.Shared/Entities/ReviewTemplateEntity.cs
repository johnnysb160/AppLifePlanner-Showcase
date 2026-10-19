//-----------------------------------------------------------------------
// <copyright file="ReviewTemplateEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  using AppControlOrganizer.V2.Shared.Enums;

  /// <summary>
  /// Represents a reusable review template (Weekly or Monthly) with an ordered checklist.
  /// </summary>
  public sealed class ReviewTemplateEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the template name (e.g. "Revisao Semanal").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the review cadence type (Weekly or Monthly).</summary>
    public ReviewType ReviewType { get; set; } = ReviewType.Weekly;

    /// <summary>Gets or sets a value indicating whether this is the default template for its type.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property — checklist items belonging to this template.</summary>
    public List<ReviewTemplateItemEntity> Items { get; set; } = new();
  }
}
