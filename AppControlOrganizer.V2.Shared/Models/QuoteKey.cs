//-----------------------------------------------------------------------
// <copyright file="QuoteKey.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Composite key that uniquely identifies a market quote.
/// For hardcoded specific types (BTC, IBM, SPY…) Symbol is null — AllocationType alone is sufficient.
/// For generic types (Stocks, FII, ETF, BDR, ForeignStocks, Crypto…) Symbol holds the ticker/id
/// taken from the allocation's Initials field, normalised to upper-case.
/// </summary>
public sealed record QuoteKey(AllocationType AllocationType, string? Symbol)
{
  /// <summary>
  /// Creates a QuoteKey for a hardcoded specific type (Symbol = null).
  /// </summary>
  public static QuoteKey ForType(AllocationType type) => new(type, null);

  /// <summary>
  /// Creates a QuoteKey for a dynamic asset identified by ticker symbol.
  /// Symbol is trimmed and upper-cased for consistent comparison.
  /// </summary>
  public static QuoteKey ForSymbol(AllocationType type, string symbol)
    => new(type, symbol.Trim().ToUpperInvariant());

  /// <summary>
  /// Derives a QuoteKey from an allocation entity, combining its type and normalised Initials.
  /// Returns null if the allocation has no Initials and the type is not a hardcoded specific type.
  /// </summary>
  public static QuoteKey? FromAllocation(
    AllocationType type,
    string? initials,
    bool isHardcodedType)
  {
    if (isHardcodedType)
    {
      return ForType(type);
    }

    if (string.IsNullOrWhiteSpace(initials))
    {
      return null;
    }

    return ForSymbol(type, initials);
  }
}
