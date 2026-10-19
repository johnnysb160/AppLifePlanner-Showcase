//-----------------------------------------------------------------------
// <copyright file="FinancialYearReview.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Aggregated annual financial review for a single user.
/// </summary>
public sealed class FinancialYearReview
{
  /// <summary>Gets or sets the selected calendar year.</summary>
  public int Year { get; set; }

  /// <summary>Gets or sets the date used for canonical position facts.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets when the review was calculated.</summary>
  public DateTime CalculatedAtUtc { get; set; }

  /// <summary>Gets or sets available years with financial activity.</summary>
  public List<int> AvailableYears { get; set; } = new List<int>();

  /// <summary>Gets or sets a value indicating whether the selected year has cash-flow activity.</summary>
  public bool HasYearActivity { get; set; }

  /// <summary>Gets or sets the number of selected-year transactions.</summary>
  public int TransactionCount { get; set; }

  /// <summary>Gets or sets annual operating income.</summary>
  public decimal AnnualOperatingIncome { get; set; }

  /// <summary>Gets or sets annual operating expenses.</summary>
  public decimal AnnualOperatingExpense { get; set; }

  /// <summary>Gets or sets annual long-term investment contributions.</summary>
  public decimal AnnualContributions { get; set; }

  /// <summary>Gets or sets annual result including investment contributions.</summary>
  public decimal AnnualResult { get; set; }

  /// <summary>Gets or sets average monthly operating income.</summary>
  public decimal AverageMonthlyIncome { get; set; }

  /// <summary>Gets or sets average monthly operating expense.</summary>
  public decimal AverageMonthlyExpense { get; set; }

  /// <summary>Gets or sets annual savings rate based on income and contributions.</summary>
  public decimal SavingsRate { get; set; }

  /// <summary>Gets or sets current canonical net worth.</summary>
  public decimal NetWorth { get; set; }

  /// <summary>Gets or sets estimated beginning net worth derived from current net worth and annual result.</summary>
  public decimal EstimatedStartNetWorth { get; set; }

  /// <summary>Gets or sets estimated net-worth movement for the year.</summary>
  public decimal NetWorthChange { get; set; }

  /// <summary>Gets or sets estimated net-worth movement percentage.</summary>
  public decimal NetWorthChangePercentage { get; set; }

  /// <summary>Gets or sets current account cash.</summary>
  public decimal AccountCash { get; set; }

  /// <summary>Gets or sets current emergency reserve.</summary>
  public decimal EmergencyReserve { get; set; }

  /// <summary>Gets or sets current idle cash.</summary>
  public decimal IdleCash { get; set; }

  /// <summary>Gets or sets current long-term investments.</summary>
  public decimal LongTermInvestments { get; set; }

  /// <summary>Gets or sets current applied allocation value.</summary>
  public decimal InvestmentAppliedValue { get; set; }

  /// <summary>Gets or sets current allocation value.</summary>
  public decimal InvestmentCurrentValue { get; set; }

  /// <summary>Gets or sets current investment profit or loss.</summary>
  public decimal InvestmentProfit { get; set; }

  /// <summary>Gets or sets current investment return percentage.</summary>
  public decimal InvestmentReturnPercentage { get; set; }

  /// <summary>Gets or sets selected-year investment exits.</summary>
  public decimal InvestmentExits { get; set; }

  /// <summary>Gets or sets the number of positions without current pricing.</summary>
  public int UnpricedPositionCount { get; set; }

  /// <summary>Gets or sets monthly review items.</summary>
  public List<FinancialYearReviewMonth> Months { get; set; } = new List<FinancialYearReviewMonth>();

  /// <summary>Gets or sets top expense categories.</summary>
  public List<FinancialYearReviewCategory> TopExpenseCategories { get; set; } = new List<FinancialYearReviewCategory>();

