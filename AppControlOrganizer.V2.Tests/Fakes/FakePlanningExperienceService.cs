//-----------------------------------------------------------------------
// <copyright file="FakePlanningExperienceService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Planning;

/// <summary>
/// Test double for the simplified planning experience.
/// </summary>
internal sealed class FakePlanningExperienceService : IPlanningExperienceService
{
  /// <summary>Gets or sets the annual plan returned by reads.</summary>
  public AnnualPlanEntity? AnnualPlan { get; set; }

  /// <summary>Gets or sets the active routine returned by reads.</summary>
  public RoutineTemplateEntity? Routine { get; set; }

  /// <summary>Gets or sets weekly focuses returned by reads.</summary>
  public List<WeeklyFocusEntity> Focuses { get; set; } = new List<WeeklyFocusEntity>();

  /// <summary>Gets or sets weekly suggestions returned by reads.</summary>
  public List<WeeklyFocusEntity> Suggestions { get; set; } = new List<WeeklyFocusEntity>();

  /// <summary>Gets or sets compact reviews returned by reads.</summary>
  public List<PlanningReviewEntity> Reviews { get; set; } = new List<PlanningReviewEntity>();

  /// <inheritdoc />
  public Task<AnnualPlanEntity?> GetAnnualPlanAsync(string userId, int year, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.AnnualPlan);
  }

  /// <inheritdoc />
  public Task<AnnualPlanEntity> GetOrCreateAnnualPlanAsync(
    string userId,
    int year,
    FinancialFacts facts,
    CancellationToken cancellationToken = default)
  {
    this.AnnualPlan ??= new AnnualPlanEntity
    {
      OwnerUserId = userId,
      Year = year,
      Theme = year.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
    return Task.FromResult(this.AnnualPlan);
  }

  /// <inheritdoc />
  public Task<RoutineTemplateEntity?> GetActiveRoutineAsync(string userId, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Routine);
  }

  /// <inheritdoc />
  public Task<List<WeeklyFocusEntity>> GetWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Focuses);
  }

  /// <inheritdoc />
  public Task<List<WeeklyFocusEntity>> GetWeeklyFocusSuggestionsAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Suggestions);
  }

  /// <inheritdoc />
  public Task<List<PlanningReviewEntity>> GetRecentReviewsAsync(
    string userId,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Reviews);
  }

  /// <inheritdoc />
  public Task<AnnualPlanEntity> SaveAnnualPlanAsync(
    AnnualPlanEntity plan,
    string userId,
    CancellationToken cancellationToken = default)
  {
    this.AnnualPlan = plan;
    return Task.FromResult(plan);
  }

  /// <inheritdoc />
  public Task<RoutineTemplateEntity> ReplaceRoutineAsync(
    string userId,
    string name,
    IReadOnlyCollection<RoutineBlockEntity> blocks,
    CancellationToken cancellationToken = default)
  {
    this.Routine = new RoutineTemplateEntity
    {
      OwnerUserId = userId,
      Name = name,
      IsActiveTemplate = true,
      Blocks = blocks.ToList(),
    };
    return Task.FromResult(this.Routine);
  }

  /// <inheritdoc />
  public Task<RoutineBlockEntity> SaveRoutineBlockAsync(
    RoutineBlockEntity block,
    string userId,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(block);
  }

  /// <inheritdoc />
  public Task DeleteRoutineBlockAsync(int blockId, string userId, CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task<List<WeeklyFocusEntity>> ReplaceWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    IReadOnlyCollection<WeeklyFocusEntity> focuses,
    CancellationToken cancellationToken = default)
  {
    this.Focuses = focuses.ToList();
    return Task.FromResult(this.Focuses);
  }

  /// <inheritdoc />
  public Task<PlanningReviewEntity> CompleteReviewAsync(
    PlanningReviewEntity review,
    string userId,
    AnnualPlanEntity annualPlan,
    IReadOnlyCollection<WeeklyFocusEntity> nextFocuses,
    CancellationToken cancellationToken = default)
  {
    this.Reviews.Insert(0, review);
    return Task.FromResult(review);
  }

  /// <inheritdoc />
  public Task RefreshFinancialKeyResultsAsync(
    string userId,
    FinancialFacts facts,
    CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }
}
