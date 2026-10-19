//-----------------------------------------------------------------------
// <copyright file="CreditCardEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents a credit card and its associated details, including cardholder information, card type, limits, benefits,
/// and account association.
/// </summary>
/// <remarks>This entity is used to model credit card data within an account, supporting various card features
/// such as rewards, insurance, and multiple transaction currencies. Optional properties may be null if the
/// corresponding feature is not applicable to the card. The entity includes navigation to the owning account and
/// supports display ordering within an account.</remarks>
public class CreditCardEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the cardholder name.
  /// </summary>
  public string HolderName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the type of the card.
  /// </summary>
  public CardType CardType { get; set; }

  /// <summary>
  /// Gets or sets the card brand/flag.
  /// </summary>
  public CardFlag Flag { get; set; }

  /// <summary>
  /// Gets or sets the card number (optional).
  /// </summary>
  public string? Number { get; set; }

  /// <summary>
  /// Gets or sets the card security code (optional).
  /// </summary>
  public string? SecurityCode { get; set; }

  /// <summary>
  /// Gets or sets the card validity/expiration date (optional).
  /// </summary>
  public DateTime? Validity { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card has a credit limit.
  /// </summary>
  public bool HasLimit { get; set; }

  /// <summary>
  /// Gets or sets the credit limit value (optional).
  /// </summary>
  public decimal? LimitValue { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card issues invoices.
  /// </summary>
  public bool HasInvoice { get; set; }

  /// <summary>
  /// Gets or sets the invoice closing day of month (optional).
  /// </summary>
  public int? InvoiceClosingDay { get; set; }

  /// <summary>
  /// Gets or sets the invoice payment day of month (optional).
  /// </summary>
  public int? InvoicePaymentDay { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card has an annual fee.
  /// </summary>
  public bool HasAnnualFee { get; set; }

  /// <summary>
  /// Gets or sets the annual fee amount (optional).
  /// </summary>
  public decimal? AnnualFee { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card offers benefits.
  /// </summary>
  public bool HasBenefits { get; set; }

  /// <summary>
  /// Gets or sets benefits description (optional).
  /// </summary>
  public string? Benefits { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card has rewards.
  /// </summary>
  public bool HasRewards { get; set; }

  /// <summary>
  /// Gets or sets rewards description (optional).
  /// </summary>
  public string? Rewards { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card includes insurance.
  /// </summary>
  public bool HasInsurance { get; set; }

  /// <summary>
  /// Gets or sets insurance details (optional).
  /// </summary>
  public string? Insurance { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card provides assistance services.
  /// </summary>
  public bool HasAssistance { get; set; }

  /// <summary>
  /// Gets or sets assistance details (optional).
  /// </summary>
  public string? Assistance { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the card has other benefits.
  /// </summary>
  public bool HasOtherBenefits { get; set; }

  /// <summary>
  /// Gets or sets other benefits description (optional).
  /// </summary>
  public string? OtherBenefits { get; set; }

  /// <summary>
  /// Gets or sets the status of the card (active/inactive).
  /// </summary>
  public bool Status { get; set; } = true;

  /// <summary>
  /// Gets or sets the currency used by the card.
  /// </summary>
  public Currency Currency { get; set; } = Currency.BRL;

  /// <summary>
  /// Multiple transaction currencies supported by this card. Stored as JSON in DB.
  /// </summary>
  public List<Currency> TransactionCurrencies { get; set; } = new List<Currency>();

  /// <summary>
  /// Gets or sets the foreign key to the owning account.
  /// </summary>
  public int AccountId { get; set; }

  /// <summary>
  /// Display order for ordering cards within an account.
  /// </summary>
  public int DisplayOrder { get; set; }

  /// <summary>
  /// Navigation property to the owning account.
  /// </summary>
  [ForeignKey(nameof(AccountId))]
  public AccountEntity Account { get; set; } = null!;
}
