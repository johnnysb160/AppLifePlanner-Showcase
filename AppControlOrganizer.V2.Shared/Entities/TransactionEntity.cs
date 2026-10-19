//-----------------------------------------------------------------------
// <copyright file="TransactionEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents an transaction, including its date, category, value, description, and income status.
/// </summary>
/// <remarks>This entity is used to store information about individual Transactions or financial transactions. It
/// supports both legacy enum-based categories and newer category references for backward compatibility. Use the
/// CategoryId and CategoryEntity properties for working with categories in new code.</remarks>
public class TransactionEntity : BaseEntity
{
	/// <summary>
	/// Gets or sets the date of the transaction.
	/// </summary>
	public DateTime Date { get; set; }

	/// <summary>
	/// Legacy enum-based category. Kept for backward compatibility with existing data. New code should use CategoryId instead.
	/// </summary>
	public Category Category { get; set; }

	/// <summary>
	/// FK to CategoryEntity. Nullable for backward compatibility with existing Transactions.
	/// </summary>
	public int? CategoryId { get; set; }

	/// <summary>
	/// Navigation property to CategoryEntity.
	/// </summary>
	public CategoryEntity? CategoryEntity { get; set; }

	/// <summary>
	/// Gets or sets the monetary value of the Transaction.
	/// </summary>
	public decimal Value { get; set; }

	/// <summary>
	/// Gets or sets the description or memo for the Transaction.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether this Transaction represents income.
	/// </summary>
	public bool Income { get; set; }

	/// <summary>
	/// Optional FK to AccountEntity. Links the transaction to a specific account for balance sync.
	/// </summary>
	public int? AccountId { get; set; }

	/// <summary>
	/// Navigation property to AccountEntity.
	/// </summary>
	public AccountEntity? Account { get; set; }

  /// <summary>Gets or sets the optional credit card associated with an imported invoice transaction.</summary>
  public int? CreditCardId { get; set; }

  /// <summary>Gets or sets the source that created the transaction.</summary>
  public TransactionSourceKind SourceKind { get; set; } = TransactionSourceKind.Legacy;

  /// <summary>Gets or sets how the transaction affects planning calculations.</summary>
  public TransactionPlanningImpact PlanningImpact { get; set; } = TransactionPlanningImpact.Unclassified;

  /// <summary>Gets or sets the source of the planning classification.</summary>
  public PlanningClassificationSource PlanningClassificationSource { get; set; } = PlanningClassificationSource.None;

  /// <summary>Gets or sets a value indicating whether the planning classification was explicitly confirmed.</summary>
  public bool PlanningClassificationConfirmed { get; set; }

  /// <summary>Gets or sets the optional time of day (HH:mm) reported by the source, since <see cref="Date"/> keeps only the day.</summary>
  public string? TransactionTime { get; set; }

  /// <summary>Gets or sets optional original source text (statement line, e-mail excerpt, issuer category) that helps identify the transaction later.</summary>
  public string? SourceDetails { get; set; }
}
