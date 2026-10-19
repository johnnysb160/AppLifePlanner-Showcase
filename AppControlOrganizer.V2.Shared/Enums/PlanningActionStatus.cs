//-----------------------------------------------------------------------
// <copyright file="PlanningActionStatus.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines the lifecycle state of a generated planning action.
/// </summary>
public enum PlanningActionStatus
{
  /// <summary>The action is visible and pending.</summary>
  Open = 0,

  /// <summary>The user marked the action as completed.</summary>
  Done = 1,

  /// <summary>The user intentionally dismissed the action.</summary>
  Dismissed = 2,

  /// <summary>The system no longer considers the action current.</summary>
  Expired = 3,
}
