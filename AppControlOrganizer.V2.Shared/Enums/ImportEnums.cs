//-----------------------------------------------------------------------
// <copyright file="ImportEnums.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Type of import source (bank statement vs card invoice).
/// </summary>
public enum ImportSourceType
{
  /// <summary>
  /// Bank statement import source type.
  /// </summary>
  BankStatement = 1,

  /// <summary>
  /// Card invoice import source type.
  /// </summary>
  CardInvoice = 2
}

/// <summary>
/// Supported file formats for import.
/// </summary>
public enum ImportFileFormat
{
  /// <summary>
  /// OFX file format.
  /// </summary>
  Ofx = 1,

  /// <summary>
  /// CSV file format.
  /// </summary>
  Csv = 2
}

/// <summary>
/// Reason why an import candidate became pending.
/// </summary>
public enum PendingReason
{
  /// <summary>
  /// No pending reason.
  /// </summary>
  None = 0,

  /// <summary>
  /// Possible duplicate pending reason.
  /// </summary>
  PossibleDuplicate = 1,

  /// <summary>
  /// Parse error pending reason.
  /// </summary>
  ParseError = 2,

  /// <summary>
  /// Negative or zero amount pending reason.
  /// </summary>
  NegativeOrZeroAmount = 3,

  /// <summary>
  /// Card mismatch pending reason.
  /// </summary>
  CardMismatch = 4,

  /// <summary>
  /// Insert error pending reason.
  /// </summary>
  InsertError = 5,

  /// <summary>
  /// Invalid date pending reason.
  /// </summary>
  InvalidDate = 6
}
