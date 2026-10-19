//-----------------------------------------------------------------------
// <copyright file="FinancialFactsService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Calculates canonical financial facts from accounts, allocations, transactions, and market data.
/// </summary>
public sealed class FinancialFactsService : IFinancialFactsService
{
  private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);

  private readonly Dictionary<FinancialFactsCacheKey, Task<FinancialFacts>> activeLoads = new();
  private readonly Dictionary<FinancialFactsCacheKey, CachedFinancialFacts> cachedFacts = new();
  private readonly object cacheSync = new();
  private readonly Dictionary<string, int> cacheVersions = new(StringComparer.Ordinal);
  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly MarketQuoteCache quoteCache;
  private readonly AllocationQuotePricingService pricingService;

  /// <summary>
  /// Initializes a new instance of the <see cref="FinancialFactsService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database-context factory.</param>
  /// <param name="quoteCache">Latest successful market-quote cache.</param>
  /// <param name="pricingService">Central allocation pricing service.</param>
  public FinancialFactsService(
    IDbContextFactory<LifePlannerContext> contextFactory,
    MarketQuoteCache quoteCache,
    AllocationQuotePricingService pricingService)
  {
    this.contextFactory = contextFactory;
    this.quoteCache = quoteCache;
    this.pricingService = pricingService;
  }

  /// <inheritdoc />
  public void Invalidate(string userId)
  {
    lock (this.cacheSync)
    {
      int nextVersion = this.cacheVersions.GetValueOrDefault(userId) + 1;
      this.cacheVersions[userId] = nextVersion;
      List<FinancialFactsCacheKey> keys = this.cachedFacts.Keys
        .Where(key => key.UserId == userId)
        .ToList();
      foreach (FinancialFactsCacheKey key in keys)
      {
        this.cachedFacts.Remove(key);
      }
    }
  }

  /// <inheritdoc />
  public async Task<FinancialFacts> GetAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default)
  {
    FinancialFacts facts = await this.GetBaseFactsAsync(userId, referenceDate, cancellationToken);
    this.CalculatePlan(facts, annualPlan, referenceDate);
    this.BuildSignals(facts, annualPlan);

    return facts;
  }

  /// <inheritdoc />
  public async Task<FinancialFacts> GetWithAnnualPlanAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken = default)
  {
    Task<FinancialFacts> factsTask = this.GetBaseFactsAsync(userId, referenceDate, cancellationToken);
    Task<AnnualPlanEntity?> planTask = this.LoadAnnualPlanAsync(userId, referenceDate.Year, cancellationToken);
    await Task.WhenAll(factsTask, planTask);
    FinancialFacts facts = await factsTask;
    AnnualPlanEntity? annualPlan = await planTask;
    this.CalculatePlan(facts, annualPlan, referenceDate);
    this.BuildSignals(facts, annualPlan);

    return facts;
  }

  private Task<FinancialFacts> GetBaseFactsAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken)
  {
    FinancialFactsCacheKey key;
    Task<FinancialFacts> loadTask;
    lock (this.cacheSync)
    {
      int version = this.cacheVersions.GetValueOrDefault(userId);
      key = new FinancialFactsCacheKey(userId, referenceDate, version);
      if (this.cachedFacts.TryGetValue(key, out CachedFinancialFacts? cached)
        && DateTime.UtcNow - cached.CachedAtUtc <= CacheLifetime)
      {
        return Task.FromResult(CloneBaseFacts(cached.Facts));
      }

      if (!this.activeLoads.TryGetValue(key, out loadTask!))
      {
        loadTask = this.LoadBaseFactsAsync(userId, referenceDate, cancellationToken);
        this.activeLoads[key] = loadTask;
      }
    }

    return this.AwaitBaseFactsAsync(key, loadTask);
  }

  private async Task<FinancialFacts> AwaitBaseFactsAsync(
    FinancialFactsCacheKey key,
    Task<FinancialFacts> loadTask)
  {
    try
    {
      FinancialFacts facts = await loadTask;
      lock (this.cacheSync)
      {
        int currentVersion = this.cacheVersions.GetValueOrDefault(key.UserId);
        if (currentVersion == key.Version)
        {
          this.cachedFacts[key] = new CachedFinancialFacts(facts, DateTime.UtcNow);
        }
      }

      return CloneBaseFacts(facts);
    }
    finally
    {
      lock (this.cacheSync)
      {
        if (this.activeLoads.TryGetValue(key, out Task<FinancialFacts>? active)
          && ReferenceEquals(active, loadTask))
        {
          this.activeLoads.Remove(key);
        }
      }
    }
  }

  private async Task<FinancialFacts> LoadBaseFactsAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken)
  {
    Task<List<AccountEntity>> accountsTask = this.LoadAccountsAsync(userId, cancellationToken);
    Task<List<AllocationEntity>> allocationsTask = this.LoadAllocationsAsync(userId, cancellationToken);
    Task<List<AllocationHistoryEntity>> allocationHistoriesTask = this.LoadAllocationHistoriesAsync(userId, cancellationToken);
    Task<List<TransactionEntity>> transactionsTask = this.LoadTransactionsAsync(userId, cancellationToken);
    Task<IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot>> quotesTask =
      this.quoteCache.GetLatestSuccessQuotesAsync(cancellationToken);
    await Task.WhenAll(accountsTask, allocationsTask, allocationHistoriesTask, transactionsTask, quotesTask);
    List<AccountEntity> accounts = await accountsTask;
    List<AllocationEntity> allocations = await allocationsTask;
    List<AllocationHistoryEntity> allocationHistories = await allocationHistoriesTask;
    List<TransactionEntity> transactions = await transactionsTask;
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes = await quotesTask;
    FinancialFacts facts = new FinancialFacts
    {
      ReferenceDate = referenceDate,
      CalculatedAtUtc = DateTime.UtcNow,
    };
    if (MarketQuoteCache.TryGetByType(quotes, AllocationType.UsdBrl, out MarketQuoteSnapshot? usdBrl)
      && usdBrl != null
      && usdBrl.Price > 0m)
    {
      facts.UsdBrlRate = usdBrl.Price;
      facts.UsdBrlRetrievedAtUtc = usdBrl.RetrievedAtUtc;
    }

    this.CalculatePositions(facts, accounts, allocations, transactions, quotes, referenceDate);
    this.CalculateCashFlow(facts, transactions, allocationHistories, referenceDate);
    return facts;
  }

  private async Task<List<AccountEntity>> LoadAccountsAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Accounts
      .AsNoTracking()
      .Where(account => EF.Property<string>(account, "OwnerUserId") == userId)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<AllocationEntity>> LoadAllocationsAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Allocations
      .AsNoTracking()
      .Where(allocation => EF.Property<string>(allocation, "OwnerUserId") == userId)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<TransactionEntity>> LoadTransactionsAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Transactions
      .AsNoTracking()
      .Where(transaction => EF.Property<string>(transaction, "OwnerUserId") == userId)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<AllocationHistoryEntity>> LoadAllocationHistoriesAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.AllocationHistories
      .AsNoTracking()
      .Where(history => history.OwnerUserId == userId)
      .ToListAsync(cancellationToken);
  }

  private async Task<AnnualPlanEntity?> LoadAnnualPlanAsync(
    string userId,
    int year,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.AnnualPlans
      .AsNoTracking()
      .FirstOrDefaultAsync(
        plan => plan.OwnerUserId == userId && plan.Year == year,
        cancellationToken);
  }

  private static FinancialFacts CloneBaseFacts(FinancialFacts source)
  {
    return new FinancialFacts
    {
      ReferenceDate = source.ReferenceDate,
      CalculatedAtUtc = source.CalculatedAtUtc,
      NetWorth = source.NetWorth,
      AccountCash = source.AccountCash,
      AllocationCurrentValue = source.AllocationCurrentValue,
      AllocationAppliedValue = source.AllocationAppliedValue,
      ExcludedAllocationValue = source.ExcludedAllocationValue,
      SkippedDuplicateAccountBalance = source.SkippedDuplicateAccountBalance,
      ProviderAccountBalance = source.ProviderAccountBalance,
      ConfirmedValue = source.ConfirmedValue,
      EstimatedValue = source.EstimatedValue,
      OperatingCash = source.OperatingCash,
      EmergencyReserve = source.EmergencyReserve,
      EmergencyReserveAllocationValue = source.EmergencyReserveAllocationValue,
      EmergencyReserveAllocationCount = source.EmergencyReserveAllocationCount,
      EmergencyReserveAllocations = source.EmergencyReserveAllocations
        .Select(allocation => new FinancialReserveAllocationDetail
        {
          AllocationId = allocation.AllocationId,
          AllocationName = allocation.AllocationName,
          AccountName = allocation.AccountName,
          CurrentAmountBrl = allocation.CurrentAmountBrl,
          SourceKey = allocation.SourceKey,
        })
        .ToList(),
      IdleCash = source.IdleCash,
      LongTermInvestments = source.LongTermInvestments,
      MonthlyOperatingIncome = source.MonthlyOperatingIncome,
      MonthlyOperatingExpense = source.MonthlyOperatingExpense,
      MonthlyContributions = source.MonthlyContributions,
      YearToDateContributions = source.YearToDateContributions,
      YearToDateContributionMonths = source.YearToDateContributionMonths,
      SuggestedMonthlyIncome = source.SuggestedMonthlyIncome,
      SuggestedMonthlyExpense = source.SuggestedMonthlyExpense,
      SuggestedEssentialExpense = source.SuggestedEssentialExpense,
      ProjectedRemainingContributions = source.ProjectedRemainingContributions,
      ProjectedRemainingAssetAppreciation = source.ProjectedRemainingAssetAppreciation,
      ProjectedOneOffFlows = source.ProjectedOneOffFlows,
      UnpricedPositionCount = source.UnpricedPositionCount,
      RecurringPatternCount = source.RecurringPatternCount,
      UsdBrlRate = source.UsdBrlRate,
      UsdBrlRetrievedAtUtc = source.UsdBrlRetrievedAtUtc,
      AccountPositions = source.AccountPositions
        .Select(position => new FinancialAccountPosition
        {
          AccountId = position.AccountId,
          IncludedCash = position.IncludedCash,
          AllocationCurrentValue = position.AllocationCurrentValue,
          AllocationAppliedValue = position.AllocationAppliedValue,
          SkippedDuplicateBalance = position.SkippedDuplicateBalance,
          ProviderBalance = position.ProviderBalance,
        })
        .ToList(),
    };
  }

  /// <inheritdoc />
  public async Task<FinancialSnapshotEntity> CaptureSnapshotAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default)
  {
    FinancialFacts facts = await this.GetAsync(userId, referenceDate, annualPlan, cancellationToken);

    // Anonymous sessions must not write: return the computed snapshot without persisting it.
    // The showcase tenant keeps its stored snapshots refreshed by the demonstration seeder instead.
    if (userId == LifePlannerContext.DemoUserId && !LifePlannerContext.IsDemoWriteScopeActive)
    {
      return BuildTransientSnapshot(userId, referenceDate, facts);
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    FinancialSnapshotEntity? snapshot = await context.FinancialSnapshots
      .FirstOrDefaultAsync(
        item => item.OwnerUserId == userId && item.SnapshotDate == referenceDate,
        cancellationToken);

    if (snapshot == null)
    {
      snapshot = new FinancialSnapshotEntity
      {
        OwnerUserId = userId,
        SnapshotDate = referenceDate,
      };
      context.FinancialSnapshots.Add(snapshot);
    }

    snapshot.CalculatedAtUtc = facts.CalculatedAtUtc;
    snapshot.NetWorth = facts.NetWorth;
    snapshot.ConfirmedValue = facts.ConfirmedValue;
    snapshot.EstimatedValue = facts.EstimatedValue;
    snapshot.OperatingCash = facts.OperatingCash;
    snapshot.EmergencyReserve = facts.EmergencyReserve;
    snapshot.IdleCash = facts.IdleCash;
    snapshot.LongTermInvestments = facts.LongTermInvestments;
    snapshot.MonthlyOperatingIncome = facts.MonthlyOperatingIncome;
    snapshot.MonthlyOperatingExpense = facts.MonthlyOperatingExpense;
    snapshot.MonthlyContributions = facts.MonthlyContributions;
    snapshot.UnpricedPositionCount = facts.UnpricedPositionCount;

    await context.SaveChangesAsync(cancellationToken);
    return snapshot;
  }

  /// <summary>
  /// Builds an in-memory snapshot for callers that must not persist, such as anonymous showcase sessions.
  /// </summary>
  /// <param name="userId">Owner the snapshot describes.</param>
  /// <param name="referenceDate">Reference date of the calculation.</param>
  /// <param name="facts">Facts already calculated for the reference date.</param>
  /// <returns>A detached snapshot carrying the calculated values.</returns>
  private static FinancialSnapshotEntity BuildTransientSnapshot(
    string userId,
    DateOnly referenceDate,
    FinancialFacts facts)
  {
    return new FinancialSnapshotEntity
    {
      OwnerUserId = userId,
      SnapshotDate = referenceDate,
      CalculatedAtUtc = facts.CalculatedAtUtc,
      NetWorth = facts.NetWorth,
      ConfirmedValue = facts.ConfirmedValue,
      EstimatedValue = facts.EstimatedValue,
      OperatingCash = facts.OperatingCash,
      EmergencyReserve = facts.EmergencyReserve,
      IdleCash = facts.IdleCash,
      LongTermInvestments = facts.LongTermInvestments,
      MonthlyOperatingIncome = facts.MonthlyOperatingIncome,
      MonthlyOperatingExpense = facts.MonthlyOperatingExpense,
      MonthlyContributions = facts.MonthlyContributions,
      UnpricedPositionCount = facts.UnpricedPositionCount,
    };
  }

  private void CalculatePositions(
    FinancialFacts facts,
    List<AccountEntity> accounts,
    List<AllocationEntity> allocations,
    List<TransactionEntity> transactions,
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    DateOnly referenceDate)
  {
    FinancialPositionBreakdown breakdown = FinancialPositionCalculator.CalculateBreakdown(
      accounts,
      allocations,
      transactions,
      quotes,
      this.pricingService,
      ResolvePurpose);
    facts.AccountCash = breakdown.AccountCash;
    facts.AllocationCurrentValue = breakdown.AllocationCurrentValue;
    facts.AllocationAppliedValue = breakdown.AllocationAppliedValue;
    facts.ExcludedAllocationValue = breakdown.ExcludedAllocationValue;
    facts.SkippedDuplicateAccountBalance = breakdown.SkippedDuplicateAccountBalance;
    facts.ProviderAccountBalance = breakdown.ProviderAccountBalance;
    facts.NetWorth = breakdown.NetWorth;
    facts.ConfirmedValue = breakdown.ConfirmedValue;
    facts.EstimatedValue = breakdown.EstimatedValue;
    facts.OperatingCash = breakdown.OperatingCash;
    facts.EmergencyReserve = breakdown.EmergencyReserve;
    facts.EmergencyReserveAllocationValue = breakdown.AllocationPositions
      .Where(position => position.Purpose == FinancialPurpose.EmergencyReserve)
      .Sum(position => position.CurrentAmountBrl);
    facts.EmergencyReserveAllocationCount = breakdown.AllocationPositions
      .Count(position => position.Purpose == FinancialPurpose.EmergencyReserve);
    facts.EmergencyReserveAllocations = BuildEmergencyReserveAllocations(
      accounts,
      allocations,
      breakdown);
    facts.IdleCash = breakdown.IdleCash;
    facts.LongTermInvestments = breakdown.LongTermInvestments;
    facts.UnpricedPositionCount = breakdown.UnpricedPositionCount;
    facts.AccountPositions = breakdown.AccountPositions;
  }

  private static List<FinancialReserveAllocationDetail> BuildEmergencyReserveAllocations(
    List<AccountEntity> accounts,
    List<AllocationEntity> allocations,
    FinancialPositionBreakdown breakdown)
  {
    Dictionary<int, string> accountNames = accounts.ToDictionary(account => account.Id, account => account.Name);
    Dictionary<int, AllocationEntity> allocationById = allocations.ToDictionary(allocation => allocation.Id);
    return breakdown.AllocationPositions
      .Where(position => position.Purpose == FinancialPurpose.EmergencyReserve)
      .Select(position =>
      {
        allocationById.TryGetValue(position.AllocationId, out AllocationEntity? allocation);
        string sourceKey = allocation?.PlanningPurpose == FinancialPurpose.EmergencyReserve
          ? "manual allocation purpose"
          : "inferred from allocation name";
        string accountName = allocation != null && accountNames.TryGetValue(allocation.AccountId, out string? name)
          ? name
          : string.Empty;
        return new FinancialReserveAllocationDetail
        {
          AllocationId = position.AllocationId,
          AllocationName = allocation?.Name ?? $"#{position.AllocationId}",
          AccountName = accountName,
          CurrentAmountBrl = position.CurrentAmountBrl,
          SourceKey = sourceKey,
        };
      })
      .OrderByDescending(allocation => allocation.CurrentAmountBrl)
      .ThenBy(allocation => allocation.AllocationName)
      .ToList();
  }

  private void CalculateCashFlow(
    FinancialFacts facts,
    List<TransactionEntity> transactions,
    List<AllocationHistoryEntity> allocationHistories,
    DateOnly referenceDate)
  {
    HashSet<int> internalTransfers = DetectInternalTransfers(transactions);
    DateTime monthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
    FinancialMonthlyCashFlow currentCashFlow = FinancialMonthlyCashFlowCalculator.Calculate(
      transactions,
      allocationHistories,
      referenceDate);
    facts.MonthlyOperatingIncome = currentCashFlow.OperatingIncome;
    facts.MonthlyOperatingExpense = currentCashFlow.OperatingExpense;
    facts.MonthlyContributions = currentCashFlow.Contributions;
    CalculateYearToDateContributions(facts, transactions, allocationHistories, referenceDate, internalTransfers);

    List<decimal> monthlyIncome = new List<decimal>();
    List<decimal> monthlyExpense = new List<decimal>();
    List<decimal> monthlyEssential = new List<decimal>();
    DateTime currentMonthStart = monthStart;
    for (int offset = 1; offset <= 6; offset++)
    {
      DateTime start = currentMonthStart.AddMonths(-offset);
      DateTime end = start.AddMonths(1);
      List<TransactionEntity> month = transactions
        .Where(item => item.Date >= start && item.Date < end)
        .ToList();
      monthlyIncome.Add(month
        .Where(item => item.Income && ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Operating)
        .Sum(item => item.Value));
      monthlyExpense.Add(month
        .Where(item => !item.Income && ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Operating)
        .Sum(item => item.Value));
      monthlyEssential.Add(month
        .Where(item => !item.Income
          && ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Operating
          && IsEssential(item.Category))
        .Sum(item => item.Value));
    }

    facts.SuggestedMonthlyIncome = Median(monthlyIncome.Where(value => value > 0m));
    facts.SuggestedMonthlyExpense = Median(monthlyExpense.Where(value => value > 0m));
    facts.SuggestedEssentialExpense = Median(monthlyEssential.Where(value => value > 0m));
    facts.RecurringPatternCount = transactions
      .GroupBy(item => new
      {
        Fingerprint = TransactionImportHasher.NormalizeDescription(item.Description),
        item.Income,
        item.Category,
      })
      .Count(group => group.Select(item => new { item.Date.Year, item.Date.Month }).Distinct().Count() >= 3);
  }

  private static void CalculateYearToDateContributions(
    FinancialFacts facts,
    List<TransactionEntity> transactions,
    List<AllocationHistoryEntity> allocationHistories,
    DateOnly referenceDate,
    HashSet<int> internalTransfers)
  {
    DateTime yearStart = new DateTime(referenceDate.Year, 1, 1);
    DateTime endExclusive = referenceDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
    List<TransactionEntity> contributionTransactions = transactions
      .Where(item => !item.Deleted
        && item.Date >= yearStart
        && item.Date < endExclusive
        && ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Contribution)
      .ToList();
    List<AllocationHistoryEntity> positiveContributionHistories = allocationHistories
      .Where(item => item.OccurredAt >= yearStart
        && item.OccurredAt < endExclusive
        && item.AmountBrl > 0m
        && item.PlanningPurpose == FinancialPurpose.LongTermInvestment
        && !HasMatchingContributionTransaction(item, contributionTransactions, internalTransfers))
      .ToList();

    facts.YearToDateContributions = contributionTransactions.Sum(item => item.Value)
      + positiveContributionHistories.Sum(item => item.AmountBrl);
    facts.YearToDateContributionMonths = referenceDate.Month;
  }

  private void CalculatePlan(FinancialFacts facts, AnnualPlanEntity? plan, DateOnly referenceDate)
  {
    FinancialPlanProjectionCalculator.Apply(facts, plan, referenceDate);
  }

  private void BuildSignals(FinancialFacts facts, AnnualPlanEntity? plan)
  {
    if (facts.UnpricedPositionCount > 0)
    {
      facts.Signals.Add(new FinancialSignal
      {
        Code = "UNPRICED_POSITIONS",
        TitleKey = "Some positions need pricing",
        DetailKey = "Review positions without a current BRL value.",
        RequiresAttention = true,
      });
    }

    if (facts.EmergencyReserveTarget > 0m && facts.EmergencyReserve < facts.EmergencyReserveTarget)
    {
      facts.Signals.Add(new FinancialSignal
      {
        Code = "RESERVE_GAP",
        TitleKey = "Emergency reserve below target",
        DetailKey = "The current reserve does not yet cover the planned number of months.",
        RequiresAttention = true,
      });
    }

    if (facts.IdleCashPercentage > 10m)
    {
      facts.Signals.Add(new FinancialSignal
      {
        Code = "IDLE_CASH",
        TitleKey = "Idle cash deserves a decision",
        DetailKey = "Consider assigning this cash to a goal, reserve, or investment.",
        RequiresAttention = true,
      });
    }

    if (plan?.TargetNetWorth > 0m && facts.PlannedYearEndNetWorth < plan.TargetNetWorth)
    {
      facts.Signals.Add(new FinancialSignal
      {
        Code = "TARGET_GAP",
        TitleKey = "The planned pace is below the annual target",
        DetailKey = "Review the monthly contribution or the annual assumptions.",
        RequiresAttention = true,
      });
    }
  }

  internal static FinancialPurpose ResolvePurpose(AllocationEntity allocation)
  {
    if (allocation.PlanningPurpose != FinancialPurpose.Unclassified)
    {
      return allocation.PlanningPurpose;
    }

    if (allocation.AllocationType != AllocationType.Stopped)
    {
      return FinancialPurpose.LongTermInvestment;
    }

    string normalized = $"{allocation.Name} {allocation.Annotations}".ToUpperInvariant();
    if (normalized.Contains("RESERVA", StringComparison.Ordinal)
      || normalized.Contains("(RE)", StringComparison.Ordinal))
    {
      return FinancialPurpose.EmergencyReserve;
    }

    if (normalized.Contains("PARADO", StringComparison.Ordinal)
      || normalized.Contains("IDLE", StringComparison.Ordinal))
    {
      return FinancialPurpose.IdleCash;
    }

    return FinancialPurpose.OperatingCash;
  }

  internal static TransactionPlanningImpact ResolveImpact(
    TransactionEntity transaction,
    HashSet<int> internalTransfers)
  {
    if (transaction.PlanningImpact != TransactionPlanningImpact.Unclassified)
    {
      return transaction.PlanningImpact;
    }

    if (internalTransfers.Contains(transaction.Id))
    {
      return TransactionPlanningImpact.InternalTransfer;
    }

    return transaction.Category == Category.Investiment
      ? TransactionPlanningImpact.Contribution
      : TransactionPlanningImpact.Operating;
  }

  internal static bool ApplyAutomaticClassification(
    TransactionEntity transaction,
    HashSet<int> internalTransfers)
  {
    if (transaction.PlanningClassificationConfirmed
      || transaction.PlanningImpact != TransactionPlanningImpact.Unclassified)
    {
      return false;
    }

    TransactionPlanningImpact impact = ResolveImpact(transaction, internalTransfers);
    transaction.PlanningImpact = impact;
    transaction.PlanningClassificationSource = impact == TransactionPlanningImpact.InternalTransfer
      ? PlanningClassificationSource.DetectedPattern
      : PlanningClassificationSource.DefaultRule;
    transaction.PlanningClassificationConfirmed = false;
    return impact != TransactionPlanningImpact.Unclassified;
  }

  private static bool HasMatchingContributionTransaction(
    AllocationHistoryEntity history,
    IReadOnlyList<TransactionEntity> transactions,
    HashSet<int> internalTransfers)
  {
    return transactions.Any(transaction =>
      transaction.AccountId == history.AccountId
      && Math.Abs((transaction.Date.Date - history.OccurredAt.Date).TotalDays) <= 2d
      && transaction.Value == Math.Abs(history.AmountBrl)
      && ResolveImpact(transaction, internalTransfers) == TransactionPlanningImpact.Contribution);
  }

  internal static HashSet<int> DetectInternalTransfers(List<TransactionEntity> transactions)
  {
    HashSet<int> result = new HashSet<int>();
    foreach (IGrouping<decimal, TransactionEntity> amountGroup in transactions.GroupBy(item => item.Value))
    {
      List<TransactionEntity> items = amountGroup.OrderBy(item => item.Date).ToList();
      for (int index = 0; index < items.Count; index++)
      {
        TransactionEntity current = items[index];
        TransactionEntity? match = items.FirstOrDefault(candidate =>
          candidate.Id != current.Id
          && candidate.AccountId != current.AccountId
          && candidate.Income != current.Income
          && Math.Abs((candidate.Date.Date - current.Date.Date).TotalDays) <= 2d);
        if (match != null)
        {
          result.Add(current.Id);
          result.Add(match.Id);
        }
      }
    }

    return result;
  }

  private static bool IsEssential(Category category)
  {
    return category is Category.Home
      or Category.Light
      or Category.Food
      or Category.Internet
      or Category.Health
      or Category.Vehicles
      or Category.School;
  }

  internal static decimal Median(IEnumerable<decimal> source)
  {
    List<decimal> values = source.OrderBy(value => value).ToList();
    if (values.Count == 0)
    {
      return 0m;
    }

    int middle = values.Count / 2;
    return values.Count % 2 == 0
      ? (values[middle - 1] + values[middle]) / 2m
      : values[middle];
  }

  private sealed class CachedFinancialFacts
  {
    public CachedFinancialFacts(FinancialFacts facts, DateTime cachedAtUtc)
    {
      this.Facts = facts;
      this.CachedAtUtc = cachedAtUtc;
    }

    public DateTime CachedAtUtc { get; }

    public FinancialFacts Facts { get; }
  }

  private readonly record struct FinancialFactsCacheKey(
    string UserId,
    DateOnly ReferenceDate,
    int Version);
}
