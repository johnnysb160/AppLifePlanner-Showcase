//-----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Provides a base class for entities with standard identifier and metadata properties.
/// </summary>
/// <remarks>BaseEntity defines common properties such as a primary key, globally unique identifier, creation
/// timestamp, and status flags for use in derived entity types. This class is intended to be inherited by domain
/// entities to ensure consistency in entity identification and lifecycle tracking.</remarks>
public abstract class BaseEntity
{
  /// <summary>
  /// Gets or sets the primary identifier for the entity.
  /// </summary>
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public int Id { get; set; }

  /// <summary>
  /// Gets or sets a globally unique reference identifier for the entity.
  /// </summary>
  public Guid RefId { get; set; } = Guid.NewGuid();

  /// <summary>
  /// Gets or sets an optional external identifier (e.g., id from external systems).
  /// </summary>
  public int? ExternalId { get; set; }

  /// <summary>
  /// Gets or sets the UTC creation date/time for the entity.
  /// </summary>
  public DateTime CreationDate { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Gets or sets a value indicating whether the entity is active.
  /// </summary>
  public bool Active { get; set; } = true;

  /// <summary>
  /// Gets or sets a value indicating whether the entity is soft-deleted.
  /// </summary>
  public bool Deleted { get; set; } = false;
}
