//-----------------------------------------------------------------------
// <copyright file="FakePlanningActionService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Services.Planning;

/// <summary>
/// Test double for planning action reads.
/// </summary>
internal sealed class FakePlanningActionService : IPlanningActionService
{
  /// <summary>Gets or sets open actions returned by reads.</summary>
  public List<PlanningActionEntity> Actions { get; set; } = new List<PlanningActionEntity>();

  /// <inheritdoc />
  public Task<List<PlanningActionEntity>> GetOpenActionsAsync(
    string userId,
    int take = 6,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Actions.Take(take).ToList());
  }

  /// <inheritdoc />
  public Task<List<PlanningActionEntity>> GenerateForUserAsync(
    string userId,
    DateOnly referenceDate,
    bool manual,
    CancellationToken cancellationToken = default)
  {
    if (userId == AppControlOrganizer.V2.Web.Data.LifePlannerContext.DemoUserId && manual)
    {
      throw new AppControlOrganizer.V2.Web.Data.DemoReadOnlyException();
    }

    return Task.FromResult(this.Actions);
  }

  /// <inheritdoc />
  public Task GenerateForAllUsersAsync(CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task CompleteActionAsync(int actionId, string userId, CancellationToken cancellationToken = default)
  {
    PlanningActionEntity? action = this.Actions.FirstOrDefault(item => item.Id == actionId);
    if (action != null)
    {
      action.Status = PlanningActionStatus.Done;
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task DismissActionAsync(int actionId, string userId, CancellationToken cancellationToken = default)
  {
    PlanningActionEntity? action = this.Actions.FirstOrDefault(item => item.Id == actionId);
    if (action != null)
    {
      action.Status = PlanningActionStatus.Dismissed;
    }

    return Task.CompletedTask;
  }
}
