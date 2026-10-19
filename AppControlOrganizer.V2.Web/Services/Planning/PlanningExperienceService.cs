//-----------------------------------------------------------------------
// <copyright file="PlanningExperienceService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implements the simplified planning experience without replacing the existing advanced planning service.
/// </summary>
public sealed class PlanningExperienceService : IPlanningExperienceService
{
  /// <summary>Neutral name given to a routine created for a profile that has none yet.</summary>
  private const string DefaultRoutineName = "My routine";

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly IFinancialFactsService financialFactsService;

  /// <summary>
  /// Initializes a new instance of the <see cref="PlanningExperienceService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database-context factory.</param>
  /// <param name="financialFactsService">Canonical financial facts service.</param>
  public PlanningExperienceService(
    IDbContextFactory<LifePlannerContext> contextFactory,
    IFinancialFactsService financialFactsService)
  {
    this.contextFactory = contextFactory;
    this.financialFactsService = financialFactsService;
  }

  /// <inheritdoc />
  public async Task<AnnualPlanEntity?> GetAnnualPlanAsync(
    string userId,
    int year,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.AnnualPlans
      .FirstOrDefaultAsync(item => item.OwnerUserId == userId && item.Year == year, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<AnnualPlanEntity> GetOrCreateAnnualPlanAsync(
    string userId,
    int year,
    FinancialFacts facts,
    CancellationToken cancellationToken = default)
  {
    AnnualPlanEntity? existing = await this.GetAnnualPlanAsync(userId, year, cancellationToken);
    if (existing != null)
    {
      return existing;
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<KeyResultEntity> financialResults = await context.KeyResults
      .Where(item => item.OwnerUserId == userId)
      .ToListAsync(cancellationToken);
    decimal target = financialResults
      .Where(item => IsNetWorthTitle(item.Title))
      .Select(item => item.Target)
      .DefaultIfEmpty(facts.NetWorth)
      .Max();

    AnnualPlanEntity plan = new AnnualPlanEntity
    {
      OwnerUserId = userId,
      Year = year,
      Theme = year.ToString(System.Globalization.CultureInfo.InvariantCulture),
      TargetNetWorth = target,
      PlannedMonthlyIncome = facts.SuggestedMonthlyIncome > 0m ? facts.SuggestedMonthlyIncome : null,
      MonthlySpendingCeiling = facts.SuggestedMonthlyExpense > 0m ? facts.SuggestedMonthlyExpense : null,
      EmergencyReserveMonths = 6,
      UpdatedAtUtc = DateTime.UtcNow,
    };
    context.AnnualPlans.Add(plan);
    await context.SaveChangesAsync(cancellationToken);

    foreach (KeyResultEntity result in financialResults)
    {
      FinancialMetricSource source = InferMetricSource(result.Title);
      if (source != FinancialMetricSource.Manual)
      {
        result.MetricSource = source;
      }
    }

    List<ObjectiveEntity> objectives = await context.Objectives
      .Where(item => item.OwnerUserId == userId)
      .ToListAsync(cancellationToken);
    foreach (ObjectiveEntity objective in objectives.Where(item =>
      financialResults.Any(result => result.ObjectiveId == item.Id)))
    {
      objective.AnnualPlanId = plan.Id;
    }

    await context.SaveChangesAsync(cancellationToken);
    return plan;
  }

  /// <inheritdoc />
  public async Task<AnnualPlanEntity> SaveAnnualPlanAsync(
    AnnualPlanEntity plan,
    string userId,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    AnnualPlanEntity? existing = await context.AnnualPlans
      .FirstOrDefaultAsync(item => item.Id == plan.Id && item.OwnerUserId == userId, cancellationToken);
    if (existing == null)
    {
      throw new InvalidOperationException("Annual plan not found for the current user.");
    }

    existing.Theme = plan.Theme.Trim();
    existing.TargetNetWorth = plan.TargetNetWorth;
    existing.PlannedMonthlyIncome = plan.PlannedMonthlyIncome;
    existing.MonthlySpendingCeiling = plan.MonthlySpendingCeiling;
    existing.PlannedAnnualExpenses = plan.PlannedAnnualExpenses;
    existing.PlannedMonthlyContribution = plan.PlannedMonthlyContribution;
    existing.PlannedOneOffNetFlows = plan.PlannedOneOffNetFlows;
    existing.EmergencyReserveMonths = Math.Clamp(plan.EmergencyReserveMonths, 1, 24);
    existing.UpdatedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
    return existing;
  }

  /// <inheritdoc />
  public async Task<RoutineTemplateEntity?> GetActiveRoutineAsync(
    string userId,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    RoutineTemplateEntity? template = await context.RoutineTemplates
      .Include(item => item.Blocks)
      .FirstOrDefaultAsync(
        item => item.OwnerUserId == userId && item.IsActiveTemplate,
        cancellationToken);

    if (template == null)
    {
      // A profile starts with an empty week and fills it in; the showcase tenant is read-only and
      // receives its routine from the demonstration seeder, so nothing is created on a read here.
      if (userId == LifePlannerContext.DemoUserId)
      {
        return null;
      }

      template = new RoutineTemplateEntity
      {
        OwnerUserId = userId,
        Name = DefaultRoutineName,
        IsActiveTemplate = true,
      };
      context.RoutineTemplates.Add(template);
      await context.SaveChangesAsync(cancellationToken);
    }

    return await context.RoutineTemplates
      .AsNoTracking()
      .Include(item => item.Blocks)
      .SingleAsync(item => item.Id == template.Id, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<RoutineTemplateEntity> ReplaceRoutineAsync(
    string userId,
    string name,
    IReadOnlyCollection<RoutineBlockEntity> blocks,
    CancellationToken cancellationToken = default)
  {
    ValidateRoutineBlocks(blocks);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    RoutineTemplateEntity? template = await context.RoutineTemplates
      .Include(item => item.Blocks)
      .FirstOrDefaultAsync(
        item => item.OwnerUserId == userId && item.IsActiveTemplate,
        cancellationToken);

    if (template == null)
    {
      template = new RoutineTemplateEntity
      {
        OwnerUserId = userId,
        Name = string.IsNullOrWhiteSpace(name) ? DefaultRoutineName : name.Trim(),
        IsActiveTemplate = true,
      };
      context.RoutineTemplates.Add(template);
      await context.SaveChangesAsync(cancellationToken);
    }
    else
    {
      template.Name = string.IsNullOrWhiteSpace(name) ? template.Name : name.Trim();
      foreach (RoutineBlockEntity oldBlock in template.Blocks.Where(item => !item.Deleted))
      {
        oldBlock.Deleted = true;
        oldBlock.Active = false;
      }
    }

    foreach (RoutineBlockEntity source in blocks)
    {
      context.RoutineBlocks.Add(CloneBlock(source, userId, template.Id));
    }

    await context.SaveChangesAsync(cancellationToken);
    return await context.RoutineTemplates
      .AsNoTracking()
      .Include(item => item.Blocks.Where(block => !block.Deleted))
      .SingleAsync(item => item.Id == template.Id, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<RoutineBlockEntity> SaveRoutineBlockAsync(
    RoutineBlockEntity block,
    string userId,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(block.Title) || block.EndTime <= block.StartTime)
    {
      throw new InvalidOperationException("Every routine block needs a title and a valid time range.");
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    RoutineTemplateEntity? template = await context.RoutineTemplates
      .FirstOrDefaultAsync(
        item => item.Id == block.RoutineTemplateId && item.OwnerUserId == userId,
        cancellationToken);
    if (template == null)
    {
      throw new InvalidOperationException("Routine template not found for the current user.");
    }

    bool overlaps = await context.RoutineBlocks.AnyAsync(
      item => item.OwnerUserId == userId
        && item.RoutineTemplateId == template.Id
        && item.Id != block.Id
        && item.DayOfWeek == block.DayOfWeek
        && item.StartTime < block.EndTime
        && block.StartTime < item.EndTime,
      cancellationToken);
    if (overlaps)
    {
      throw new InvalidOperationException("This block overlaps another block on the same day.");
    }

    RoutineBlockEntity? target = block.Id == 0
      ? null
      : await context.RoutineBlocks.FirstOrDefaultAsync(
        item => item.Id == block.Id && item.OwnerUserId == userId,
        cancellationToken);
    if (target == null)
    {
      target = CloneBlock(block, userId, template.Id);
      context.RoutineBlocks.Add(target);
    }
    else
    {
      target.DayOfWeek = block.DayOfWeek;
      target.StartTime = block.StartTime;
      target.EndTime = block.EndTime;
      target.Title = block.Title.Trim();
      target.Category = block.Category?.Trim();
      target.IconCss = block.IconCss;
      target.ColorHex = block.ColorHex;
      target.SortOrder = block.SortOrder;
    }

    await context.SaveChangesAsync(cancellationToken);
    return target;
  }

  /// <inheritdoc />
  public async Task DeleteRoutineBlockAsync(
    int blockId,
    string userId,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    RoutineBlockEntity? block = await context.RoutineBlocks
      .FirstOrDefaultAsync(item => item.Id == blockId && item.OwnerUserId == userId, cancellationToken);
    if (block == null)
    {
      return;
    }

    block.Deleted = true;
    block.Active = false;
    await context.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<List<WeeklyFocusEntity>> GetWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default)
  {
    DateOnly weekStart = GetWeekStart(date);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.WeeklyFocuses
      .AsNoTracking()
      .Where(item => item.OwnerUserId == userId && item.WeekStartDate == weekStart)
      .OrderBy(item => item.SortOrder)
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<List<WeeklyFocusEntity>> GetWeeklyFocusSuggestionsAsync(
    string userId,
    DateOnly date,
    CancellationToken cancellationToken = default)
  {
    int quarter = ((date.Month - 1) / 3) + 1;
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<WeeklyFocusEntity> suggestions = await context.QuarterPlans
      .AsNoTracking()
      .Where(plan => plan.OwnerUserId == userId
        && plan.Year == date.Year
        && plan.Quarter == quarter
        && plan.Active
        && !plan.Deleted)
      .SelectMany(plan => plan.Tasks
        .Where(task => task.Active && !task.Deleted && !task.Done)
        .OrderBy(task => task.SortOrder))
      .Select(task => new WeeklyFocusEntity
      {
        Text = task.Text,
        QuarterTaskId = task.Id,
        SortOrder = task.SortOrder,
      })
      .Take(3)
      .ToListAsync(cancellationToken);

    if (suggestions.Count < 3)
    {
      List<WeeklyFocusEntity> keyResults = await context.KeyResults
        .AsNoTracking()
        .Where(result => result.OwnerUserId == userId
          && result.Active
          && !result.Deleted
          && result.Status != RagStatus.Green
          && result.Objective != null
          && result.Objective.Active
          && !result.Objective.Deleted)
        .OrderByDescending(result => result.Status == RagStatus.Red)
        .ThenBy(result => result.Deadline ?? DateTime.MaxValue)
        .ThenBy(result => result.SortOrder)
        .Select(result => new WeeklyFocusEntity
        {
          Text = result.Title,
          ObjectiveId = result.ObjectiveId,
          KeyResultId = result.Id,
          SortOrder = result.SortOrder,
        })
        .Take(6)
        .ToListAsync(cancellationToken);
      foreach (WeeklyFocusEntity suggestion in keyResults)
      {
        if (suggestions.Count == 3)
        {
          break;
        }

        if (suggestions.Any(item => string.Equals(item.Text, suggestion.Text, StringComparison.OrdinalIgnoreCase)))
        {
          continue;
        }

        suggestions.Add(suggestion);
      }
    }

    for (int index = 0; index < suggestions.Count; index++)
    {
      suggestions[index].SortOrder = index + 1;
    }

    return suggestions;
  }

  /// <inheritdoc />
  public async Task<List<WeeklyFocusEntity>> ReplaceWeeklyFocusesAsync(
    string userId,
    DateOnly date,
    IReadOnlyCollection<WeeklyFocusEntity> focuses,
    CancellationToken cancellationToken = default)
  {
    List<WeeklyFocusEntity> valid = focuses
      .Where(item => !string.IsNullOrWhiteSpace(item.Text))
      .Take(3)
      .ToList();
    DateOnly weekStart = GetWeekStart(date);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<WeeklyFocusEntity> previous = await context.WeeklyFocuses
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == userId && item.WeekStartDate == weekStart)
      .ToListAsync(cancellationToken);
    int order = 1;
    foreach (WeeklyFocusEntity source in valid)
    {
      WeeklyFocusEntity? target = previous.FirstOrDefault(item => item.SortOrder == order);
      if (target == null)
      {
        target = new WeeklyFocusEntity
        {
          OwnerUserId = userId,
          WeekStartDate = weekStart,
          SortOrder = order,
        };
        context.WeeklyFocuses.Add(target);
      }

      target.Text = source.Text.Trim();
      target.ObjectiveId = source.ObjectiveId;
      target.KeyResultId = source.KeyResultId;
      target.QuarterTaskId = source.QuarterTaskId;
      target.RoutineBlockId = source.RoutineBlockId;
      target.Active = true;
      target.Deleted = false;
      order++;
    }

    foreach (WeeklyFocusEntity extra in previous.Where(item => item.SortOrder >= order))
    {
      extra.Deleted = true;
      extra.Active = false;
    }

    await context.SaveChangesAsync(cancellationToken);
    return await this.GetWeeklyFocusesAsync(userId, date, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<List<PlanningReviewEntity>> GetRecentReviewsAsync(
    string userId,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.PlanningReviews
      .AsNoTracking()
      .Where(item => item.OwnerUserId == userId)
      .OrderByDescending(item => item.ReviewDate)
      .Take(12)
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<PlanningReviewEntity> CompleteReviewAsync(
    PlanningReviewEntity review,
    string userId,
    AnnualPlanEntity annualPlan,
    IReadOnlyCollection<WeeklyFocusEntity> nextFocuses,
    CancellationToken cancellationToken = default)
  {
    FinancialSnapshotEntity snapshot = await this.financialFactsService.CaptureSnapshotAsync(
      userId,
      review.ReviewDate,
      annualPlan,
      cancellationToken);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    PlanningReviewEntity completed = new PlanningReviewEntity
    {
      OwnerUserId = userId,
      ReviewType = review.ReviewType,
      Status = PlanningReviewStatus.Completed,
      ReviewDate = review.ReviewDate,
      ProgressNotes = review.ProgressNotes.Trim(),
      BlockerNotes = review.BlockerNotes.Trim(),
      DecisionNotes = review.DecisionNotes.Trim(),
      FinancialSnapshotId = snapshot.Id,
      CompletedAtUtc = DateTime.UtcNow,
    };
    context.PlanningReviews.Add(completed);
    await context.SaveChangesAsync(cancellationToken);

    if (review.ReviewType == ReviewType.Weekly && nextFocuses.Count > 0)
    {
      await this.ReplaceWeeklyFocusesAsync(
        userId,
        review.ReviewDate.AddDays(7),
        nextFocuses,
        cancellationToken);
    }

    return completed;
  }

  /// <inheritdoc />
  public async Task RefreshFinancialKeyResultsAsync(
    string userId,
    FinancialFacts facts,
    CancellationToken cancellationToken = default)
  {
    // Anonymous sessions read a read-only showcase; refreshing it on page load would be a write.
    if (userId == LifePlannerContext.DemoUserId && !LifePlannerContext.IsDemoWriteScopeActive)
    {
      return;
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<KeyResultEntity> results = await context.KeyResults
      .Where(item => item.OwnerUserId == userId && item.MetricSource != FinancialMetricSource.Manual)
      .ToListAsync(cancellationToken);
    foreach (KeyResultEntity result in results)
    {
      result.Current = result.MetricSource switch
      {
        FinancialMetricSource.NetWorth => facts.NetWorth,
        FinancialMetricSource.EmergencyReserve => facts.EmergencyReserve,
        FinancialMetricSource.EmergencyReserveMonths => facts.SuggestedEssentialExpense > 0m
          ? facts.EmergencyReserve / facts.SuggestedEssentialExpense
          : 0m,
        FinancialMetricSource.IdleCashPercentage => facts.IdleCashPercentage,
        FinancialMetricSource.MonthlyOperatingExpense => facts.MonthlyOperatingExpense,
        FinancialMetricSource.MonthlyContribution => facts.MonthlyContributions,
        FinancialMetricSource.PlannedYearEndNetWorth => facts.PlannedYearEndNetWorth,
        _ => result.Current,
      };
      result.UpdatedAt = DateTime.UtcNow;
    }

    await context.SaveChangesAsync(cancellationToken);
  }

  private static RoutineBlockEntity CloneBlock(
    RoutineBlockEntity source,
    string userId,
    int templateId)
  {
    return new RoutineBlockEntity
    {
      OwnerUserId = userId,
      RoutineTemplateId = templateId,
      DayOfWeek = source.DayOfWeek,
      StartTime = source.StartTime,
      EndTime = source.EndTime,
      Title = source.Title.Trim(),
      Category = source.Category?.Trim(),
      IconCss = string.IsNullOrWhiteSpace(source.IconCss) ? "bi bi-circle" : source.IconCss,
      ColorHex = string.IsNullOrWhiteSpace(source.ColorHex) ? "#64748b" : source.ColorHex,
      SortOrder = source.SortOrder,
    };
  }

  internal static void ValidateRoutineBlocks(IReadOnlyCollection<RoutineBlockEntity> blocks)
  {
    foreach (RoutineBlockEntity block in blocks)
    {
      if (string.IsNullOrWhiteSpace(block.Title) || block.EndTime <= block.StartTime)
      {
        throw new InvalidOperationException("Every routine block needs a title and a valid time range.");
      }
    }

    foreach (IGrouping<DayOfWeek, RoutineBlockEntity> day in blocks.GroupBy(item => item.DayOfWeek))
    {
      List<RoutineBlockEntity> ordered = day.OrderBy(item => item.StartTime).ToList();
      for (int index = 1; index < ordered.Count; index++)
      {
        if (ordered[index].StartTime < ordered[index - 1].EndTime)
        {
          throw new InvalidOperationException("Routine blocks cannot overlap on the same day.");
        }
      }
    }
  }

  internal static DateOnly GetWeekStart(DateOnly date)
  {
    return date.AddDays(-(int)date.DayOfWeek);
  }

  private static bool IsNetWorthTitle(string title)
  {
    return title.Contains("patrim", StringComparison.OrdinalIgnoreCase)
      || title.Contains("net worth", StringComparison.OrdinalIgnoreCase);
  }

  internal static FinancialMetricSource InferMetricSource(string title)
  {
    if (IsNetWorthTitle(title))
    {
      return FinancialMetricSource.NetWorth;
    }

    if (title.Contains("reserva", StringComparison.OrdinalIgnoreCase))
    {
      return FinancialMetricSource.EmergencyReserve;
    }

    if (title.Contains("parado", StringComparison.OrdinalIgnoreCase)
      || title.Contains("idle", StringComparison.OrdinalIgnoreCase))
    {
      return FinancialMetricSource.IdleCashPercentage;
    }

    if (title.Contains("aporte", StringComparison.OrdinalIgnoreCase)
      || title.Contains("contribution", StringComparison.OrdinalIgnoreCase))
    {
      return FinancialMetricSource.MonthlyContribution;
    }

    if (title.Contains("gasto", StringComparison.OrdinalIgnoreCase)
      || title.Contains("expense", StringComparison.OrdinalIgnoreCase))
    {
      return FinancialMetricSource.MonthlyOperatingExpense;
    }

    return FinancialMetricSource.Manual;
  }
}
