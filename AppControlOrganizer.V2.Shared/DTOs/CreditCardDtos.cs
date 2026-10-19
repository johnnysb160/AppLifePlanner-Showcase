//-----------------------------------------------------------------------
// <copyright file="CreditCardDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

using System.ComponentModel.DataAnnotations;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents the data required to create a new credit card, including cardholder information, card details, limits,
/// fees, and associated account.
/// </summary>
/// <remarks>This data transfer object is typically used when submitting information to create a credit card
/// record in the system. It includes required fields such as cardholder name, card type, card brand, currency, and
/// account association, as well as optional fields for card features, benefits, and financial details. Validation
/// attributes are applied to ensure required information is provided and to enforce value constraints where
/// applicable.</remarks>
public class CreateCreditCardDto
{
  /// <summary>
  /// Gets or sets the cardholder name.
  /// </summary>
  [Required(ErrorMessage = "Cardholder name is required")]
  [StringLength(100, ErrorMessage = "Cardholder name must be at most 100 characters")]
  public string HolderName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the type of the card.
  /// </summary>
  [Required(ErrorMessage = "Card type is required")]
  public CardType CardType { get; set; }

  /// <summary>
  /// Gets or sets the card brand/flag.
  /// </summary>
  [Required(ErrorMessage = "Card brand is required")]
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
  [Required(ErrorMessage = "Currency is required")]
  public Currency Currency { get; set; } = Currency.BRL;

  /// <summary>
  /// Multiple transaction currencies supported by this card.
  /// </summary>
  public List<Currency> TransactionCurrencies { get; set; } = new List<Currency>();

  /// <summary>
  /// Gets or sets the identifier of the associated account.
  /// </summary>
  [Required(ErrorMessage = "Account is required")]
  public int AccountId { get; set; }
}

/// <summary>
/// Represents the data required to update an existing credit card, including cardholder information, card details,
/// limits, fees, and associated account.
/// </summary>
/// <remarks>This data transfer object is typically used in update operations to modify the details of a credit
/// card in a financial or banking application. All required fields must be provided to ensure successful validation.
/// Optional fields allow for partial updates of card features such as benefits, rewards, and insurance. The object
/// supports cards with multiple transaction currencies and various card features. Validation attributes are applied to
/// ensure data integrity.</remarks>
public class UpdateCreditCardDto
{
  /// <summary>
  /// Gets or sets the cardholder name.
  /// </summary>
  [Required(ErrorMessage = "Cardholder name is required")]
  [StringLength(100, ErrorMessage = "Cardholder name must be at most 100 characters")]
  public string HolderName { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the type of the card.
  /// </summary>
  [Required(ErrorMessage = "Card type is required")]
  public CardType CardType { get; set; }

  /// <summary>
  /// Gets or sets the card brand/flag.
  /// </summary>
  [Required(ErrorMessage = "Card brand is required")]
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
  public bool Status { get; set; }

  /// <summary>
  /// Gets or sets the currency used by the card.
  /// </summary>
  [Required(ErrorMessage = "Currency is required")]
  public Currency Currency { get; set; }

  /// <summary>
  /// Multiple transaction currencies supported by this card.
  /// </summary>
  public List<Currency> TransactionCurrencies { get; set; } = new List<Currency>();

  /// <summary>
  /// Gets or sets the identifier of the associated account.
  /// </summary>
  [Required(ErrorMessage = "Account is required")]
  public int AccountId { get; set; }
}
