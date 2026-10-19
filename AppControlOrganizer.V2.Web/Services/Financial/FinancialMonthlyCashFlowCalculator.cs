//-----------------------------------------------------------------------
// <copyright file="FinancialMonthlyCashFlowCalculator.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Calculates monthly operating cash flow and investment contributions from financial facts sources.
/// </summary>
internal static class FinancialMonthlyCashFlowCalculator
{
  /// <summary>
  /// Calculates the month result from operating transactions and positive long-term investment contributions.
  /// </summary>
  /// <param name="transactions">Transactions available for transfer detection and monthly filtering.</param>
  /// <param name="allocationHistories">Allocation history rows available for monthly contribution detection.</param>
  /// <param name="referenceDate">Any date in the month to calculate.</param>
  /// <returns>The calculated monthly cash-flow result.</returns>
  internal static FinancialMonthlyCashFlow Calculate(
    IReadOnlyList<TransactionEntity> transactions,
    IReadOnlyList<AllocationHistoryEntity> allocationHistories,
    DateOnly referenceDate)
  {
    HashSet<int> internalTransfers = FinancialFactsService.DetectInternalTransfers(transactions.ToList());
    DateTime monthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
    DateTime monthEnd = monthStart.AddMonths(1);
    List<TransactionEntity> currentMonth = transactions
      .Where(item => item.Date >= monthStart && item.Date < monthEnd)
      .ToList();
    List<TransactionEntity> operatingIncome = currentMonth
      .Where(item => item.Income
        && FinancialFactsService.ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Operating)
      .ToList();
    List<TransactionEntity> operatingExpense = currentMonth
      .Where(item => !item.Income
        && FinancialFactsService.ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Operating)
      .ToList();
    List<TransactionEntity> contributionTransactions = currentMonth
      .Where(item => FinancialFactsService.ResolveImpact(item, internalTransfers) == TransactionPlanningImpact.Contribution)
      .ToList();
    List<AllocationHistoryEntity> positiveContributionHistories = allocationHistories
      .Where(item => item.OccurredAt >= monthStart
        && item.OccurredAt < monthEnd
        && item.AmountBrl > 0m
        && item.PlanningPurpose == FinancialPurpose.LongTermInvestment
        && !HasMatchingContributionTransaction(item, contributionTransactions, internalTransfers))
      .ToList();

    decimal monthlyOperatingIncome = operatingIncome.Sum(item => item.Value);
    decimal monthlyOperatingExpense = operatingExpense.Sum(item => item.Value);
    decimal monthlyContributions = contributionTransactions.Sum(item => item.Value)
      + positiveContributionHistories.Sum(item => item.AmountBrl);

    return new FinancialMonthlyCashFlow
    {
      OperatingIncome = monthlyOperatingIncome,
      OperatingExpense = monthlyOperatingExpense,
      Contributions = monthlyContributions,
      ResultWithContributions = monthlyOperatingIncome + monthlyContributions - monthlyOperatingExpense,
      OperatingIncomeCount = operatingIncome.Count,
      OperatingExpenseCount = operatingExpense.Count,
      ContributionTransactions = contributionTransactions,
      PositiveContributionHistories = positiveContributionHistories,
    };
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
      && FinancialFactsService.ResolveImpact(transaction, internalTransfers) == TransactionPlanningImpact.Contribution);
  }
}

/// <summary>
/// Monthly operating cash flow and investment contribution result.
/// </summary>
internal sealed class FinancialMonthlyCashFlow
{
  /// <summary>Gets or sets the operating income total.</summary>
  public decimal OperatingIncome { get; set; }

  /// <summary>Gets or sets the operating expense total.</summary>
  public decimal OperatingExpense { get; set; }

  /// <summary>Gets or sets the positive investment contribution total.</summary>
  public decimal Contributions { get; set; }

  /// <summary>Gets or sets the monthly result including contributions.</summary>
  public decimal ResultWithContributions { get; set; }

  /// <summary>Gets or sets the number of operating income transactions.</summary>
  public int OperatingIncomeCount { get; set; }

  /// <summary>Gets or sets the number of operating expense transactions.</summary>
  public int OperatingExpenseCount { get; set; }

  /// <summary>Gets or sets contribution transactions included in the contribution total.</summary>
  public List<TransactionEntity> ContributionTransactions { get; set; } = new List<TransactionEntity>();

  /// <summary>Gets or sets positive long-term allocation histories included in the contribution total.</summary>
  public List<AllocationHistoryEntity> PositiveContributionHistories { get; set; } = new List<AllocationHistoryEntity>();
}
