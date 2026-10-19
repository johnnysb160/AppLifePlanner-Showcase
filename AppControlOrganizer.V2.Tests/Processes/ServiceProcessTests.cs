//-----------------------------------------------------------------------
// <copyright file="ServiceProcessTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Processes;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Localization;
using AppControlOrganizer.V2.Web.Services.Planning;
using AppControlOrganizer.V2.Web.Services.Vehicles;
using Xunit;

/// <summary>
/// Tests service-level process behavior used by pages.
/// </summary>
public sealed class ServiceProcessTests
{
  /// <summary>
  /// Planning service helpers enforce routine overlap and weekly boundaries.
  /// </summary>
  [Fact]
  public void PlanningExperienceHelpersCoverRoutineAndMetricProcessRules()
  {
    DateOnly weekStart = PlanningExperienceService.GetWeekStart(new DateOnly(2026, 9, 16));
    FinancialMetricSource source = PlanningExperienceService.InferMetricSource("Increase net worth");

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
      PlanningExperienceService.ValidateRoutineBlocks(new[]
      {
        new RoutineBlockEntity
        {
          DayOfWeek = DayOfWeek.Monday,
          StartTime = new TimeOnly(9, 0),
          EndTime = new TimeOnly(10, 0),
          Title = "A",
        },
        new RoutineBlockEntity
        {
          DayOfWeek = DayOfWeek.Monday,
          StartTime = new TimeOnly(9, 30),
          EndTime = new TimeOnly(10, 30),
          Title = "B",
        },
      }));

    Assert.Equal(new DateOnly(2026, 9, 13), weekStart);
    Assert.Equal(FinancialMetricSource.NetWorth, source);
    Assert.Contains("overlap", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Planning experience persists only three non-empty weekly focuses.
  /// </summary>
  [Fact]
  public async Task PlanningExperienceReplacesOnlyThreeWeeklyFocuses()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    PlanningExperienceService service = new PlanningExperienceService(factory, new FakeFinancialFactsService());

    List<WeeklyFocusEntity> saved = await service.ReplaceWeeklyFocusesAsync(
      "user-a",
      TestClock.Today,
      new[]
      {
        new WeeklyFocusEntity { Text = "One" },
        new WeeklyFocusEntity { Text = "Two" },
        new WeeklyFocusEntity { Text = string.Empty },
        new WeeklyFocusEntity { Text = "Three" },
        new WeeklyFocusEntity { Text = "Four" },
      });

    Assert.Equal(new[] { "One", "Two", "Three" }, saved.Select(item => item.Text));
  }

  /// <summary>
  /// Vehicle process saves records, updates odometer, and creates the next oil reminder.
  /// </summary>
  [Fact]
  public async Task VehicleServiceCreatesOilReminderFromOilChangeRecord()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    VehicleService service = new VehicleService(factory);
    VehicleEntity vehicle = await service.SaveVehicleAsync(
      new VehicleEntity
      {
        Name = "Car",
        VehicleType = VehicleType.Car,
        CurrentOdometer = 1000m,
        Active = true,
      },
      "user-a");

    await service.SaveRecordAsync(
      new VehicleRecordEntity
      {
        VehicleId = vehicle.Id,
        RecordType = VehicleRecordType.OilChange,
        RecordDate = TestClock.Today,
        Odometer = 1200m,
        Title = "Oil change",
        Amount = 150m,
      },
      "user-a");

    VehiclePageState state = await service.GetPageStateAsync("user-a", vehicle.Id);

    Assert.Equal(1200m, state.Vehicles.Single().CurrentOdometer);
    Assert.Single(state.Reminders);
    Assert.Equal(VehicleReminderStatus.Open, state.Reminders.Single().Reminder.Status);
  }

  /// <summary>
  /// Localization normalizes unsupported page language requests to English.
  /// </summary>
  [Fact]
  public void LocalizationFallsBackToEnglishForUnsupportedLanguage()
  {
    LocService service = new LocService(new FakeStringLocalizer<SharedResource>());
    service.Set("es");

    Assert.Equal("en", service.Language);
    Assert.Equal("Today", service["Today"]);
  }
}
