//-----------------------------------------------------------------------
// <copyright file="VehiclePageState.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Vehicles;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Contains all read models needed by the vehicle page.
/// </summary>
public sealed class VehiclePageState
{
  /// <summary>Gets or sets the vehicle summaries.</summary>
  public List<VehicleSummary> Vehicles { get; set; } = new();

  /// <summary>Gets or sets the selected vehicle identifier.</summary>
  public int? SelectedVehicleId { get; set; }

  /// <summary>Gets or sets records for the selected vehicle.</summary>
  public List<VehicleRecordEntity> Records { get; set; } = new();

  /// <summary>Gets or sets reminders for the selected vehicle.</summary>
  public List<VehicleReminderView> Reminders { get; set; } = new();

  /// <summary>Gets or sets total current-month costs for the selected vehicle.</summary>
  public decimal SelectedMonthCost { get; set; }

  /// <summary>Gets or sets total current-year costs for the selected vehicle.</summary>
  public decimal SelectedYearCost { get; set; }
}

/// <summary>
/// Summarizes one vehicle for compact vehicle cards.
/// </summary>
public sealed class VehicleSummary
{
  /// <summary>Gets or sets the source vehicle.</summary>
  public VehicleEntity Vehicle { get; set; } = new();

  /// <summary>Gets or sets the calculated current odometer.</summary>
  public decimal CurrentOdometer { get; set; }

  /// <summary>Gets or sets the current month cost.</summary>
  public decimal MonthCost { get; set; }

  /// <summary>Gets or sets the latest service or maintenance record.</summary>
  public VehicleRecordEntity? LastService { get; set; }

  /// <summary>Gets or sets the next pending reminder.</summary>
  public VehicleReminderView? NextReminder { get; set; }
}

/// <summary>
/// Represents a vehicle reminder with calculated due state.
/// </summary>
public sealed class VehicleReminderView
{
  /// <summary>Gets or sets the source reminder.</summary>
  public VehicleReminderEntity Reminder { get; set; } = new();

  /// <summary>Gets or sets the calculated due state key.</summary>
  public VehicleDueState DueState { get; set; }

  /// <summary>Gets or sets the number of days until due.</summary>
  public int? DaysUntilDue { get; set; }

  /// <summary>Gets or sets the odometer distance until due.</summary>
  public decimal? OdometerUntilDue { get; set; }
}

/// <summary>
/// Defines the calculated state of a vehicle reminder.
/// </summary>
public enum VehicleDueState
{
  /// <summary>The reminder has no due trigger.</summary>
  Neutral = 0,

  /// <summary>The reminder is still comfortably in the future.</summary>
  Current = 1,

  /// <summary>The reminder is close to its date or odometer trigger.</summary>
  Soon = 2,

  /// <summary>The reminder is past its date or odometer trigger.</summary>
  Overdue = 3,

  /// <summary>The reminder is no longer open.</summary>
  Resolved = 4,
}
