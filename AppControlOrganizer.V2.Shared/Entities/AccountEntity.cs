//-----------------------------------------------------------------------
// <copyright file="AccountEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents a financial account, including details such as institution type, account number, supported currencies,
/// and related cards or allocations.
/// </summary>
/// <remarks>This entity is used to model various types of financial accounts within an institution, supporting
/// multiple currencies and associated features such as cards, fees, and investments. It includes navigation properties
/// for related credit cards and allocations. The class inherits from BaseEntity, which may provide common entity
/// properties such as identifiers or timestamps.</remarks>
public class AccountEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the account display name.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the institution type for the account.
  /// </summary>
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
  /// Gets or sets the type or description of fees/charges (optional).
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
  /// Gets or sets the primary currency for the account.
  /// </summary>
  public Currency Currency { get; set; } = Currency.BRL;

  /// <summary>
  /// Additional supported currencies for this account. Stored as JSON.
  /// </summary>
  public List<Currency> SupportedCurrencies { get; set; } = new List<Currency>();

  /// <summary>
  /// Display order for manual ordering in UI. Backfilled when missing.
  /// </summary>
  public int DisplayOrder { get; set; }

  /// <summary>
  /// Gets or sets the credit cards associated with this account.
  /// </summary>
  public ICollection<CreditCardEntity> Cards { get; set; } = new List<CreditCardEntity>();

  /// <summary>
  /// Gets or sets the allocations associated with this account.
  /// </summary>
  public ICollection<AllocationEntity> Allocations { get; set; } = new List<AllocationEntity>();

  /// <summary>
  /// Computed balance: sum of all linked transactions (income minus expense). Recalculated by FinancialSyncService.
  /// </summary>
  public decimal Balance { get; set; }

  /// <summary>
  /// Computed total invested: sum of BuyUnitValue × Quantity for all active allocations. Recalculated by FinancialSyncService.
  /// </summary>
  public decimal TotalInvested { get; set; }

  /// <summary>
  /// Gets or sets how this account contributes to the canonical financial position.
  /// Existing accounts default to position-based behavior to preserve their allocation-driven totals.
  /// </summary>
  public AccountBalanceMode BalanceMode { get; set; } = AccountBalanceMode.PositionBased;

  /// <summary>Gets or sets an optional opening balance for ledger-based accounts.</summary>
  public decimal? OpeningBalance { get; set; }

  /// <summary>Gets or sets the local date associated with the optional opening balance.</summary>
  public DateOnly? OpeningBalanceDate { get; set; }
}
