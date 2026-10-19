//-----------------------------------------------------------------------
// <copyright file="ImportModels.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Request parameters for an import operation.
/// </summary>
public sealed class ImportRequest
{
  /// <summary>
  /// Gets or sets the source type of the import (e.g., file provider or bank).
  /// </summary>
  public ImportSourceType SourceType { get; set; }

  /// <summary>
  /// Gets or sets the file format of the import (e.g., CSV, QIF).
  /// </summary>
  public ImportFileFormat FileFormat { get; set; }

  /// <summary>
  /// Gets or sets the identifier of the account that will receive imported items.
  /// </summary>
  public int AccountId { get; set; }

  /// <summary>
  /// Gets or sets an optional credit card identifier when importing card statements.
  /// </summary>
  public int? CreditCardId { get; set; }

  /// <summary>
  /// Gets or sets the last four digits of the credit card used for identification (optional).
  /// </summary>
  public string? CreditCardLastFour { get; set; }

  /// <summary>
  /// Gets or sets the stream containing the import file contents.
  /// </summary>
  public Stream FileStream { get; set; } = Stream.Null;

  /// <summary>
  /// Gets or sets the name of the import file.
  /// </summary>
  public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Parsed candidate ready for validation/insert.
/// </summary>
public sealed class ImportCandidate
{
  /// <summary>
  /// Gets or sets the parsed date of the candidate item.
  /// </summary>
  public DateTime Date { get; set; }

  /// <summary>
  /// Gets or sets the category assigned or inferred for the candidate.
  /// </summary>
  public Category Category { get; set; }

  /// <summary>
  /// Gets or sets the monetary value of the candidate.
  /// </summary>
  public decimal Value { get; set; }

  /// <summary>
  /// Gets or sets the description/text extracted for the candidate.
  /// </summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether the candidate represents income.
  /// </summary>
  public bool Income { get; set; }

  /// <summary>
  /// Gets or sets an optional computed external identifier used for duplicate detection.
  /// </summary>
  public int? ComputedExternalId { get; set; }

  /// <summary>
  /// Gets or sets the raw identifier string extracted from the source for tracing.
  /// </summary>
  public string RawIdentifier { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the reason why this candidate is pending (if any).
  /// </summary>
  public PendingReason PendingReason { get; set; }

  /// <summary>
  /// Gets or sets an optional message with details about the pending status.
  /// </summary>
  public string PendingMessage { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional time of day (HH:mm) reported by the source.</summary>
  public string? TransactionTime { get; set; }

  /// <summary>Gets or sets optional original source text kept to identify the transaction later.</summary>
  public string? SourceDetails { get; set; }

  /// <summary>
  /// Gets or sets the identifier of an existing, never edited transaction that this candidate enriches
  /// with more information instead of being inserted as a new row.
  /// </summary>
  public int? EnrichTransactionId { get; set; }
}

/// <summary>
/// Result of an import operation.
/// </summary>
public sealed class ImportResult
{
  /// <summary>
  /// Gets or sets the total number of items parsed from the import file.
  /// </summary>
  public int TotalParsed { get; set; }

  /// <summary>
  /// Gets or sets the number of items successfully inserted.
  /// </summary>
  public int Inserted { get; set; }

  /// <summary>
  /// Gets or sets the number of items skipped due to duplicate detection.
  /// </summary>
  public int SkippedDuplicates { get; set; }

  /// <summary>
  /// Gets or sets the number of existing transactions updated with more complete information instead of being duplicated.
  /// </summary>
  public int Enriched { get; set; }

  /// <summary>
  /// Gets or sets the count of items marked as pending for manual review.
  /// </summary>
  public int PendingCount { get; set; }

  /// <summary>
  /// Gets or sets the number of errors encountered during import.
  /// </summary>
  public int Errors { get; set; }

  /// <summary>
  /// Gets or sets the list of pending items requiring review.
  /// </summary>
  public List<PendingItem> PendingItems { get; set; } = new List<PendingItem>();

  /// <summary>
  /// Gets or sets an optional error message describing a failure in the import process.
  /// </summary>
  public string? ErrorMessage { get; set; }

  /// <summary>
  /// Gets or sets the account identifier that originated the import (used by UI to determine currency).
  /// </summary>
  public int AccountId { get; set; }

  /// <summary>
  /// Gets or sets the currency of the account that originated the import.
  /// </summary>
  public Currency AccountCurrency { get; set; }
}

/// <summary>
/// Pending item shown in review modal.
/// </summary>
public sealed class PendingItem
{
  /// <summary>
  /// Gets or sets a temporary identifier for the pending item.
  /// </summary>
  public Guid TempId { get; set; } = Guid.NewGuid();

  /// <summary>
  /// Gets or sets the date of the pending item.
  /// </summary>
  public DateTime Date { get; set; }

  /// <summary>
  /// Gets or sets the category assigned to the pending item.
  /// </summary>
  public Category Category { get; set; }

  /// <summary>
  /// Gets or sets the monetary value of the pending item.
  /// </summary>
  public decimal Value { get; set; }

  /// <summary>
  /// Gets or sets the description of the pending item.
  /// </summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether this pending item is income.
  /// </summary>
  public bool Income { get; set; }

  /// <summary>
  /// Gets or sets an optional computed external identifier used for matching.
  /// </summary>
  public int? ComputedExternalId { get; set; }

  /// <summary>
  /// Gets or sets the account identifier the transaction should be linked to.
  /// </summary>
  public int? AccountId { get; set; }

  /// <summary>Gets or sets the optional credit-card identifier from the import request.</summary>
  public int? CreditCardId { get; set; }

  /// <summary>Gets or sets the transaction source represented by this pending item.</summary>
  public TransactionSourceKind SourceKind { get; set; } = TransactionSourceKind.Legacy;

  /// <summary>Gets or sets the optional time of day (HH:mm) reported by the source.</summary>
  public string? TransactionTime { get; set; }

  /// <summary>Gets or sets optional original source text kept to identify the transaction later.</summary>
  public string? SourceDetails { get; set; }

  /// <summary>
  /// Gets or sets the reason code for why the item is pending.
  /// </summary>
  public PendingReason Reason { get; set; }

  /// <summary>
  /// Gets or sets the textual explanation for the pending reason.
  /// </summary>
  public string ReasonText { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether the item is selected in the UI.
  /// </summary>
  public bool IsSelected { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the item is currently being edited.
  /// </summary>
  public bool IsEditing { get; set; }

  /// <summary>
  /// Gets or sets the currency of the originating account for this pending item.
  /// </summary>
  public Currency Currency { get; set; } = Currency.BRL;
}
