//-----------------------------------------------------------------------
// <copyright file="ReviewTemplateItemEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Represents a single checklist item within a <see cref="ReviewTemplateEntity"/>.
  /// </summary>
  public sealed class ReviewTemplateItemEntity : BaseEntity
  {
    /// <summary>Gets or sets the parent template identifier.</summary>
    public int TemplateId { get; set; }

    /// <summary>Navigation property to the parent template.</summary>
    public ReviewTemplateEntity? Template { get; set; }

    /// <summary>Gets or sets the item text / prompt.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the display sort order within the template.</summary>
    public int SortOrder { get; set; }
  }
}
