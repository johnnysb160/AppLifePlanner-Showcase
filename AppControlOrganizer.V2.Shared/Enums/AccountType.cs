//-----------------------------------------------------------------------
// <copyright file="AccountType.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Specifies the type of financial account.
/// </summary>
/// <remarks>Use this enumeration to indicate the category of an account, such as checking, savings, investment,
/// or loan. The values can be used to control business logic, reporting, or user interface display based on account
/// type.</remarks>
public enum AccountType
{
  /// <summary>
  /// Checking account.
  /// </summary>
  CheckingAccount = 1,

  /// <summary>
  /// Savings account.
  /// </summary>
  SavingsAccount = 2,

  /// <summary>
  /// Investment account.
  /// </summary>
  InvestmentAccount = 3,

  /// <summary>
  /// Credit account.
  /// </summary>
  CreditAccount = 4,

  /// <summary>
  /// Loan account.
  /// </summary>
  LoanAccount = 5,

  /// <summary>
  /// Financing account.
  /// </summary>
  FinancingAccount = 6,

  /// <summary>
  /// Pension account.
  /// </summary>
  PensionAccount = 7,

  /// <summary>
  /// Insurance account.
  /// </summary>
  InsuranceAccount = 8,

  /// <summary>
  /// Consortium account.
  /// </summary>
  ConsortiumAccount = 9,

  /// <summary>
  /// Other account type.
  /// </summary>
  OtherAccount = 10
}
