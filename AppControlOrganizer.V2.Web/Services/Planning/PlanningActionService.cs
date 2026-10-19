//-----------------------------------------------------------------------
// <copyright file="PlanningActionService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using System.Diagnostics;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Analytics;
using AppControlOrganizer.V2.Web.Services.Logging;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;

/// <summary>
/// Generates and persists deterministic planning actions from existing application data.
/// </summary>
public sealed class PlanningActionService : IPlanningActionService
{
  private static readonly string[] ManagedSourceCodes =
  {
    "FIN_UNPRICED_POSITIONS",
    "FIN_RESERVE_GAP",
    "FIN_IDLE_CASH",
    "FIN_TARGET_GAP",
    "PLAN_WEEKLY_FOCUS_MISSING",
    "PLAN_REVIEW_DUE",
    "DATA_ALLOCATION_PURPOSE",
    "DATA_TRANSACTION_IMPACT",
  };

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly IFinancialFactsService financialFactsService;
  private readonly IPlanningExperienceService planningExperienceService;
  private readonly IAccessAnalyticsRecorder accessAnalyticsRecorder;
  private readonly ILogger<PlanningActionService> logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="PlanningActionService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database-context factory.</param>
  /// <param name="financialFactsService">Canonical financial facts service.</param>
  /// <param name="planningExperienceService">Planning read service.</param>
  /// <param name="accessAnalyticsRecorder">Records manual generation as feature usage.</param>
  /// <param name="logger">Logger for generator diagnostics.</param>
  public PlanningActionService(
    IDbContextFactory<LifePlannerContext> contextFactory,
    IFinancialFactsService financialFactsService,
    IPlanningExperienceService planningExperienceService,
    IAccessAnalyticsRecorder accessAnalyticsRecorder,
    ILogger<PlanningActionService> logger)
  {
    this.contextFactory = contextFactory;
    this.financialFactsService = financialFactsService;
    this.planningExperienceService = planningExperienceService;
    this.accessAnalyticsRecorder = accessAnalyticsRecorder;
    this.logger = logger;
  }

  /// <inheritdoc />
  public async Task<List<PlanningActionEntity>> GetOpenActionsAsync(
    string userId,
    int take = 6,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.PlanningActions
      .AsNoTracking()
      .Where(action => action.OwnerUserId == userId && action.Status == PlanningActionStatus.Open)
      .OrderBy(action => action.Priority)
      .ThenByDescending(action => action.GeneratedAtUtc)
      .Take(take)
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<List<PlanningActionEntity>> GenerateForUserAsync(
    string userId,
    DateOnly referenceDate,
    bool manual,
    CancellationToken cancellationToken = default)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    string userIdHash = LogSafe.Hash(userId);
    using IDisposable userScope = LogContext.PushProperty("UserIdHash", userIdHash);
    bool isDemoTenant = userId == LifePlannerContext.DemoUserId;

    // A visitor can never trigger generation: the showcase action center is refreshed by the
    // scheduled run only, so anonymous sessions read it without ever writing to it.
    if (isDemoTenant && manual)
    {
      throw new DemoReadOnlyException();
    }

    using IDisposable? demoScope = isDemoTenant ? LifePlannerContext.AllowDemoWrites() : null;

    PlanningActionRunEntity run = new PlanningActionRunEntity
    {
      OwnerUserId = userId,
      ReferenceDate = referenceDate,
      Manual = manual,
      StartedAtUtc = DateTime.UtcNow,
    };

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    context.PlanningActionRuns.Add(run);
    try
    {
      PlanningActionRuleInput input = await this.BuildInputAsync(userId, referenceDate, cancellationToken);
      IReadOnlyList<PlanningActionCandidate> candidates = PlanningActionRules.BuildCandidates(input);
      await this.MergeCandidatesAsync(context, userId, referenceDate, candidates, cancellationToken);
      List<PlanningActionEntity> open = await context.PlanningActions
        .Where(action => action.OwnerUserId == userId && action.Status == PlanningActionStatus.Open)
        .OrderBy(action => action.Priority)
        .ToListAsync(cancellationToken);

      run.Succeeded = true;
      run.OpenActionCount = open.Count;
      run.CompletedAtUtc = DateTime.UtcNow;
      await context.SaveChangesAsync(cancellationToken);
      if (manual)
      {
        this.accessAnalyticsRecorder.RecordFeatureUsage("planning.actions_generated", userId);
      }

      this.logger.LogInformation(
        "Planning action generation finished for user. Status={Status}. OpenActions={OpenActions}. Manual={Manual}. ElapsedMs={ElapsedMs}.",
        "Success",
        open.Count,
        manual,
        stopwatch.ElapsedMilliseconds);
      return open;
    }
    catch (Exception exception)
    {
      run.Succeeded = false;
      run.ErrorMessage = exception.Message.Length > 1000
        ? exception.Message[..1000]
        : exception.Message;
      run.CompletedAtUtc = DateTime.UtcNow;
      await context.SaveChangesAsync(cancellationToken);
      this.logger.LogWarning(
        exception,
        "Planning action generation failed for user. Status={Status}. Manual={Manual}. ElapsedMs={ElapsedMs}.",
        "Failed",
        manual,
        stopwatch.ElapsedMilliseconds);
      throw;
    }
  }

