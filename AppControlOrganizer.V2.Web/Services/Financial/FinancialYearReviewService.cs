//-----------------------------------------------------------------------
// <copyright file="FinancialYearReviewService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Builds read-only annual financial reviews from existing finance facts and transactions.
/// </summary>
public sealed class FinancialYearReviewService
{
  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly IFinancialFactsService financialFactsService;

  /// <summary>
  /// Initializes a new instance of the <see cref="FinancialYearReviewService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database-context factory.</param>
  /// <param name="financialFactsService">Canonical financial facts service.</param>
  public FinancialYearReviewService(
    IDbContextFactory<LifePlannerContext> contextFactory,
    IFinancialFactsService financialFactsService)
  {
    this.contextFactory = contextFactory;
    this.financialFactsService = financialFactsService;
  }

  /// <summary>
  /// Builds the annual financial review for a user and year.
  /// </summary>
  /// <param name="userId">The owner identifier.</param>
  /// <param name="year">The calendar year.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The assembled annual review.</returns>
  public async Task<FinancialYearReview> GetAsync(
    string userId,
    int year,
    CancellationToken cancellationToken = default)
  {
    DateOnly referenceDate = GetReferenceDate(year);
    Task<FinancialFacts> factsTask = this.financialFactsService.GetWithAnnualPlanAsync(
      userId,
      referenceDate,
      cancellationToken);
    Task<List<TransactionEntity>> transactionsTask = this.LoadTransactionsAsync(userId, year, cancellationToken);
    Task<List<AllocationHistoryEntity>> historiesTask = this.LoadAllocationHistoriesAsync(userId, year, cancellationToken);
    Task<List<AllocationEntity>> allocationsTask = this.LoadAllocationsAsync(userId, cancellationToken);
    Task<List<int>> yearsTask = this.LoadAvailableYearsAsync(userId, year, cancellationToken);

    await Task.WhenAll(factsTask, transactionsTask, historiesTask, allocationsTask, yearsTask);

    FinancialFacts facts = await factsTask;
    List<TransactionEntity> transactions = await transactionsTask;
    List<AllocationHistoryEntity> histories = await historiesTask;
    List<AllocationEntity> allocations = await allocationsTask;
    List<FinancialYearReviewMonth> months = BuildMonths(year, transactions, histories);
    FinancialYearReview review = new FinancialYearReview
    {
      Year = year,
      ReferenceDate = referenceDate,
      CalculatedAtUtc = DateTime.UtcNow,
      AvailableYears = await yearsTask,
      HasYearActivity = transactions.Count > 0 || histories.Count > 0,
      TransactionCount = transactions.Count,
      Months = months,
      AnnualOperatingIncome = months.Sum(item => item.OperatingIncome),
      AnnualOperatingExpense = months.Sum(item => item.OperatingExpense),
      AnnualContributions = months.Sum(item => item.Contributions),
      AnnualResult = months.Sum(item => item.Result),
      NetWorth = facts.NetWorth,
      AccountCash = facts.AccountCash,
      EmergencyReserve = facts.EmergencyReserve,
      IdleCash = facts.IdleCash,
      LongTermInvestments = facts.LongTermInvestments,
      InvestmentAppliedValue = facts.AllocationAppliedValue,
      InvestmentCurrentValue = facts.AllocationCurrentValue,
      InvestmentProfit = facts.AllocationCurrentValue - facts.AllocationAppliedValue,
      InvestmentExits = histories
        .Where(item => item.AmountBrl < 0m && item.PlanningPurpose == FinancialPurpose.LongTermInvestment)
        .Sum(item => Math.Abs(item.AmountBrl)),
      UnpricedPositionCount = facts.UnpricedPositionCount,
    };

    review.AverageMonthlyIncome = review.AnnualOperatingIncome / 12m;
    review.AverageMonthlyExpense = review.AnnualOperatingExpense / 12m;
    review.SavingsRate = CalculateRate(review.AnnualContributions, review.AnnualOperatingIncome);
    review.EstimatedStartNetWorth = review.NetWorth - review.AnnualResult;
    review.NetWorthChange = review.AnnualResult;
    review.NetWorthChangePercentage = CalculateRate(review.NetWorthChange, Math.Abs(review.EstimatedStartNetWorth));
    review.InvestmentReturnPercentage = CalculateRate(review.InvestmentProfit, review.InvestmentAppliedValue);
    review.TopExpenseCategories = BuildTopExpenseCategories(transactions, review.AnnualOperatingExpense);
    review.InvestmentPositions = BuildInvestmentPositions(allocations);
    review.AnalysisKeys = BuildAnalysisKeys(review);
    review.AnalystInsights = BuildAnalystInsights(review);

    return review;
  }

