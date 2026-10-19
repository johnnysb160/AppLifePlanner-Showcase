//-----------------------------------------------------------------------
// <copyright file="FinancialPositionCalculator.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Services;

/// <summary>
/// Centralizes shared financial position formulas used by account sync and planning facts.
/// </summary>
public static class FinancialPositionCalculator
{
  /// <summary>
  /// Calculates the complete canonical position breakdown for accounts, allocations, and transactions.
  /// </summary>
  /// <param name="accounts">Accounts owned by the user.</param>
  /// <param name="allocations">Allocations owned by the user.</param>
  /// <param name="transactions">Transactions owned by the user.</param>
  /// <param name="quotes">Latest successful market quotes.</param>
  /// <param name="pricingService">Allocation pricing service.</param>
  /// <param name="purposeResolver">Function that resolves an allocation planning purpose.</param>
  /// <returns>The canonical financial-position breakdown.</returns>
  public static FinancialPositionBreakdown CalculateBreakdown(
    IEnumerable<AccountEntity> accounts,
    IEnumerable<AllocationEntity> allocations,
    IEnumerable<TransactionEntity> transactions,
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    AllocationQuotePricingService pricingService,
    Func<AllocationEntity, FinancialPurpose> purposeResolver)
  {
    List<AccountEntity> activeAccounts = accounts.Where(account => !account.Deleted).ToList();
    List<AllocationEntity> activeAllocations = allocations.Where(allocation => !allocation.Deleted).ToList();
    List<TransactionEntity> activeTransactions = transactions.Where(transaction => !transaction.Deleted).ToList();
    FinancialPositionBreakdown breakdown = new FinancialPositionBreakdown();
    Dictionary<int, FinancialAccountPosition> accountPositions = activeAccounts
      .ToDictionary(
        account => account.Id,
        account => new FinancialAccountPosition { AccountId = account.Id });

    foreach (AllocationEntity allocation in activeAllocations)
    {
      FinancialAllocationPosition position = CalculateAllocationPosition(
        allocation,
        quotes,
        pricingService,
        purposeResolver);
      breakdown.AllocationPositions.Add(position);

      if (!position.CurrentIsConvertible)
      {
        breakdown.UnpricedPositionCount++;
        continue;
      }

      FinancialAccountPosition accountPosition = GetOrCreateAccountPosition(
        accountPositions,
        allocation.AccountId);
      if (position.Purpose == FinancialPurpose.Excluded)
      {
        breakdown.ExcludedAllocationValue += position.CurrentAmountBrl;
        continue;
      }

      breakdown.AllocationCurrentValue += position.CurrentAmountBrl;
      if (position.AppliedIsConvertible)
      {
        breakdown.AllocationAppliedValue += position.AppliedAmountBrl;
        accountPosition.AllocationAppliedValue += position.AppliedAmountBrl;
      }

      breakdown.NetWorth += position.CurrentAmountBrl;
      accountPosition.AllocationCurrentValue += position.CurrentAmountBrl;
      if (position.UsedQuote || !position.UsedFallback || allocation.CurrentUnitValue.HasValue || allocation.AllocationType == AllocationType.Stopped)
      {
        breakdown.ConfirmedValue += position.CurrentAmountBrl;
      }
      else
      {
        breakdown.EstimatedValue += position.CurrentAmountBrl;
      }

      AddByPurpose(breakdown, position.Purpose, position.CurrentAmountBrl);
    }

    HashSet<int> positionBasedAccountsWithPositions = activeAllocations
      .Where(allocation => allocation.AllocationType != AllocationType.Stopped)
      .Select(allocation => allocation.AccountId)
      .ToHashSet();

    foreach (AccountEntity account in activeAccounts)
    {
      FinancialAccountPosition accountPosition = GetOrCreateAccountPosition(accountPositions, account.Id);
      if (account.BalanceMode == AccountBalanceMode.ProviderBased)
      {
        accountPosition.ProviderBalance = account.Balance;
        breakdown.ProviderAccountBalance += account.Balance;
        continue;
      }

      if (account.BalanceMode == AccountBalanceMode.PositionBased
        && positionBasedAccountsWithPositions.Contains(account.Id))
      {
        decimal skippedBalance = account.Balance;
        accountPosition.SkippedDuplicateBalance = skippedBalance;
        breakdown.SkippedDuplicateAccountBalance += skippedBalance;
        continue;
      }

      decimal accountCash = account.BalanceMode == AccountBalanceMode.LedgerBased
        ? CalculateLedgerBalance(account, activeTransactions)
        : activeTransactions
          .Where(transaction => transaction.AccountId == account.Id)
          .Sum(GetSignedTransactionValue);
      accountPosition.IncludedCash = accountCash;
      breakdown.AccountCash += accountCash;
      breakdown.NetWorth += accountCash;
      breakdown.ConfirmedValue += accountCash;
      breakdown.OperatingCash += accountCash;
    }

    breakdown.AccountPositions = accountPositions.Values
      .OrderBy(position => position.AccountId)
      .ToList();

    return breakdown;
  }

