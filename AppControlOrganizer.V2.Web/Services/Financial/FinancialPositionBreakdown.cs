//-----------------------------------------------------------------------
// <copyright file="FinancialPositionBreakdown.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Canonical breakdown of the current financial position.
/// </summary>
public sealed class FinancialPositionBreakdown
{
  /// <summary>Gets or sets ledger/provider cash included in net worth.</summary>
  public decimal AccountCash { get; set; }

  /// <summary>Gets or sets included allocation current value in BRL.</summary>
  public decimal AllocationCurrentValue { get; set; }

  /// <summary>Gets or sets included allocation applied value in BRL.</summary>
  public decimal AllocationAppliedValue { get; set; }

  /// <summary>Gets or sets allocation current value excluded from planning net worth.</summary>
  public decimal ExcludedAllocationValue { get; set; }

  /// <summary>Gets or sets saved account balance ignored to avoid duplicated positions.</summary>
  public decimal SkippedDuplicateAccountBalance { get; set; }

  /// <summary>Gets or sets provider account balance observed but not recalculated by canonical rules.</summary>
  public decimal ProviderAccountBalance { get; set; }

  /// <summary>Gets or sets canonical net worth.</summary>
  public decimal NetWorth { get; set; }

  /// <summary>Gets or sets amount supported by current values, fresh quotes, or ledger cash.</summary>
  public decimal ConfirmedValue { get; set; }

  /// <summary>Gets or sets amount supported by purchase-value fallbacks.</summary>
  public decimal EstimatedValue { get; set; }

  /// <summary>Gets or sets operating cash.</summary>
  public decimal OperatingCash { get; set; }

  /// <summary>Gets or sets emergency reserve value.</summary>
  public decimal EmergencyReserve { get; set; }

  /// <summary>Gets or sets idle cash value.</summary>
  public decimal IdleCash { get; set; }

  /// <summary>Gets or sets long-term investment value.</summary>
  public decimal LongTermInvestments { get; set; }

  /// <summary>Gets or sets the count of positions that could not be converted to BRL.</summary>
  public int UnpricedPositionCount { get; set; }

  /// <summary>Gets or sets allocation positions used to build the breakdown.</summary>
  public List<FinancialAllocationPosition> AllocationPositions { get; set; } = new List<FinancialAllocationPosition>();

  /// <summary>Gets or sets account positions used to build the breakdown.</summary>
  public List<FinancialAccountPosition> AccountPositions { get; set; } = new List<FinancialAccountPosition>();
}

/// <summary>
/// Canonical current/applied BRL values for one allocation.
/// </summary>
public sealed class FinancialAllocationPosition
{
  /// <summary>Gets or sets the allocation identifier.</summary>
  public int AllocationId { get; set; }

  /// <summary>Gets or sets the owning account identifier.</summary>
  public int AccountId { get; set; }

  /// <summary>Gets or sets the resolved planning purpose.</summary>
  public FinancialPurpose Purpose { get; set; }

  /// <summary>Gets or sets the current amount in the allocation currency.</summary>
  public decimal CurrentNativeAmount { get; set; }

  /// <summary>Gets or sets the applied amount in the allocation currency.</summary>
  public decimal AppliedNativeAmount { get; set; }

  /// <summary>Gets or sets the current amount converted to BRL.</summary>
  public decimal CurrentAmountBrl { get; set; }

  /// <summary>Gets or sets the applied amount converted to BRL.</summary>
  public decimal AppliedAmountBrl { get; set; }

  /// <summary>Gets or sets a value indicating whether the current amount is convertible to BRL.</summary>
  public bool CurrentIsConvertible { get; set; }

  /// <summary>Gets or sets a value indicating whether the applied amount is convertible to BRL.</summary>
  public bool AppliedIsConvertible { get; set; }

  /// <summary>Gets or sets a value indicating whether a live quote participated in pricing.</summary>
  public bool UsedQuote { get; set; }

  /// <summary>Gets or sets a value indicating whether pricing used a fallback value.</summary>
  public bool UsedFallback { get; set; }

  /// <summary>Gets or sets the note returned by pricing or conversion.</summary>
  public string? Note { get; set; }
}

/// <summary>
/// Canonical current-position contribution for one account.
/// </summary>
public sealed class FinancialAccountPosition
{
  /// <summary>Gets or sets the account identifier.</summary>
  public int AccountId { get; set; }

  /// <summary>Gets or sets account cash included in net worth.</summary>
  public decimal IncludedCash { get; set; }

  /// <summary>Gets or sets included allocation current value in BRL.</summary>
  public decimal AllocationCurrentValue { get; set; }

  /// <summary>Gets or sets included allocation applied value in BRL.</summary>
  public decimal AllocationAppliedValue { get; set; }

  /// <summary>Gets or sets saved account balance ignored to avoid duplicated positions.</summary>
  public decimal SkippedDuplicateBalance { get; set; }

  /// <summary>Gets or sets provider balance observed but not included in canonical net worth.</summary>
  public decimal ProviderBalance { get; set; }

  /// <summary>Gets the total canonical position for this account.</summary>
  public decimal TotalPosition
  {
    get
    {
      return this.IncludedCash + this.AllocationCurrentValue;
    }
  }
}

/// <summary>
/// Account aggregate values persisted by financial sync.
/// </summary>
public sealed class FinancialAccountOperationalPosition
{
  /// <summary>Gets or sets the account balance value to persist when the account is not provider-based.</summary>
  public decimal Balance { get; set; }

  /// <summary>Gets or sets the active invested amount to persist.</summary>
  public decimal TotalInvested { get; set; }
}
