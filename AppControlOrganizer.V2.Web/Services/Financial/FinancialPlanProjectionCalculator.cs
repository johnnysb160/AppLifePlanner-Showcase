//-----------------------------------------------------------------------
// <copyright file="FinancialPlanProjectionCalculator.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Applies annual-planning assumptions to canonical financial facts.
/// </summary>
public static class FinancialPlanProjectionCalculator
{
  /// <summary>
  /// Applies year-end projection, target-gap, reserve-target, and idle-cash formulas.
  /// </summary>
  /// <param name="facts">The canonical facts to update.</param>
  /// <param name="plan">Optional annual plan assumptions.</param>
  /// <param name="referenceDate">The local reference date.</param>
  public static void Apply(
    FinancialFacts facts,
    AnnualPlanEntity? plan,
    DateOnly referenceDate)
  {
    decimal annualExpenseMonthlyShare = (plan?.PlannedAnnualExpenses ?? 0m) / 12m;
    decimal historicalCapacityContribution = Math.Max(
      0m,
      facts.SuggestedMonthlyIncome - facts.SuggestedMonthlyExpense - annualExpenseMonthlyShare);
    decimal planCapacityContribution = Math.Max(
      0m,
      (plan?.PlannedMonthlyIncome ?? 0m) - (plan?.MonthlySpendingCeiling ?? 0m) - annualExpenseMonthlyShare);
    decimal capacityContribution = HasHistoricalOperatingBaseline(facts)
      ? historicalCapacityContribution
      : planCapacityContribution;
    decimal historicalContribution = facts.YearToDateContributionMonths > 0
      ? facts.YearToDateContributions / facts.YearToDateContributionMonths
      : 0m;
    decimal? manualContribution = plan?.PlannedMonthlyContribution;
    facts.EffectivePlannedContribution = manualContribution
      ?? Math.Max(historicalContribution, capacityContribution);
    int remainingFullMonths = Math.Max(0, 12 - referenceDate.Month);
    decimal oneOffFlows = plan?.PlannedOneOffNetFlows ?? 0m;
    facts.ProjectedOneOffFlows = oneOffFlows;
    facts.ProjectedRemainingContributions = facts.EffectivePlannedContribution * remainingFullMonths;
    facts.ProjectedRemainingAssetAppreciation = CalculateProjectedRemainingAppreciation(
      facts,
      referenceDate,
      remainingFullMonths);
    facts.PlannedYearEndNetWorth = facts.NetWorth
      + facts.ProjectedRemainingContributions
      + facts.ProjectedRemainingAssetAppreciation
      + oneOffFlows;
    decimal target = plan?.TargetNetWorth ?? 0m;
    facts.RequiredMonthlyContribution = target > 0m && remainingFullMonths > 0
      ? Math.Max(
        0m,
        (target - facts.NetWorth - facts.ProjectedRemainingAssetAppreciation - oneOffFlows) / remainingFullMonths)
      : 0m;
    decimal reserveMonthlyBaseline = plan?.MonthlySpendingCeiling
      ?? facts.SuggestedEssentialExpense;
    facts.EmergencyReserveTarget = reserveMonthlyBaseline * (plan?.EmergencyReserveMonths ?? 6);
    facts.IdleCashPercentage = facts.NetWorth > 0m ? (facts.IdleCash / facts.NetWorth) * 100m : 0m;
  }

  private static bool HasHistoricalOperatingBaseline(FinancialFacts facts)
  {
    return facts.SuggestedMonthlyIncome > 0m
      || facts.SuggestedMonthlyExpense > 0m;
  }

  private static decimal CalculateProjectedRemainingAppreciation(
    FinancialFacts facts,
    DateOnly referenceDate,
    int remainingFullMonths)
  {
    if (remainingFullMonths == 0
      || facts.AllocationAppliedValue <= 0m
      || facts.AllocationCurrentValue <= 0m)
    {
      return 0m;
    }

    decimal appreciationToDate = facts.AllocationCurrentValue - facts.AllocationAppliedValue;
    if (appreciationToDate == 0m)
    {
      return 0m;
    }

    decimal elapsedMonths = Math.Max(1m, referenceDate.Month);
    decimal remainingRatio = remainingFullMonths / elapsedMonths;
    return appreciationToDate * remainingRatio;
  }
}
