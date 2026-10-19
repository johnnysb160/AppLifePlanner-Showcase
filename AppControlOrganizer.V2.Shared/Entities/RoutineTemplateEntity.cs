//-----------------------------------------------------------------------
// <copyright file="RoutineTemplateEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents a reusable weekly routine template.
/// </summary>
public sealed class RoutineTemplateEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the template name.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Gets or sets a value indicating whether this is the active template.</summary>
  public bool IsActiveTemplate { get; set; }

  /// <summary>Gets or sets the version of the one-time reference data already applied to this routine.</summary>
  public int InitialDataVersion { get; set; }

  /// <summary>Gets or sets the routine blocks.</summary>
  public ICollection<RoutineBlockEntity> Blocks { get; set; } = new List<RoutineBlockEntity>();
}
