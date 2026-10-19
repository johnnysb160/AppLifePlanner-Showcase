//-----------------------------------------------------------------------
// <copyright file="IPlanningActionService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Provides deterministic, no-paid-AI operational planning actions.
/// </summary>
public interface IPlanningActionService
{
  /// <summary>Returns open actions for the user's Today surface.</summary>
  Task<List<PlanningActionEntity>> GetOpenActionsAsync(
    string userId,
    int take = 6,
    CancellationToken cancellationToken = default);

  /// <summary>Generates current actions for one user.</summary>
  Task<List<PlanningActionEntity>> GenerateForUserAsync(
    string userId,
    DateOnly referenceDate,
    bool manual,
    CancellationToken cancellationToken = default);

  /// <summary>Generates current actions for every known user with planning or finance data.</summary>
  Task GenerateForAllUsersAsync(CancellationToken cancellationToken = default);

  /// <summary>Marks an open action as completed.</summary>
  Task CompleteActionAsync(
    int actionId,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Dismisses an open action for the current cycle.</summary>
  Task DismissActionAsync(
    int actionId,
    string userId,
    CancellationToken cancellationToken = default);
}
