//-----------------------------------------------------------------------
// <copyright file="IPlanningService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Provides all planning operations: OKRs, Quarter Plans, Reviews, Risks and Habits.
/// </summary>
public interface IPlanningService
{
  // ── Pillars ──────────────────────────────────────────────────────────────

  /// <summary>Returns all pillars owned by the user, ordered by SortOrder.</summary>
  Task<List<PillarEntity>> GetPillarsAsync(string userId);

  /// <summary>Saves (insert or update) a pillar.</summary>
  Task<PillarEntity> SavePillarAsync(PillarEntity entity, string userId);

  /// <summary>Soft-deletes a pillar and its objectives/KRs.</summary>
  Task DeletePillarAsync(int id, string userId);

  // ── Objectives ────────────────────────────────────────────────────────────

  /// <summary>Returns all objectives for a pillar, ordered by SortOrder.</summary>
  Task<List<ObjectiveEntity>> GetObjectivesAsync(int pillarId, string userId);

  /// <summary>Saves an objective.</summary>
  Task<ObjectiveEntity> SaveObjectiveAsync(ObjectiveEntity entity, string userId);

  /// <summary>Soft-deletes an objective.</summary>
  Task DeleteObjectiveAsync(int id, string userId);

  // ── Key Results ───────────────────────────────────────────────────────────

  /// <summary>Returns all key results for an objective, ordered by SortOrder.</summary>
  Task<List<KeyResultEntity>> GetKeyResultsAsync(int objectiveId, string userId);

  /// <summary>Saves a key result.</summary>
  Task<KeyResultEntity> SaveKeyResultAsync(KeyResultEntity entity, string userId);

  /// <summary>Soft-deletes a key result.</summary>
  Task DeleteKeyResultAsync(int id, string userId);

  /// <summary>Returns all pillars with objectives and key results loaded (full OKR tree).</summary>
  Task<List<PillarEntity>> GetFullOkrTreeAsync(string userId);

  // ── Quarter Plans ─────────────────────────────────────────────────────────

  /// <summary>Returns all quarter plans owned by the user, ordered by Year/Quarter.</summary>
  Task<List<QuarterPlanEntity>> GetQuarterPlansAsync(string userId);

  /// <summary>Returns a single quarter plan including its tasks.</summary>
  Task<QuarterPlanEntity?> GetQuarterPlanWithTasksAsync(int id, string userId);

  /// <summary>Saves a quarter plan.</summary>
  Task<QuarterPlanEntity> SaveQuarterPlanAsync(QuarterPlanEntity entity, string userId);

  /// <summary>Soft-deletes a quarter plan.</summary>
  Task DeleteQuarterPlanAsync(int id, string userId);

  /// <summary>Saves (insert or update) a quarter task.</summary>
  Task<QuarterTaskEntity> SaveQuarterTaskAsync(QuarterTaskEntity entity, string userId);

  /// <summary>Toggles the Done flag on a quarter task.</summary>
  Task ToggleQuarterTaskAsync(int taskId, string userId);

  /// <summary>Soft-deletes a quarter task.</summary>
  Task DeleteQuarterTaskAsync(int id, string userId);

  // ── Review Templates ──────────────────────────────────────────────────────

  /// <summary>Returns all review templates owned by the user.</summary>
  Task<List<ReviewTemplateEntity>> GetReviewTemplatesAsync(string userId);

  /// <summary>Returns a template including its items.</summary>
  Task<ReviewTemplateEntity?> GetReviewTemplateWithItemsAsync(int id, string userId);

  /// <summary>Saves a review template and its items.</summary>
  Task<ReviewTemplateEntity> SaveReviewTemplateAsync(ReviewTemplateEntity entity, string userId);

  /// <summary>Soft-deletes a review template.</summary>
  Task DeleteReviewTemplateAsync(int id, string userId);

  /// <summary>Ensures at least one Weekly and one Monthly default template exist for the user; seeds them if not.</summary>
  Task EnsureDefaultTemplatesAsync(string userId);

  // ── Review Runs ───────────────────────────────────────────────────────────

  /// <summary>Returns review runs for the user, optionally filtered by type, ordered by RunDate descending.</summary>
  Task<List<ReviewRunEntity>> GetReviewRunsAsync(string userId, ReviewType? type = null, DateTime? from = null, DateTime? to = null);

  /// <summary>Returns a single run with its items.</summary>
  Task<ReviewRunEntity?> GetReviewRunWithItemsAsync(int id, string userId);

  /// <summary>Starts a new review run from a template (snapshots the current items).</summary>
  Task<ReviewRunEntity> StartReviewRunAsync(int templateId, DateTime runDate, string userId);

  /// <summary>Saves a run (updates scores, notes, item Done flags).</summary>
  Task SaveReviewRunAsync(ReviewRunEntity run, string userId);

  /// <summary>Soft-deletes a run.</summary>
  Task DeleteReviewRunAsync(int id, string userId);

  // ── Risks ──────────────────────────────────────────────────────────────────

  /// <summary>Returns all risks owned by the user, including pillar navigation.</summary>
  Task<List<RiskEntity>> GetRisksAsync(string userId);

  /// <summary>Saves a risk.</summary>
  Task<RiskEntity> SaveRiskAsync(RiskEntity entity, string userId);

  /// <summary>Soft-deletes a risk.</summary>
  Task DeleteRiskAsync(int id, string userId);

  // ── Habits ─────────────────────────────────────────────────────────────────

  /// <summary>Returns all active habits for the user.</summary>
  Task<List<HabitEntity>> GetHabitsAsync(string userId, bool includeInactive = false);

  /// <summary>Saves a habit.</summary>
  Task<HabitEntity> SaveHabitAsync(HabitEntity entity, string userId);

  /// <summary>Soft-deletes a habit.</summary>
  Task DeleteHabitAsync(int id, string userId);

  /// <summary>Returns all log entries for the given month (1-based month/year).</summary>
  Task<List<HabitLogEntity>> GetHabitLogsAsync(string userId, int year, int month);

  /// <summary>Toggles the Done flag for a habit on the given date; creates the log entry if it does not exist.</summary>
  Task<HabitLogEntity> ToggleHabitLogAsync(int habitId, DateTime logDate, string userId);

  /// <summary>
  /// Returns basic metrics for a habit: weekly % (current ISO week), monthly % (current month), and current streak.
  /// </summary>
  Task<HabitMetrics> GetHabitMetricsAsync(int habitId, string userId);
}

/// <summary>Basic computed metrics for a single habit.</summary>
public sealed class HabitMetrics
{
  /// <summary>Gets or sets the habit identifier.</summary>
  public int HabitId { get; set; }

  /// <summary>Gets or sets the done-percentage for the current ISO week (0–100).</summary>
  public double WeeklyPercent { get; set; }

  /// <summary>Gets or sets the done-percentage for the current calendar month (0–100).</summary>
  public double MonthlyPercent { get; set; }

  /// <summary>Gets or sets the current consecutive-day streak (streak breaks on a day with Done=false or no entry).</summary>
  public int CurrentStreak { get; set; }
}
