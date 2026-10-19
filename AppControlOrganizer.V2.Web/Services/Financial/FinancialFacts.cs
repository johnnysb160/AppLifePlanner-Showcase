//-----------------------------------------------------------------------
// <copyright file="FinancialFacts.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Contains the canonical financial facts and planning projections for one user and date.
/// </summary>
public sealed class FinancialFacts
{
  /// <summary>Gets or sets the local reference date.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets the UTC calculation instant.</summary>
  public DateTime CalculatedAtUtc { get; set; }

  /// <summary>Gets or sets canonical net worth.</summary>
  public decimal NetWorth { get; set; }

  /// <summary>Gets or sets account cash included in canonical net worth.</summary>
  public decimal AccountCash { get; set; }

  /// <summary>Gets or sets allocation current value included in canonical net worth.</summary>
  public decimal AllocationCurrentValue { get; set; }

  /// <summary>Gets or sets allocation applied value included in canonical net worth.</summary>
  public decimal AllocationAppliedValue { get; set; }

  /// <summary>Gets or sets allocation current value excluded from canonical net worth.</summary>
  public decimal ExcludedAllocationValue { get; set; }

  /// <summary>Gets or sets saved account balance ignored to avoid duplicated positions.</summary>
  public decimal SkippedDuplicateAccountBalance { get; set; }

  /// <summary>Gets or sets provider balance observed outside canonical position recalculation.</summary>
  public decimal ProviderAccountBalance { get; set; }

  /// <summary>Gets or sets the amount supported by current values or fresh quotes.</summary>
  public decimal ConfirmedValue { get; set; }

  /// <summary>Gets or sets the amount supported by purchase-value fallbacks.</summary>
  public decimal EstimatedValue { get; set; }

  /// <summary>Gets or sets operating cash.</summary>
  public decimal OperatingCash { get; set; }

  /// <summary>Gets or sets emergency-reserve value.</summary>
  public decimal EmergencyReserve { get; set; }

  /// <summary>Gets or sets the emergency-reserve value coming from allocations.</summary>
  public decimal EmergencyReserveAllocationValue { get; set; }

  /// <summary>Gets or sets the number of allocations classified as emergency reserve.</summary>
  public int EmergencyReserveAllocationCount { get; set; }

  /// <summary>Gets or sets allocations that compose the emergency reserve.</summary>
  public List<FinancialReserveAllocationDetail> EmergencyReserveAllocations { get; set; } = new List<FinancialReserveAllocationDetail>();

  /// <summary>Gets or sets idle-cash value.</summary>
  public decimal IdleCash { get; set; }

  /// <summary>Gets or sets long-term invested value.</summary>
  public decimal LongTermInvestments { get; set; }

  /// <summary>Gets or sets current-month operating income.</summary>
  public decimal MonthlyOperatingIncome { get; set; }

  /// <summary>Gets or sets current-month operating expenses.</summary>
  public decimal MonthlyOperatingExpense { get; set; }

  /// <summary>Gets or sets current-month contributions.</summary>
  public decimal MonthlyContributions { get; set; }

  /// <summary>Gets or sets year-to-date long-term investment contributions.</summary>
  public decimal YearToDateContributions { get; set; }

  /// <summary>Gets or sets the number of months represented by the year-to-date contribution total.</summary>
  public int YearToDateContributionMonths { get; set; }

  /// <summary>Gets or sets the recommended monthly-income assumption derived from history.</summary>
  public decimal SuggestedMonthlyIncome { get; set; }

  /// <summary>Gets or sets the recommended monthly-spending assumption derived from history.</summary>
  public decimal SuggestedMonthlyExpense { get; set; }

  /// <summary>Gets or sets the recommended essential monthly-spending baseline.</summary>
  public decimal SuggestedEssentialExpense { get; set; }

  /// <summary>Gets or sets the planned effective monthly contribution.</summary>
  public decimal EffectivePlannedContribution { get; set; }

  /// <summary>Gets or sets the projected remaining contribution total.</summary>
  public decimal ProjectedRemainingContributions { get; set; }

  /// <summary>Gets or sets the projected remaining allocation appreciation.</summary>
  public decimal ProjectedRemainingAssetAppreciation { get; set; }

  /// <summary>Gets or sets planned one-off flows included in the year-end projection.</summary>
  public decimal ProjectedOneOffFlows { get; set; }

  /// <summary>Gets or sets the projected year-end net worth.</summary>
  public decimal PlannedYearEndNetWorth { get; set; }

  /// <summary>Gets or sets the monthly contribution required to reach the annual target.</summary>
  public decimal RequiredMonthlyContribution { get; set; }

  /// <summary>Gets or sets the target emergency-reserve value.</summary>
  public decimal EmergencyReserveTarget { get; set; }

  /// <summary>Gets or sets idle cash as a percentage of net worth.</summary>
  public decimal IdleCashPercentage { get; set; }

  /// <summary>Gets or sets the number of unpriced positions.</summary>
  public int UnpricedPositionCount { get; set; }

  /// <summary>Gets or sets the number of automatically detected recurring patterns.</summary>
  public int RecurringPatternCount { get; set; }

  /// <summary>Gets or sets the latest available USD/BRL rate.</summary>
  public decimal? UsdBrlRate { get; set; }

  /// <summary>Gets or sets when the latest available USD/BRL rate was retrieved.</summary>
  public DateTime? UsdBrlRetrievedAtUtc { get; set; }

  /// <summary>Gets or sets concise financial signals.</summary>
  public List<FinancialSignal> Signals { get; set; } = new List<FinancialSignal>();

  /// <summary>Gets or sets canonical account-position composition.</summary>
  public List<FinancialAccountPosition> AccountPositions { get; set; } = new List<FinancialAccountPosition>();
}

/// <summary>
/// Describes one allocation included in the emergency reserve.
/// </summary>
public sealed class FinancialReserveAllocationDetail
{
  /// <summary>Gets or sets the allocation identifier.</summary>
  public int AllocationId { get; set; }

  /// <summary>Gets or sets the allocation name.</summary>
  public string AllocationName { get; set; } = string.Empty;

  /// <summary>Gets or sets the owning account name.</summary>
  public string AccountName { get; set; } = string.Empty;

  /// <summary>Gets or sets the current BRL value.</summary>
  public decimal CurrentAmountBrl { get; set; }

  /// <summary>Gets or sets the localized source key.</summary>
  public string SourceKey { get; set; } = string.Empty;
}
