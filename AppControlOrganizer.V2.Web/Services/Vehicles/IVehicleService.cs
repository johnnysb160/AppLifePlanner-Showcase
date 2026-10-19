//-----------------------------------------------------------------------
// <copyright file="IVehicleService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Vehicles;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Coordinates personal vehicle records and reminders for the routine vehicle page.
/// </summary>
public interface IVehicleService
{
  /// <summary>
  /// Returns the full vehicle page state for the current user.
  /// </summary>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="selectedVehicleId">The optional selected vehicle identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The page state.</returns>
  Task<VehiclePageState> GetPageStateAsync(
    string userId,
    int? selectedVehicleId = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Saves a vehicle for the current user.
  /// </summary>
  /// <param name="vehicle">The vehicle to save.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The saved vehicle.</returns>
  Task<VehicleEntity> SaveVehicleAsync(
    VehicleEntity vehicle,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Soft-deletes a vehicle and preserves its history.
  /// </summary>
  /// <param name="vehicleId">The vehicle identifier.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task DeleteVehicleAsync(int vehicleId, string userId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Saves a unified vehicle history record.
  /// </summary>
  /// <param name="record">The record to save.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The saved record.</returns>
  Task<VehicleRecordEntity> SaveRecordAsync(
    VehicleRecordEntity record,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Soft-deletes a vehicle record.
  /// </summary>
  /// <param name="recordId">The record identifier.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task DeleteRecordAsync(int recordId, string userId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Saves a vehicle reminder.
  /// </summary>
  /// <param name="reminder">The reminder to save.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The saved reminder.</returns>
  Task<VehicleReminderEntity> SaveReminderAsync(
    VehicleReminderEntity reminder,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Updates the lifecycle state of a reminder.
  /// </summary>
  /// <param name="reminderId">The reminder identifier.</param>
  /// <param name="status">The target status.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="completedByRecordId">The optional completing record identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task UpdateReminderStatusAsync(
    int reminderId,
    VehicleReminderStatus status,
    string userId,
    int? completedByRecordId = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Soft-deletes a vehicle reminder.
  /// </summary>
  /// <param name="reminderId">The reminder identifier.</param>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  Task DeleteReminderAsync(int reminderId, string userId, CancellationToken cancellationToken = default);
}