  /// <inheritdoc />
  public async Task GenerateForAllUsersAsync(CancellationToken cancellationToken = default)
  {
    DateOnly referenceDate = DateOnly.FromDateTime(DateTime.Now);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<string> userIds = await ReadKnownUserIdsAsync(context, cancellationToken);
    foreach (string userId in userIds)
    {
      try
      {
        await this.GenerateForUserAsync(userId, referenceDate, manual: false, cancellationToken);
      }
      catch (Exception exception)
      {
        this.logger.LogWarning(
          exception,
          "Skipping planning action generation for user. UserIdHash={UserIdHash}.",
          LogSafe.Hash(userId));
      }
    }
  }

  /// <inheritdoc />
  public Task CompleteActionAsync(
    int actionId,
    string userId,
    CancellationToken cancellationToken = default)
  {
    return this.ResolveActionAsync(actionId, userId, PlanningActionStatus.Done, cancellationToken);
  }

  /// <inheritdoc />
  public Task DismissActionAsync(
    int actionId,
    string userId,
    CancellationToken cancellationToken = default)
  {
    return this.ResolveActionAsync(actionId, userId, PlanningActionStatus.Dismissed, cancellationToken);
  }

  private static async Task<List<string>> ReadKnownUserIdsAsync(
    LifePlannerContext context,
    CancellationToken cancellationToken)
  {
    List<string> userIds = await context.Users
      .AsNoTracking()
      .Select(user => user.Id)
      .ToListAsync(cancellationToken);

    userIds.Add(LifePlannerContext.DemoUserId);

    List<string?> accountOwners = await context.Accounts
      .IgnoreQueryFilters()
      .Select(account => EF.Property<string?>(account, "OwnerUserId"))
      .Distinct()
      .ToListAsync(cancellationToken);
    List<string?> allocationOwners = await context.Allocations
      .IgnoreQueryFilters()
      .Select(allocation => EF.Property<string?>(allocation, "OwnerUserId"))
      .Distinct()
      .ToListAsync(cancellationToken);
    List<string?> transactionOwners = await context.Transactions
      .IgnoreQueryFilters()
      .Select(transaction => EF.Property<string?>(transaction, "OwnerUserId"))
      .Distinct()
      .ToListAsync(cancellationToken);
    List<string> planningOwners = await context.AnnualPlans
      .IgnoreQueryFilters()
      .Select(plan => plan.OwnerUserId)
      .Distinct()
      .ToListAsync(cancellationToken);

    userIds.AddRange(accountOwners
      .Where(owner => !string.IsNullOrWhiteSpace(owner))
      .Select(owner => owner!));
    userIds.AddRange(allocationOwners
      .Where(owner => !string.IsNullOrWhiteSpace(owner))
      .Select(owner => owner!));
    userIds.AddRange(transactionOwners
      .Where(owner => !string.IsNullOrWhiteSpace(owner))
      .Select(owner => owner!));
    userIds.AddRange(planningOwners);

    return userIds
      .Where(userId => !string.IsNullOrWhiteSpace(userId))
      .Distinct(StringComparer.Ordinal)
      .ToList();
  }

