//-----------------------------------------------------------------------
// <copyright file="VehicleReminderEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents a manual vehicle reminder due by date, odometer, or both.
/// </summary>
public sealed class VehicleReminderEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the parent vehicle identifier.</summary>
  public int VehicleId { get; set; }

  /// <summary>Gets or sets the parent vehicle.</summary>
  public VehicleEntity? Vehicle { get; set; }

  /// <summary>Gets or sets the optional record that completed this reminder.</summary>
  public int? CompletedByRecordId { get; set; }

  /// <summary>Gets or sets the optional record that completed this reminder.</summary>
  public VehicleRecordEntity? CompletedByRecord { get; set; }

  /// <summary>Gets or sets the reminder title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional category.</summary>
  public string? Category { get; set; }

  /// <summary>Gets or sets the optional local due date.</summary>
  public DateOnly? DueDate { get; set; }

  /// <summary>Gets or sets the optional due odometer in kilometers.</summary>
  public decimal? DueOdometer { get; set; }

  /// <summary>Gets or sets the reminder status.</summary>
  public VehicleReminderStatus Status { get; set; }

  /// <summary>Gets or sets optional notes.</summary>
  public string? Notes { get; set; }

  /// <summary>Gets or sets the UTC timestamp when the reminder was resolved.</summary>
  public DateTime? ResolvedAtUtc { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
  public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