  /// <summary>Gets or sets current investment positions ranked by financial relevance.</summary>
  public List<FinancialYearReviewInvestmentPosition> InvestmentPositions { get; set; } = new List<FinancialYearReviewInvestmentPosition>();

  /// <summary>Gets or sets deterministic analysis keys.</summary>
  public List<string> AnalysisKeys { get; set; } = new List<string>();

  /// <summary>Gets or sets deterministic analyst insight blocks.</summary>
  public List<FinancialYearReviewInsight> AnalystInsights { get; set; } = new List<FinancialYearReviewInsight>();
}

/// <summary>
/// One month inside an annual financial review.
/// </summary>
public sealed class FinancialYearReviewMonth
{
  /// <summary>Gets or sets the month number.</summary>
  public int Month { get; set; }

  /// <summary>Gets or sets operating income.</summary>
  public decimal OperatingIncome { get; set; }

  /// <summary>Gets or sets operating expense.</summary>
  public decimal OperatingExpense { get; set; }

  /// <summary>Gets or sets long-term investment contributions.</summary>
  public decimal Contributions { get; set; }

  /// <summary>Gets or sets result including contributions.</summary>
  public decimal Result { get; set; }

  /// <summary>Gets or sets the number of transactions in the month.</summary>
  public int TransactionCount { get; set; }

  /// <summary>Gets or sets the savings rate for the month.</summary>
  public decimal SavingsRate { get; set; }
}

/// <summary>
/// Category aggregate inside an annual financial review.
/// </summary>
public sealed class FinancialYearReviewCategory
{
  /// <summary>Gets or sets the category identifier, when the dynamic category model is used.</summary>
  public int? CategoryId { get; set; }

  /// <summary>Gets or sets the legacy category, when no dynamic category is linked.</summary>
  public Category? LegacyCategory { get; set; }

  /// <summary>Gets or sets the localization key or display name.</summary>
  public string NameKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the category total.</summary>
  public decimal Total { get; set; }

  /// <summary>Gets or sets the category percentage of annual expenses.</summary>
  public decimal Percentage { get; set; }

  /// <summary>Gets or sets the number of transactions in this category.</summary>
  public int Count { get; set; }
}

/// <summary>
/// Investment-position aggregate for the annual review dashboard.
/// </summary>
public sealed class FinancialYearReviewInvestmentPosition
{
  /// <summary>Gets or sets the allocation identifier.</summary>
  public int AllocationId { get; set; }

  /// <summary>Gets or sets the allocation name.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Gets or sets the allocation ticker or initials.</summary>
  public string Symbol { get; set; } = string.Empty;

  /// <summary>Gets or sets the allocation type label.</summary>
  public string Type { get; set; } = string.Empty;

  /// <summary>Gets or sets the allocation currency label.</summary>
  public string Currency { get; set; } = string.Empty;

  /// <summary>Gets or sets applied value in the allocation currency.</summary>
  public decimal AppliedValue { get; set; }

  /// <summary>Gets or sets current value in the allocation currency.</summary>
  public decimal CurrentValue { get; set; }

  /// <summary>Gets or sets profit or loss in the allocation currency.</summary>
  public decimal Profit { get; set; }

  /// <summary>Gets or sets profit or loss percentage.</summary>
  public decimal ReturnPercentage { get; set; }

  /// <summary>Gets or sets a value indicating whether the current price is explicit.</summary>
  public bool HasCurrentPrice { get; set; }
}

/// <summary>
/// Deterministic analyst insight shown by the annual review page.
/// </summary>
public sealed class FinancialYearReviewInsight
{
  /// <summary>Gets or sets the insight tone.</summary>
  public string Tone { get; set; } = "neutral";

  /// <summary>Gets or sets the Bootstrap icon class.</summary>
  public string IconCss { get; set; } = "bi bi-lightbulb";

  /// <summary>Gets or sets the localized title key.</summary>
  public string TitleKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized body key.</summary>
  public string BodyKey { get; set; } = string.Empty;
}
