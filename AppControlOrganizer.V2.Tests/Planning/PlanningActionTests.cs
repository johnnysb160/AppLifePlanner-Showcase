//-----------------------------------------------------------------------
// <copyright file="PlanningActionTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Planning;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Tests deterministic planning action generation.
/// </summary>
public sealed class PlanningActionTests
{
  /// <summary>
  /// Rule candidates are deterministic and ordered by priority.
  /// </summary>
  [Fact]
  public void RulesBuildFinancialPlanningAndDataQualityActions()
  {
    PlanningActionRuleInput input = new PlanningActionRuleInput
    {
      ReferenceDate = TestClock.Today,
      Facts = new FinancialFacts
      {
        UnpricedPositionCount = 2,
        Signals =
        {
          new FinancialSignal { Code = "UNPRICED_POSITIONS", RequiresAttention = true },
        },
      },
      HasWeeklyFocuses = false,
      LastReviewDate = TestClock.Today.AddDays(-9),
      UnclassifiedAllocationCount = 1,
      UnclassifiedTransactionCount = 3,
    };

    IReadOnlyList<PlanningActionCandidate> candidates = PlanningActionRules.BuildCandidates(input);

    Assert.Equal(
      new[]
      {
        "FIN_UNPRICED_POSITIONS",
        "PLAN_WEEKLY_FOCUS_MISSING",
        "PLAN_REVIEW_DUE",
        "DATA_ALLOCATION_PURPOSE",
        "DATA_TRANSACTION_IMPACT",
      },
      candidates.Select(item => item.SourceCode));
  }

  /// <summary>
  /// Manual generation cannot write into the anonymous demo tenant.
  /// </summary>
  [Fact]
  public async Task ManualDemoGenerationIsReadOnly()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    PlanningActionService service = new PlanningActionService(
      factory,
      new FakeFinancialFactsService(),
      new FakePlanningExperienceService(),
      new FakeAccessAnalyticsRecorder(),
      NullLogger<PlanningActionService>.Instance);

    await Assert.ThrowsAsync<DemoReadOnlyException>(() =>
      service.GenerateForUserAsync(TestHttpContext.DemoUserId, TestClock.Today, manual: true));
  }

  /// <summary>
  /// Completed generated actions are not recreated for the same source/date cycle.
  /// </summary>
  [Fact]
  public async Task CompletedManagedActionIsNotReopenedForSameReferenceDate()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    await using (LifePlannerContext context = await factory.CreateDbContextAsync())
    {
      context.PlanningActions.Add(new PlanningActionEntity
      {
        OwnerUserId = "user-a",
        ReferenceDate = TestClock.Today,
        SourceCode = "PLAN_WEEKLY_FOCUS_MISSING",
        Status = PlanningActionStatus.Done,
        Active = false,
      });
      await context.SaveChangesAsync();
    }

    PlanningActionService service = new PlanningActionService(
      factory,
      new FakeFinancialFactsService(),
      new FakePlanningExperienceService
      {
        Reviews = new List<PlanningReviewEntity>
        {
          new PlanningReviewEntity { OwnerUserId = "user-a", ReviewDate = TestClock.Today },
        },
      },
      new FakeAccessAnalyticsRecorder(),
      NullLogger<PlanningActionService>.Instance);

    List<PlanningActionEntity> actions = await service.GenerateForUserAsync("user-a", TestClock.Today, manual: false);

    Assert.DoesNotContain(actions, item => item.SourceCode == "PLAN_WEEKLY_FOCUS_MISSING");
  }
}