  /// <summary>
  /// Calculates the canonical BRL amounts for one allocation.
  /// </summary>
  /// <param name="allocation">The allocation to price.</param>
  /// <param name="quotes">Latest successful market quotes.</param>
  /// <param name="pricingService">Allocation pricing service.</param>
  /// <param name="purposeResolver">Function that resolves an allocation planning purpose.</param>
  /// <returns>The priced allocation position.</returns>
  public static FinancialAllocationPosition CalculateAllocationPosition(
    AllocationEntity allocation,
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    AllocationQuotePricingService pricingService,
    Func<AllocationEntity, FinancialPurpose> purposeResolver)
  {
    AllocationQuotePriceResult pricing = pricingService.Calculate(allocation, quotes);
    decimal appliedNative = CalculateAppliedNativeValue(allocation);
    decimal currentNative = allocation.BuyUnitValue == 0m
      ? allocation.Quantity
      : pricing.UnitPriceInAllocationCurrency * allocation.Quantity;
    AllocationBrlConversionResult appliedBrl = pricingService.ConvertAmountToBrl(
      allocation,
      appliedNative,
      quotes);
    AllocationBrlConversionResult currentBrl = pricingService.ConvertAmountToBrl(
      allocation,
      currentNative,
      quotes);

    return new FinancialAllocationPosition
    {
      AllocationId = allocation.Id,
      AccountId = allocation.AccountId,
      Purpose = purposeResolver(allocation),
      AppliedNativeAmount = appliedNative,
      CurrentNativeAmount = currentNative,
      AppliedAmountBrl = appliedBrl.IsConvertible ? appliedBrl.AmountBrl : 0m,
      CurrentAmountBrl = currentBrl.IsConvertible ? currentBrl.AmountBrl : 0m,
      AppliedIsConvertible = appliedBrl.IsConvertible,
      CurrentIsConvertible = currentBrl.IsConvertible,
      UsedQuote = pricing.UsedQuote,
      UsedFallback = pricing.UsedFallback,
      Note = currentBrl.Note ?? pricing.Note ?? appliedBrl.Note,
    };
  }

  /// <summary>
  /// Calculates the account aggregates persisted by financial sync.
  /// </summary>
  /// <param name="account">The account to sync.</param>
  /// <param name="allocations">Allocations linked to the account.</param>
  /// <param name="transactions">Transactions linked to the account.</param>
  /// <param name="quotes">Latest successful market quotes.</param>
  /// <param name="pricingService">Allocation pricing service.</param>
  /// <param name="purposeResolver">Function that resolves an allocation planning purpose.</param>
  /// <returns>The operational account aggregate.</returns>
  public static FinancialAccountOperationalPosition CalculateOperationalAccountPosition(
    AccountEntity account,
    IEnumerable<AllocationEntity> allocations,
    IEnumerable<TransactionEntity> transactions,
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    AllocationQuotePricingService pricingService,
    Func<AllocationEntity, FinancialPurpose> purposeResolver)
  {
    List<AllocationEntity> activeAllocations = allocations.Where(allocation => !allocation.Deleted).ToList();
    List<TransactionEntity> activeTransactions = transactions.Where(transaction => !transaction.Deleted).ToList();
    decimal transactionBalance = account.BalanceMode == AccountBalanceMode.LedgerBased
      ? CalculateLedgerBalance(account, activeTransactions)
      : activeTransactions
        .Where(transaction => transaction.AccountId == account.Id)
        .Sum(GetSignedTransactionValue);
    decimal liquidBalance = account.BalanceMode == AccountBalanceMode.PositionBased
      ? activeAllocations
        .Where(allocation => allocation.AllocationType == AllocationType.Stopped)
        .Select(allocation => CalculateAllocationPosition(allocation, quotes, pricingService, purposeResolver))
        .Where(position => position.CurrentIsConvertible)
        .Sum(position => position.CurrentAmountBrl)
      : 0m;
    decimal activeInvestment = activeAllocations
      .Where(allocation => allocation.AllocationType != AllocationType.Stopped)
      .Select(allocation => CalculateAllocationPosition(allocation, quotes, pricingService, purposeResolver))
      .Where(position => position.CurrentIsConvertible && position.Purpose != FinancialPurpose.Excluded)
      .Sum(position => position.CurrentAmountBrl);

    return new FinancialAccountOperationalPosition
    {
      Balance = transactionBalance + liquidBalance,
      TotalInvested = activeInvestment,
    };
  }

