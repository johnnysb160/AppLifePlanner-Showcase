//-----------------------------------------------------------------------
// <copyright file="IPlanningExperienceService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Coordinates the simplified annual, weekly, routine, and review experience.
/// </summary>
public interface IPlanningExperienceService
{
  /// <summary>Returns the annual plan when one already exists for the user and year.</summary>
  Task<AnnualPlanEntity?> GetAnnualPlanAsync(
    string userId,
    int year,
    CancellationToken cancellationToken = default);

  /// <summary>Returns or creates the annual plan using existing financial goals and facts as suggestions.</summary>
  Task<AnnualPlanEntity> GetOrCreateAnnualPlanAsync(
    string userId,
    int year,
    FinancialFacts facts,
    CancellationToken cancellationToken = default);

  /// <summary>Saves annual planning decisions.</summary>
  Task<AnnualPlanEntity> SaveAnnualPlanAsync(
    AnnualPlanEntity plan,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Returns the active routine with its blocks.</summary>
  Task<RoutineTemplateEntity?> GetActiveRoutineAsync(
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Replaces the active routine blocks after validating the supplied preview.</summary>
  Task<RoutineTemplateEntity> ReplaceRoutineAsync(
    string userId,
    string name,
    IReadOnlyCollection<RoutineBlockEntity> blocks,
    CancellationToken cancellationToken = default);

  /// <summary>Saves one routine block and rejects overlaps.</summary>
  Task<RoutineBlockEntity> SaveRoutineBlockAsync(
    RoutineBlockEntity block,
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Soft-deletes one routine block.</summary>
  Task DeleteRoutineBlockAsync(int blockId, string userId, CancellationToken cancellationToken = default);

  /// <summary>Returns the focuses for the week containing the supplied date.</summary>
  Task<List<WeeklyFocusEntity>> GetWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default);

  /// <summary>Suggests up to three focuses from unfinished current-quarter work and attention-required key results.</summary>
  Task<List<WeeklyFocusEntity>> GetWeeklyFocusSuggestionsAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default);

  /// <summary>Replaces the week focuses, accepting at most three non-empty entries.</summary>
  Task<List<WeeklyFocusEntity>> ReplaceWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    IReadOnlyCollection<WeeklyFocusEntity> focuses,
    CancellationToken cancellationToken = default);

  /// <summary>Returns the latest compact reviews.</summary>
  Task<List<PlanningReviewEntity>> GetRecentReviewsAsync(
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Completes a compact review and records the financial snapshot used by it.</summary>
  Task<PlanningReviewEntity> CompleteReviewAsync(
    PlanningReviewEntity review,
    string userId,
    AnnualPlanEntity annualPlan,
    IReadOnlyCollection<WeeklyFocusEntity> nextFocuses,
    CancellationToken cancellationToken = default);

  /// <summary>Refreshes key results that are bound to canonical financial metrics.</summary>
  Task RefreshFinancialKeyResultsAsync(
    string userId,
    FinancialFacts facts,
    CancellationToken cancellationToken = default);
}
