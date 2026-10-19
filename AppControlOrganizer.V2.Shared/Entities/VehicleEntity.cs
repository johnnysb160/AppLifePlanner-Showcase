//-----------------------------------------------------------------------
// <copyright file="VehicleEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents one personal vehicle managed from the routine vehicle page.
/// </summary>
public sealed class VehicleEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the vehicle type.</summary>
  public VehicleType VehicleType { get; set; }

  /// <summary>Gets or sets the display name or nickname.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional brand.</summary>
  public string? Brand { get; set; }

  /// <summary>Gets or sets the optional model.</summary>
  public string? Model { get; set; }

  /// <summary>Gets or sets the optional model year.</summary>
  public int? Year { get; set; }

  /// <summary>Gets or sets the optional plate identifier.</summary>
  public string? Plate { get; set; }

  /// <summary>Gets or sets the current odometer in kilometers.</summary>
  public decimal CurrentOdometer { get; set; }

  /// <summary>Gets or sets the odometer unit label.</summary>
  public string OdometerUnit { get; set; } = "Km";

  /// <summary>Gets or sets a Bootstrap icon class.</summary>
  public string IconCss { get; set; } = "bi bi-car-front";

  /// <summary>Gets or sets the hex colour used for the vehicle accent.</summary>
  public string ColorHex { get; set; } = "#2563eb";

  /// <summary>Gets or sets an optional vehicle image as a data URL.</summary>
  public string? ImageDataUrl { get; set; }

  /// <summary>Gets or sets optional notes.</summary>
  public string? Notes { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
  public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

  /// <summary>Gets or sets the vehicle history entries.</summary>
  public List<VehicleRecordEntity> Records { get; set; } = new();

  /// <summary>Gets or sets the vehicle reminders.</summary>
  public List<VehicleReminderEntity> Reminders { get; set; } = new();
}
