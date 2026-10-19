//-----------------------------------------------------------------------
// <copyright file="WidgetTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Widgets;

using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Localization;
using AppControlOrganizer.V2.Web.Services.Widgets;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Tests widget pairing and snapshot behavior.
/// </summary>
public sealed class WidgetTests
{
  /// <summary>
  /// Pairing codes exchange into tokens, validate to the owner, and can be revoked.
  /// </summary>
  [Fact]
  public async Task PairingLifecycleResolvesAndRevokesOwnerToken()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    WidgetPairingService service = new WidgetPairingService(factory);

    WidgetPairingCodeResponseDto code = await service.CreatePairingCodeAsync("user-a");
    string? token = await service.ExchangePairingCodeAsync(code.Code.ToLowerInvariant(), "Desktop");
    string? owner = await service.ValidateTokenAsync(token!);
    int revoked = await service.RevokeUserTokensAsync("user-a");
    string? ownerAfterRevoke = await service.ValidateTokenAsync(token!);

    Assert.False(string.IsNullOrWhiteSpace(token));
    Assert.Equal("user-a", owner);
    Assert.Equal(1, revoked);
    Assert.Null(ownerAfterRevoke);
  }

  /// <summary>
  /// Today widget snapshots reuse planning, action, and financial services instead of direct data engines.
  /// </summary>
  [Fact]
  public async Task TodaySnapshotComposesRoutineFocusActionsAndInsights()
  {
    FakePlanningExperienceService planning = new FakePlanningExperienceService
    {
      Routine = new RoutineTemplateEntity
      {
        OwnerUserId = "user-a",
        IsActiveTemplate = true,
        Blocks =
        {
          new RoutineBlockEntity
          {
            Id = 10,
            DayOfWeek = TestClock.LocalNow.DayOfWeek,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Title = "Deep work",
          },
          new RoutineBlockEntity
          {
            Id = 11,
            DayOfWeek = TestClock.LocalNow.DayOfWeek,
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(13, 0),
            Title = "Planning",
          },
        },
      },
      Suggestions = new List<WeeklyFocusEntity>
      {
        new WeeklyFocusEntity { Text = "Review finances", SortOrder = 1 },
      },
    };
    FakePlanningActionService actions = new FakePlanningActionService
    {
      Actions = new List<PlanningActionEntity>
      {
        new PlanningActionEntity
        {
          Id = 20,
          SourceCode = "PLAN_REVIEW_DUE",
          TitleKey = "Close a light weekly review",
          DetailKey = "Record progress",
          PrimaryActionKey = "Open review",
          DestinationHref = "plan",
          Status = PlanningActionStatus.Open,
        },
      },
    };
    FakeFinancialFactsService facts = new FakeFinancialFactsService
    {
      Facts = new FinancialFacts
      {
        AccountCash = 100m,
        NetWorth = 100m,
        Signals =
        {
          new FinancialSignal { Code = "IDLE_CASH", TitleKey = "Idle cash", RequiresAttention = true },
        },
      },
    };
    TodayWidgetSnapshotService service = new TodayWidgetSnapshotService(
      planning,
      actions,
      facts,
      new LocService(new FakeStringLocalizer<SharedResource>()));

    TodayWidgetSnapshotDto snapshot = await service.BuildAsync(
      "user-a",
      TestClock.LocalNow,
      TimeSpan.FromHours(-3),
      "en");

    Assert.Equal("Deep work", snapshot.Current?.Title);
    Assert.Equal("Planning", snapshot.Next?.Title);
    Assert.Equal(2, snapshot.Focuses.Count);
    Assert.Contains(snapshot.Focuses, item => item.Text == "Idle cash");
    Assert.Single(snapshot.Actions);
    Assert.Equal(2, snapshot.Metadata.PlannedMomentCount);
    Assert.NotEmpty(snapshot.Insights);
  }

  /// <summary>
  /// Demo snapshot reads must not create AppSettings pairing state.
  /// </summary>
  [Fact]
  public async Task DemoReadOnlyContextStartsWithoutWidgetSideEffects()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Demo());
    await using LifePlannerContext context = await factory.CreateDbContextAsync();

    Assert.Equal(0, await context.AppSettings.CountAsync());
  }
}
