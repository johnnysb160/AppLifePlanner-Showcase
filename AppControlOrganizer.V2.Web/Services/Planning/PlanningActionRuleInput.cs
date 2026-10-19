//-----------------------------------------------------------------------
// <copyright file="PlanningActionRuleInput.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Provides deterministic planning action rules with the facts they need.
/// </summary>
public sealed class PlanningActionRuleInput
{
  /// <summary>Gets or sets the local reference date.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets the canonical financial facts.</summary>
  public FinancialFacts Facts { get; set; } = new FinancialFacts();

  /// <summary>Gets or sets the annual plan used for target decisions.</summary>
  public AnnualPlanEntity? AnnualPlan { get; set; }

  /// <summary>Gets or sets a value indicating whether this week already has explicit focuses.</summary>
  public bool HasWeeklyFocuses { get; set; }

  /// <summary>Gets or sets the most recent compact review date.</summary>
  public DateOnly? LastReviewDate { get; set; }

  /// <summary>Gets or sets how many allocations still use automatic planning purpose.</summary>
  public int UnclassifiedAllocationCount { get; set; }

  /// <summary>Gets or sets how many recent transactions still use automatic planning impact.</summary>
  public int UnclassifiedTransactionCount { get; set; }
}