  private async Task<PlanningActionRuleInput> BuildInputAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken)
  {
    AnnualPlanEntity? annualPlan = await this.planningExperienceService.GetAnnualPlanAsync(
      userId,
      referenceDate.Year,
      cancellationToken);
    FinancialFacts facts = await this.financialFactsService.GetWithAnnualPlanAsync(
      userId,
      referenceDate,
      cancellationToken);

    Task<List<WeeklyFocusEntity>> focusesTask =
      this.planningExperienceService.GetWeeklyFocusesAsync(userId, referenceDate, cancellationToken);
    Task<List<PlanningReviewEntity>> reviewsTask =
      this.planningExperienceService.GetRecentReviewsAsync(userId, cancellationToken);
    Task<int> allocationCountTask = this.CountUnclassifiedAllocationsAsync(userId, cancellationToken);
    Task<int> transactionCountTask = this.CountUnclassifiedTransactionsAsync(userId, referenceDate, cancellationToken);
    await Task.WhenAll(focusesTask, reviewsTask, allocationCountTask, transactionCountTask);

    return new PlanningActionRuleInput
    {
      ReferenceDate = referenceDate,
      Facts = facts,
      AnnualPlan = annualPlan,
      HasWeeklyFocuses = (await focusesTask).Count > 0,
      LastReviewDate = (await reviewsTask).FirstOrDefault()?.ReviewDate,
      UnclassifiedAllocationCount = await allocationCountTask,
      UnclassifiedTransactionCount = await transactionCountTask,
    };
  }

  private async Task<int> CountUnclassifiedAllocationsAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Allocations
      .IgnoreQueryFilters()
      .CountAsync(allocation => EF.Property<string>(allocation, "OwnerUserId") == userId
        && !allocation.Deleted
        && allocation.PlanningPurpose == FinancialPurpose.Unclassified,
        cancellationToken);
  }

  private async Task<int> CountUnclassifiedTransactionsAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken)
  {
    DateTime start = new DateTime(referenceDate.Year, referenceDate.Month, 1);
    DateTime end = start.AddMonths(1);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Transactions
      .IgnoreQueryFilters()
      .CountAsync(transaction => EF.Property<string>(transaction, "OwnerUserId") == userId
        && !transaction.Deleted
        && transaction.Date >= start
        && transaction.Date < end
        && transaction.PlanningImpact == TransactionPlanningImpact.Unclassified
        && !transaction.PlanningClassificationConfirmed,
        cancellationToken);
  }

  private async Task MergeCandidatesAsync(
    LifePlannerContext context,
    string userId,
    DateOnly referenceDate,
    IReadOnlyList<PlanningActionCandidate> candidates,
    CancellationToken cancellationToken)
  {
    List<PlanningActionEntity> existing = await context.PlanningActions
      .IgnoreQueryFilters()
      .Where(action => action.OwnerUserId == userId
        && !action.Deleted
        && ManagedSourceCodes.Contains(action.SourceCode))
      .ToListAsync(cancellationToken);
    DateTime now = DateTime.UtcNow;
    HashSet<string> currentCodes = candidates
      .Select(candidate => candidate.SourceCode)
      .ToHashSet(StringComparer.Ordinal);

    foreach (PlanningActionCandidate candidate in candidates)
    {
      PlanningActionEntity? target = existing
        .Where(action => action.SourceCode == candidate.SourceCode)
        .OrderByDescending(action => action.GeneratedAtUtc)
        .FirstOrDefault(action => action.Status == PlanningActionStatus.Open)
        ?? existing.FirstOrDefault(action => action.SourceCode == candidate.SourceCode
          && action.ReferenceDate == referenceDate);
      if (target == null)
      {
        target = new PlanningActionEntity
        {
          OwnerUserId = userId,
          Status = PlanningActionStatus.Open,
        };
        context.PlanningActions.Add(target);
      }
      else if (target.Status != PlanningActionStatus.Open)
      {
        continue;
      }

      ApplyCandidate(target, candidate, referenceDate, now);
    }

    foreach (PlanningActionEntity action in existing.Where(action =>
      action.Status == PlanningActionStatus.Open
      && !currentCodes.Contains(action.SourceCode)))
    {
      action.Status = PlanningActionStatus.Expired;
      action.Active = false;
      action.ResolvedAtUtc = now;
    }

    await context.SaveChangesAsync(cancellationToken);
  }

  private static void ApplyCandidate(
    PlanningActionEntity target,
    PlanningActionCandidate source,
    DateOnly referenceDate,
    DateTime generatedAtUtc)
  {
    target.ReferenceDate = referenceDate;
    target.SourceCode = source.SourceCode;
    target.ActionType = source.ActionType;
    target.Priority = source.Priority;
    target.IconCss = source.IconCss;
    target.TitleKey = source.TitleKey;
    target.DetailKey = source.DetailKey;
    target.EvidenceText = source.EvidenceText;
    target.DestinationHref = source.DestinationHref;
    target.PrimaryActionKey = source.PrimaryActionKey;
    target.DueDate = source.DueDate;
    target.GeneratedAtUtc = generatedAtUtc;
    target.Status = PlanningActionStatus.Open;
    target.Active = true;
    target.Deleted = false;
    target.ResolvedAtUtc = null;
  }

  private async Task ResolveActionAsync(
    int actionId,
    string userId,
    PlanningActionStatus status,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    PlanningActionEntity? action = await context.PlanningActions
      .FirstOrDefaultAsync(item => item.Id == actionId
        && item.OwnerUserId == userId
        && item.Status == PlanningActionStatus.Open,
        cancellationToken);
    if (action == null)
    {
      return;
    }

    action.Status = status;
    action.Active = false;
    action.ResolvedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
  }
}
