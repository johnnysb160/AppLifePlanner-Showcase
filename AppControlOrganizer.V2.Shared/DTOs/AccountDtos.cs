//-----------------------------------------------------------------------
// <copyright file="AccountDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

using System.ComponentModel.DataAnnotations;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents the data required to create a new account, including institution details, account information, and
/// related financial attributes.
/// </summary>
/// <remarks>Use this data transfer object when submitting information to create a new account in the system. All
/// required fields must be provided to ensure successful account creation. Optional fields can be omitted if not
/// applicable. Validation attributes are applied to ensure data integrity.</remarks>
public class CreateAccountDto
{
  /// <summary>
  /// Gets or sets the account name.
  /// </summary>
  [Required(ErrorMessage = "Name is required")]
  [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the institution type for the account.
  /// </summary>
  [Required(ErrorMessage = "Institution type is required")]
  public InstitutionType InstitutionType { get; set; }

  /// <summary>
  /// Gets or sets the logo URL or path for the institution (optional).
  /// </summary>
  public string? Logo { get; set; }

  /// <summary>
  /// Gets or sets the account number (optional).
  /// </summary>
  public string? NumberAccount { get; set; }

  /// <summary>
  /// Gets or sets the agency/branch code (optional).
  /// </summary>
  public string? Agency { get; set; }

  /// <summary>
  /// Gets or sets the bank code (optional).
  /// </summary>
  public string? BankCode { get; set; }

  /// <summary>
  /// Gets or sets the account type (optional).
  /// </summary>
  public AccountType? AccountType { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account has an associated card.
  /// </summary>
  public bool HasCard { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account is active.
  /// </summary>
  public bool AccountIsActive { get; set; } = true;

  /// <summary>
  /// Gets or sets a value indicating whether the account has fees or charges.
  /// </summary>
  public bool HasFeesOrCharges { get; set; }

  /// <summary>
  /// Gets or sets the type/description of fees or charges (optional).
  /// </summary>
  public string? FeesOrChargesType { get; set; }

  /// <summary>
  /// Gets or sets the value of fees or charges (optional).
  /// </summary>
  public decimal? FeesOrChargesValue { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account has investments.
  /// </summary>
  public bool HasInvestments { get; set; }

  /// <summary>
  /// Gets or sets the account currency.
  /// </summary>
  [Required(ErrorMessage = "Currency is required")]
  public Currency Currency { get; set; } = Currency.BRL;

  /// <summary>
  /// Gets or sets how the account balance should be calculated.
  /// </summary>
  public AccountBalanceMode BalanceMode { get; set; } = AccountBalanceMode.PositionBased;

  /// <summary>
  /// Gets or sets the manually confirmed balance used as the ledger reconciliation anchor.
  /// </summary>
  public decimal? OpeningBalance { get; set; }

  /// <summary>
  /// Gets or sets the date when the ledger reconciliation anchor was confirmed.
  /// </summary>
  public DateOnly? OpeningBalanceDate { get; set; }
}

/// <summary>
/// Represents the data required to update an existing account, including account details, institution information, and
/// related financial attributes.
/// </summary>
/// <remarks>Use this data transfer object when submitting updates to an account's profile or financial settings.
/// All required fields must be provided to ensure a valid update. Optional fields may be omitted if no changes are
/// needed for those values.</remarks>
public class UpdateAccountDto
{
  /// <summary>
  /// Gets or sets the account name.
  /// </summary>
  [Required(ErrorMessage = "Name is required")]
  [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the institution type for the account.
  /// </summary>
  [Required(ErrorMessage = "Institution type is required")]
  public InstitutionType InstitutionType { get; set; }

  /// <summary>
  /// Gets or sets the logo URL or path for the institution (optional).
  /// </summary>
  public string? Logo { get; set; }

  /// <summary>
  /// Gets or sets the account number (optional).
  /// </summary>
  public string? NumberAccount { get; set; }

  /// <summary>
  /// Gets or sets the agency/branch code (optional).
  /// </summary>
  public string? Agency { get; set; }

  /// <summary>
  /// Gets or sets the bank code (optional).
  /// </summary>
  public string? BankCode { get; set; }

  /// <summary>
  /// Gets or sets the account type (optional).
  /// </summary>
  public AccountType? AccountType { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account has an associated card.
  /// </summary>
  public bool HasCard { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account is active.
  /// </summary>
  public bool AccountIsActive { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account has fees or charges.
  /// </summary>
  public bool HasFeesOrCharges { get; set; }

  /// <summary>
  /// Gets or sets the type/description of fees or charges (optional).
  /// </summary>
  public string? FeesOrChargesType { get; set; }

  /// <summary>
  /// Gets or sets the value of fees or charges (optional).
  /// </summary>
  public decimal? FeesOrChargesValue { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the account has investments.
  /// </summary>
  public bool HasInvestments { get; set; }

  /// <summary>
  /// Gets or sets the account currency.
  /// </summary>
  [Required(ErrorMessage = "Currency is required")]
  public Currency Currency { get; set; }

  /// <summary>
  /// Gets or sets how the account balance should be calculated.
  /// </summary>
  public AccountBalanceMode BalanceMode { get; set; } = AccountBalanceMode.PositionBased;

  /// <summary>
  /// Gets or sets the manually confirmed balance used as the ledger reconciliation anchor.
  /// </summary>
  public decimal? OpeningBalance { get; set; }

  /// <summary>
  /// Gets or sets the date when the ledger reconciliation anchor was confirmed.
  /// </summary>
  public DateOnly? OpeningBalanceDate { get; set; }
}
