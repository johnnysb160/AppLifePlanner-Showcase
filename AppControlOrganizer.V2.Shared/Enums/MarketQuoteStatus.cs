//-----------------------------------------------------------------------
// <copyright file="MarketQuoteStatus.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Indicates the outcome status of a market quote retrieval operation.
/// </summary>
public enum MarketQuoteStatus
{
  /// <summary>
  /// The quote was successfully retrieved.
  /// </summary>
  Success = 1,

  /// <summary>
  /// The quote retrieval failed due to an error.
  /// </summary>
  Failed = 2,

  /// <summary>
  /// The allocation type is not supported for quote retrieval.
  /// </summary>
  Unsupported = 3
}
