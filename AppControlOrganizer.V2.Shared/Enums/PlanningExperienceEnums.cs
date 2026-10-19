//-----------------------------------------------------------------------
// <copyright file="PlanningExperienceEnums.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines how an account contributes to the canonical financial position.
/// </summary>
public enum AccountBalanceMode
{
  /// <summary>Allocation positions are the current balance source.</summary>
  PositionBased = 0,

  /// <summary>An opening balance plus subsequent transactions is the current balance source.</summary>
  LedgerBased = 1,

  /// <summary>An external provider is the current balance source.</summary>
  ProviderBased = 2,
}

/// <summary>
/// Defines the planning purpose of an allocation.
/// </summary>
public enum FinancialPurpose
{
  /// <summary>The purpose has not been confirmed.</summary>
  Unclassified = 0,

  /// <summary>Money used for ordinary short-term cash flow.</summary>
  OperatingCash = 1,

  /// <summary>Money reserved for emergencies.</summary>
  EmergencyReserve = 2,

  /// <summary>Liquid money without a near-term purpose.</summary>
  IdleCash = 3,

  /// <summary>Long-term invested capital.</summary>
  LongTermInvestment = 4,

  /// <summary>A non-financial asset included in net worth.</summary>
  NonFinancialAsset = 5,

  /// <summary>The position is intentionally excluded from planning totals.</summary>
  Excluded = 6,
}

/// <summary>
/// Defines how a transaction affects planning calculations.
/// </summary>
public enum TransactionPlanningImpact
{
  /// <summary>The impact has not been explicitly classified.</summary>
  Unclassified = 0,

  /// <summary>The transaction is part of ordinary income or spending.</summary>
  Operating = 1,

  /// <summary>The transaction transfers money between owned accounts.</summary>
  InternalTransfer = 2,

  /// <summary>The transaction adds capital to investments.</summary>
  Contribution = 3,

  /// <summary>The transaction buys, sells, or moves an asset.</summary>
  AssetMovement = 4,

  /// <summary>The transaction is exceptional and should not shape the recurring baseline.</summary>
  Extraordinary = 5,

  /// <summary>The transaction is intentionally ignored by planning calculations.</summary>
  Ignored = 6,
}

/// <summary>
/// Identifies where a transaction classification came from.
/// </summary>
public enum PlanningClassificationSource
{
  /// <summary>No classification source exists.</summary>
  None = 0,

  /// <summary>A safe built-in rule supplied the classification.</summary>
  DefaultRule = 1,

  /// <summary>A detected pattern supplied the classification.</summary>
  DetectedPattern = 2,

  /// <summary>A user-owned reusable rule supplied the classification.</summary>
  UserRule = 3,

  /// <summary>The user explicitly confirmed the classification.</summary>
  Manual = 4,
}

/// <summary>
/// Identifies the source that created a transaction.
/// </summary>
public enum TransactionSourceKind
{
  /// <summary>The source predates explicit provenance.</summary>
  Legacy = 0,

  /// <summary>The transaction was entered manually.</summary>
  Manual = 1,

  /// <summary>The transaction came from a bank statement.</summary>
  BankStatement = 2,

  /// <summary>The transaction came from a credit-card invoice.</summary>
  CardInvoice = 3,

  /// <summary>The transaction came from an email integration.</summary>
  Email = 4,

  /// <summary>The transaction came from an HTTP API.</summary>
  Api = 5,
}

/// <summary>
/// Defines financial metrics that can drive a key result automatically.
/// </summary>
public enum FinancialMetricSource
{
  /// <summary>The key result remains manually measured.</summary>
  Manual = 0,

  /// <summary>Current canonical net worth.</summary>
  NetWorth = 1,

  /// <summary>Current emergency reserve.</summary>
  EmergencyReserve = 2,

  /// <summary>Emergency reserve expressed in essential-expense months.</summary>
  EmergencyReserveMonths = 3,

  /// <summary>Idle cash as a percentage of net worth.</summary>
  IdleCashPercentage = 4,

  /// <summary>Operating expense for the current month.</summary>
  MonthlyOperatingExpense = 5,

  /// <summary>Investment contributions for the current month.</summary>
  MonthlyContribution = 6,

  /// <summary>Planned year-end net worth.</summary>
  PlannedYearEndNetWorth = 7,
}

/// <summary>
/// Defines the lifecycle state of a compact planning review.
/// </summary>
public enum PlanningReviewStatus
{
  /// <summary>The review is an explicit draft.</summary>
  Draft = 0,

  /// <summary>The review is complete.</summary>
  Completed = 1,
}