  private static DateOnly GetReferenceDate(int year)
  {
    DateOnly today = DateOnly.FromDateTime(DateTime.Now);
    if (year == today.Year)
    {
      return today;
    }

    return new DateOnly(year, 12, 31);
  }

  private async Task<List<TransactionEntity>> LoadTransactionsAsync(
    string userId,
    int year,
    CancellationToken cancellationToken)
  {
    DateTime start = new DateTime(year, 1, 1);
    DateTime end = start.AddYears(1);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Transactions
      .AsNoTracking()
      .Include(transaction => transaction.CategoryEntity)
      .Where(transaction => EF.Property<string>(transaction, "OwnerUserId") == userId
        && !transaction.Deleted
        && transaction.Date >= start
        && transaction.Date < end)
      .OrderBy(transaction => transaction.Date)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<AllocationHistoryEntity>> LoadAllocationHistoriesAsync(
    string userId,
    int year,
    CancellationToken cancellationToken)
  {
    DateTime start = new DateTime(year, 1, 1);
    DateTime end = start.AddYears(1);
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.AllocationHistories
      .AsNoTracking()
      .Where(history => history.OwnerUserId == userId
        && !history.Deleted
        && history.OccurredAt >= start
        && history.OccurredAt < end)
      .OrderBy(history => history.OccurredAt)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<AllocationEntity>> LoadAllocationsAsync(
    string userId,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    return await context.Allocations
      .AsNoTracking()
      .Where(allocation => EF.Property<string>(allocation, "OwnerUserId") == userId && !allocation.Deleted)
      .OrderBy(allocation => allocation.Name)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<int>> LoadAvailableYearsAsync(
    string userId,
    int selectedYear,
    CancellationToken cancellationToken)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<int> transactionYears = await context.Transactions
      .AsNoTracking()
      .Where(transaction => EF.Property<string>(transaction, "OwnerUserId") == userId && !transaction.Deleted)
      .Select(transaction => transaction.Date.Year)
      .Distinct()
      .ToListAsync(cancellationToken);
    List<int> historyYears = await context.AllocationHistories
      .AsNoTracking()
      .Where(history => history.OwnerUserId == userId && !history.Deleted)
      .Select(history => history.OccurredAt.Year)
      .Distinct()
      .ToListAsync(cancellationToken);

    return transactionYears
      .Concat(historyYears)
      .Append(selectedYear)
      .Append(DateTime.Now.Year)
      .Distinct()
      .OrderByDescending(item => item)
      .ToList();
  }

  private static List<FinancialYearReviewMonth> BuildMonths(
    int year,
    IReadOnlyList<TransactionEntity> transactions,
    IReadOnlyList<AllocationHistoryEntity> histories)
  {
    List<FinancialYearReviewMonth> months = new List<FinancialYearReviewMonth>();
    for (int month = 1; month <= 12; month++)
    {
      DateTime start = new DateTime(year, month, 1);
      DateTime end = start.AddMonths(1);
      List<TransactionEntity> monthTransactions = transactions
        .Where(transaction => transaction.Date >= start && transaction.Date < end)
        .ToList();
      List<AllocationHistoryEntity> monthHistories = histories
        .Where(history => history.OccurredAt >= start && history.OccurredAt < end)
        .ToList();
      FinancialMonthlyCashFlow cashFlow = FinancialMonthlyCashFlowCalculator.Calculate(
        monthTransactions,
        monthHistories,
        new DateOnly(year, month, 1));
      months.Add(new FinancialYearReviewMonth
      {
        Month = month,
        OperatingIncome = cashFlow.OperatingIncome,
        OperatingExpense = cashFlow.OperatingExpense,
        Contributions = cashFlow.Contributions,
        Result = cashFlow.ResultWithContributions,
        TransactionCount = monthTransactions.Count,
        SavingsRate = CalculateRate(cashFlow.Contributions, cashFlow.OperatingIncome),
      });
    }

    return months;
  }

  private static List<FinancialYearReviewCategory> BuildTopExpenseCategories(
    IReadOnlyList<TransactionEntity> transactions,
    decimal annualExpense)
  {
    HashSet<int> internalTransfers = FinancialFactsService.DetectInternalTransfers(transactions.ToList());
    return transactions
      .Where(transaction => !transaction.Income
        && FinancialFactsService.ResolveImpact(transaction, internalTransfers) == TransactionPlanningImpact.Operating)
      .GroupBy(transaction => new
      {
        transaction.CategoryId,
        LegacyCategory = transaction.CategoryId.HasValue ? (Category?)null : transaction.Category,
        NameKey = transaction.CategoryEntity?.Name ?? transaction.Category.GetDisplayName(),
      })
      .Select(group => new FinancialYearReviewCategory
      {
        CategoryId = group.Key.CategoryId,
        LegacyCategory = group.Key.LegacyCategory,
        NameKey = group.Key.NameKey,
        Total = group.Sum(transaction => transaction.Value),
        Count = group.Count(),
        Percentage = annualExpense > 0m ? group.Sum(transaction => transaction.Value) / annualExpense * 100m : 0m,
      })
      .OrderByDescending(item => item.Total)
      .ThenBy(item => item.NameKey, StringComparer.Ordinal)
      .Take(6)
      .ToList();
  }

  private static List<FinancialYearReviewInvestmentPosition> BuildInvestmentPositions(
    IReadOnlyList<AllocationEntity> allocations)
  {
    return allocations
      .Where(allocation => allocation.Active && allocation.PlanningPurpose != FinancialPurpose.Excluded)
      .Select(allocation =>
      {
        decimal applied = FinancialPositionCalculator.CalculateAppliedNativeValue(allocation);
        decimal current = FinancialPositionCalculator.CalculateCurrentNativeValue(allocation);
        decimal profit = current - applied;
        return new FinancialYearReviewInvestmentPosition
        {
          AllocationId = allocation.Id,
          Name = allocation.Name,
          Symbol = string.IsNullOrWhiteSpace(allocation.Initials) ? allocation.AllocationType.ToString() : allocation.Initials,
          Type = allocation.AllocationType.GetDisplayName(),
          Currency = allocation.Currency.GetDisplayName(),
          AppliedValue = applied,
          CurrentValue = current,
          Profit = profit,
          ReturnPercentage = CalculateRate(profit, applied),
          HasCurrentPrice = allocation.CurrentUnitValue.HasValue || allocation.AllocationType == AllocationType.Stopped,
        };
      })
      .OrderByDescending(position => Math.Abs(position.CurrentValue))
      .Take(8)
      .ToList();
  }

  private static List<string> BuildAnalysisKeys(FinancialYearReview review)
  {
    List<string> keys = new List<string>();
    keys.Add(review.AnnualResult >= 0m
      ? "YearReview.Analysis.PositiveCashFlow"
      : "YearReview.Analysis.NegativeCashFlow");
    keys.Add(review.SavingsRate >= 20m
      ? "YearReview.Analysis.StrongSavings"
      : "YearReview.Analysis.LowSavings");
    keys.Add(review.TopExpenseCategories.Count > 0 && review.TopExpenseCategories[0].Percentage >= 35m
      ? "YearReview.Analysis.ConcentratedExpenses"
      : "YearReview.Analysis.DistributedExpenses");
    keys.Add(review.InvestmentProfit >= 0m
      ? "YearReview.Analysis.PositiveInvestments"
      : "YearReview.Analysis.NegativeInvestments");
    keys.Add(review.UnpricedPositionCount > 0
      ? "YearReview.Analysis.UnpricedPositions"
      : "YearReview.Analysis.PricedPositions");

    return keys;
  }

  private static List<FinancialYearReviewInsight> BuildAnalystInsights(FinancialYearReview review)
  {
    List<FinancialYearReviewInsight> insights = new List<FinancialYearReviewInsight>();
    insights.Add(new FinancialYearReviewInsight
    {
      Tone = review.AnnualResult >= 0m ? "positive" : "negative",
      IconCss = review.AnnualResult >= 0m ? "bi bi-graph-up-arrow" : "bi bi-graph-down-arrow",
      TitleKey = review.AnnualResult >= 0m
        ? "YearReview.Insight.CashFlow.Positive.Title"
        : "YearReview.Insight.CashFlow.Negative.Title",
      BodyKey = review.AnnualResult >= 0m
        ? "YearReview.Insight.CashFlow.Positive.Body"
        : "YearReview.Insight.CashFlow.Negative.Body",
    });

    decimal topExpenseShare = review.TopExpenseCategories.Count == 0 ? 0m : review.TopExpenseCategories[0].Percentage;
    insights.Add(new FinancialYearReviewInsight
    {
      Tone = topExpenseShare >= 35m ? "warning" : "neutral",
      IconCss = topExpenseShare >= 35m ? "bi bi-bullseye" : "bi bi-diagram-3",
      TitleKey = topExpenseShare >= 35m
        ? "YearReview.Insight.ExpenseConcentration.Title"
        : "YearReview.Insight.ExpenseDistribution.Title",
      BodyKey = topExpenseShare >= 35m
        ? "YearReview.Insight.ExpenseConcentration.Body"
        : "YearReview.Insight.ExpenseDistribution.Body",
    });

    insights.Add(new FinancialYearReviewInsight
    {
      Tone = review.SavingsRate >= 20m ? "positive" : "warning",
      IconCss = "bi bi-piggy-bank",
      TitleKey = review.SavingsRate >= 20m
        ? "YearReview.Insight.Savings.Strong.Title"
        : "YearReview.Insight.Savings.Low.Title",
      BodyKey = review.SavingsRate >= 20m
        ? "YearReview.Insight.Savings.Strong.Body"
        : "YearReview.Insight.Savings.Low.Body",
    });

    insights.Add(new FinancialYearReviewInsight
    {
      Tone = review.InvestmentProfit >= 0m ? "positive" : "negative",
      IconCss = review.InvestmentProfit >= 0m ? "bi bi-arrow-up-right-circle" : "bi bi-arrow-down-right-circle",
      TitleKey = review.InvestmentProfit >= 0m
        ? "YearReview.Insight.Investments.Positive.Title"
        : "YearReview.Insight.Investments.Negative.Title",
      BodyKey = review.InvestmentProfit >= 0m
        ? "YearReview.Insight.Investments.Positive.Body"
        : "YearReview.Insight.Investments.Negative.Body",
    });

    insights.Add(new FinancialYearReviewInsight
    {
      Tone = review.UnpricedPositionCount > 0 ? "warning" : "positive",
      IconCss = review.UnpricedPositionCount > 0 ? "bi bi-exclamation-circle" : "bi bi-check-circle",
      TitleKey = review.UnpricedPositionCount > 0
        ? "YearReview.Insight.DataQuality.Warning.Title"
        : "YearReview.Insight.DataQuality.Good.Title",
      BodyKey = review.UnpricedPositionCount > 0
        ? "YearReview.Insight.DataQuality.Warning.Body"
        : "YearReview.Insight.DataQuality.Good.Body",
    });

    return insights;
  }

  private static decimal CalculateRate(decimal numerator, decimal denominator)
  {
    if (denominator == 0m)
    {
      return 0m;
    }

    return numerator / denominator * 100m;
  }
}
