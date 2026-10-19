//-----------------------------------------------------------------------
// <copyright file="PlanningActionRunEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Represents one execution of the deterministic planning action generator.
/// </summary>
public sealed class PlanningActionRunEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the local reference date used by the run.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets a value indicating whether the run was explicitly requested by the user.</summary>
  public bool Manual { get; set; }

  /// <summary>Gets or sets when the run started.</summary>
  public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

  /// <summary>Gets or sets when the run completed.</summary>
  public DateTime? CompletedAtUtc { get; set; }

  /// <summary>Gets or sets a value indicating whether the run completed successfully.</summary>
  public bool Succeeded { get; set; }

  /// <summary>Gets or sets the number of open actions produced after the run.</summary>
  public int OpenActionCount { get; set; }

  /// <summary>Gets or sets a compact error message when the run fails.</summary>
  public string ErrorMessage { get; set; } = string.Empty;
}
