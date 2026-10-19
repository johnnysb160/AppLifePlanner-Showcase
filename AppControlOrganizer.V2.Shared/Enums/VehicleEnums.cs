//-----------------------------------------------------------------------
// <copyright file="VehicleEnums.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines the personal vehicle categories supported by the vehicle routine page.
/// </summary>
public enum VehicleType
{
  /// <summary>A car or similar passenger vehicle.</summary>
  Car = 0,

  /// <summary>A motorcycle or similar two-wheel vehicle.</summary>
  Motorcycle = 1,
}

/// <summary>
/// Defines the kind of record stored in a vehicle history entry.
/// </summary>
public enum VehicleRecordType
{
  /// <summary>A general maintenance or service record.</summary>
  Maintenance = 0,

  /// <summary>An oil-change record.</summary>
  OilChange = 1,

  /// <summary>A fuel fill-up record.</summary>
  Fuel = 2,

  /// <summary>A non-fuel, non-maintenance expense record.</summary>
  Expense = 3,

  /// <summary>An odometer reading without an associated cost.</summary>
  Odometer = 4,
}

/// <summary>
/// Defines the lifecycle state of a vehicle reminder.
/// </summary>
public enum VehicleReminderStatus
{
  /// <summary>The reminder is still pending.</summary>
  Open = 0,

  /// <summary>The reminder has been completed.</summary>
  Done = 1,

  /// <summary>The reminder has been intentionally dismissed.</summary>
  Dismissed = 2,
}
