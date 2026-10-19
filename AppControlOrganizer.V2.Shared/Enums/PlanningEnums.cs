//-----------------------------------------------------------------------
// <copyright file="PlanningEnums.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums
{
  /// <summary>Red-Amber-Green status for OKR Key Results, Quarter Plans, and Risks.</summary>
  public enum RagStatus
  {
    /// <summary>Off-track / critical.</summary>
    Red = 0,

    /// <summary>At risk but recoverable.</summary>
    Amber = 1,

    /// <summary>On track / completed.</summary>
    Green = 2
  }

  /// <summary>Review cadence type.</summary>
  public enum ReviewType
  {
    /// <summary>Weekly review.</summary>
    Weekly = 0,

    /// <summary>Monthly review.</summary>
    Monthly = 1
  }

  /// <summary>Estimated probability of a risk materialising.</summary>
  public enum RiskProbability
  {
    /// <summary>Low probability.</summary>
    Low = 0,

    /// <summary>Medium probability.</summary>
    Medium = 1,

    /// <summary>High probability.</summary>
    High = 2
  }

  /// <summary>Estimated impact if a risk materialises.</summary>
  public enum RiskImpact
  {
    /// <summary>Low impact.</summary>
    Low = 0,

    /// <summary>Medium impact.</summary>
    Medium = 1,

    /// <summary>High impact.</summary>
    High = 2
  }
}
