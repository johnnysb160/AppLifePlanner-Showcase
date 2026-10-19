//-----------------------------------------------------------------------
// <copyright file="VehicleRecordEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents one unified vehicle history record for odometer, maintenance, oil, fuel, or expense entries.
/// </summary>
public sealed class VehicleRecordEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the parent vehicle identifier.</summary>
  public int VehicleId { get; set; }

  /// <summary>Gets or sets the parent vehicle.</summary>
  public VehicleEntity? Vehicle { get; set; }

  /// <summary>Gets or sets the record type.</summary>
  public VehicleRecordType RecordType { get; set; }

  /// <summary>Gets or sets the local record date.</summary>
  public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

  /// <summary>Gets or sets the odometer value in kilometers.</summary>
  public decimal? Odometer { get; set; }

  /// <summary>Gets or sets the record title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional category.</summary>
  public string? Category { get; set; }

  /// <summary>Gets or sets the optional total amount.</summary>
  public decimal? Amount { get; set; }

  /// <summary>Gets or sets the optional quantity such as liters.</summary>
  public decimal? Quantity { get; set; }

  /// <summary>Gets or sets a value indicating whether this record included an oil change.</summary>
  public bool OilChanged { get; set; }

  /// <summary>Gets or sets service items selected for this record, separated by semicolons.</summary>
  public string? ServiceItems { get; set; }

  /// <summary>Gets or sets the optional place, shop, or provider.</summary>
  public string? Location { get; set; }

  /// <summary>Gets or sets optional notes.</summary>
  public string? Notes { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
  public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