  /// <summary>
  /// Calculates an allocation's applied native-currency value.
  /// </summary>
  /// <param name="allocation">The allocation to evaluate.</param>
  /// <returns>The applied native-currency value.</returns>
  public static decimal CalculateAppliedNativeValue(AllocationEntity allocation)
  {
    return allocation.BuyUnitValue == 0m
      ? allocation.Quantity
      : allocation.BuyUnitValue * allocation.Quantity;
  }

  /// <summary>
  /// Calculates a ledger balance from the account reconciliation anchor plus later transactions.
  /// </summary>
  /// <param name="account">The account whose reconciliation anchor is used.</param>
  /// <param name="transactions">Transactions linked to the account.</param>
  /// <returns>The signed ledger balance.</returns>
  public static decimal CalculateLedgerBalance(
    AccountEntity account,
    IEnumerable<TransactionEntity> transactions)
  {
    DateOnly startDate = account.OpeningBalanceDate ?? DateOnly.MinValue;
    decimal balance = account.OpeningBalance ?? 0m;
    balance += transactions
      .Where(item => item.AccountId == account.Id && DateOnly.FromDateTime(item.Date) > startDate)
      .Sum(GetSignedTransactionValue);

    return balance;
  }

  /// <summary>
  /// Calculates the signed value of a transaction.
  /// </summary>
  /// <param name="transaction">The transaction to evaluate.</param>
  /// <returns>A positive value for income and a negative value for expense.</returns>
  public static decimal GetSignedTransactionValue(TransactionEntity transaction)
  {
    return transaction.Income ? transaction.Value : -transaction.Value;
  }

  /// <summary>
  /// Calculates an allocation's current native-currency value from the current unit value when available.
  /// </summary>
  /// <param name="allocation">The allocation to evaluate.</param>
  /// <returns>The current native-currency value.</returns>
  public static decimal CalculateCurrentNativeValue(AllocationEntity allocation)
  {
    decimal unitValue = allocation.CurrentUnitValue ?? allocation.BuyUnitValue;
    return unitValue * allocation.Quantity;
  }

  private static FinancialAccountPosition GetOrCreateAccountPosition(
    Dictionary<int, FinancialAccountPosition> accountPositions,
    int accountId)
  {
    if (!accountPositions.TryGetValue(accountId, out FinancialAccountPosition? position))
    {
      position = new FinancialAccountPosition { AccountId = accountId };
      accountPositions[accountId] = position;
    }

    return position;
  }

  private static void AddByPurpose(
    FinancialPositionBreakdown breakdown,
    FinancialPurpose purpose,
    decimal amount)
  {
    switch (purpose)
    {
      case FinancialPurpose.OperatingCash:
        breakdown.OperatingCash += amount;
        break;
      case FinancialPurpose.EmergencyReserve:
        breakdown.EmergencyReserve += amount;
        break;
      case FinancialPurpose.IdleCash:
        breakdown.IdleCash += amount;
        break;
      default:
        breakdown.LongTermInvestments += amount;
        break;
    }
  }
}
