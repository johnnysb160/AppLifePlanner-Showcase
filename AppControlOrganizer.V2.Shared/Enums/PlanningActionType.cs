//-----------------------------------------------------------------------
// <copyright file="PlanningActionType.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines the operational intent of a generated planning action.
/// </summary>
public enum PlanningActionType
{
  /// <summary>The user should apply or allocate money.</summary>
  Apply = 0,

  /// <summary>The user should correct incomplete or inconsistent data.</summary>
  Correct = 1,

  /// <summary>The user should review an assumption, result, or decision area.</summary>
  Review = 2,

  /// <summary>The user should execute a practical planning step.</summary>
  Execute = 3,

  /// <summary>The user should register missing information.</summary>
  Register = 4,

  /// <summary>The user should make an explicit decision.</summary>
  Decide = 5,
}
