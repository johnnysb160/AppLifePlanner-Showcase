//-----------------------------------------------------------------------
// <copyright file="FinancialFactsTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Financial;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Financial;
using Xunit;

/// <summary>
/// Tests canonical financial facts and formulas.
/// </summary>
public sealed class FinancialFactsTests
{
  /// <summary>
  /// Net worth includes cash and included allocations but excludes explicitly excluded positions.
  /// </summary>
  [Fact]
  public void PositionBreakdownPreservesCanonicalNetWorthRules()
  {
    AccountEntity account = new AccountEntity
    {
      Id = 1,
      Name = "Main",
      Balance = 500m,
      BalanceMode = AccountBalanceMode.PositionBased,
    };
    List<AllocationEntity> allocations = new List<AllocationEntity>
    {
      new AllocationEntity
      {
        Id = 10,
        AccountId = account.Id,
        Name = "Reserva",
        AllocationType = AllocationType.Stopped,
        BuyUnitValue = 100m,
        CurrentUnitValue = 100m,
        Quantity = 3m,
        PlanningPurpose = FinancialPurpose.EmergencyReserve,
      },
      new AllocationEntity
      {
        Id = 11,
        AccountId = account.Id,
        Name = "Long term",
        AllocationType = AllocationType.Stocks,
        BuyUnitValue = 10m,
        CurrentUnitValue = 20m,
        Quantity = 10m,
        PlanningPurpose = FinancialPurpose.LongTermInvestment,
      },
      new AllocationEntity
      {
        Id = 12,
        AccountId = account.Id,
        Name = "Excluded",
        AllocationType = AllocationType.Others,
        BuyUnitValue = 1000m,
        CurrentUnitValue = 1000m,
        Quantity = 1m,
        PlanningPurpose = FinancialPurpose.Excluded,
      },
    };

    FinancialPositionBreakdown breakdown = FinancialPositionCalculator.CalculateBreakdown(
      new[] { account },
      allocations,
      Enumerable.Empty<TransactionEntity>(),
      new Dictionary<QuoteKey, MarketQuoteSnapshot>(),
      new AllocationQuotePricingService(),
      FinancialFactsService.ResolvePurpose);

    Assert.Equal(500m, breakdown.NetWorth);
    Assert.Equal(300m, breakdown.EmergencyReserve);
    Assert.Equal(200m, breakdown.LongTermInvestments);
    Assert.Equal(1000m, breakdown.ExcludedAllocationValue);
    Assert.Equal(500m, breakdown.SkippedDuplicateAccountBalance);
  }

  /// <summary>
  /// Internal transfer detection keeps transfer pairs out of operating cash flow.
  /// </summary>
  [Fact]
  public void AutomaticClassificationDetectsCrossAccountTransfers()
  {
    List<TransactionEntity> transactions = new List<TransactionEntity>
    {
      new TransactionEntity { Id = 1, AccountId = 1, Date = TestClock.LocalNow, Value = 250m, Income = false },
      new TransactionEntity { Id = 2, AccountId = 2, Date = TestClock.LocalNow.AddDays(1), Value = 250m, Income = true },
      new TransactionEntity { Id = 3, AccountId = 1, Date = TestClock.LocalNow, Value = 50m, Income = false },
    };

    HashSet<int> transfers = FinancialFactsService.DetectInternalTransfers(transactions);
    bool classified = FinancialFactsService.ApplyAutomaticClassification(transactions[0], transfers);

    Assert.Contains(1, transfers);
    Assert.Contains(2, transfers);
    Assert.True(classified);
    Assert.Equal(TransactionPlanningImpact.InternalTransfer, transactions[0].PlanningImpact);
    Assert.Equal(TransactionPlanningImpact.Operating, FinancialFactsService.ResolveImpact(transactions[2], transfers));
  }

  /// <summary>
  /// Annual projection starts from facts and applies remaining contributions plus one-off flows.
  /// </summary>
  [Fact]
  public void AnnualProjectionUsesManualContributionWhenProvided()
  {
    FinancialFacts facts = new FinancialFacts
    {
      NetWorth = 1000m,
      AllocationAppliedValue = 800m,
      AllocationCurrentValue = 1000m,
      YearToDateContributions = 300m,
      YearToDateContributionMonths = 3,
    };
    AnnualPlanEntity plan = new AnnualPlanEntity
    {
      PlannedMonthlyContribution = 100m,
      PlannedOneOffNetFlows = 50m,
      TargetNetWorth = 2200m,
      EmergencyReserveMonths = 6,
      MonthlySpendingCeiling = 200m,
    };

    FinancialPlanProjectionCalculator.Apply(facts, plan, new DateOnly(2026, 9, 15));

    Assert.Equal(100m, facts.EffectivePlannedContribution);
    Assert.Equal(300m, facts.ProjectedRemainingContributions);
    Assert.Equal(50m, facts.ProjectedOneOffFlows);
    Assert.Equal(1200m, facts.EmergencyReserveTarget);
    Assert.True(facts.RequiredMonthlyContribution > facts.EffectivePlannedContribution);
  }
}
