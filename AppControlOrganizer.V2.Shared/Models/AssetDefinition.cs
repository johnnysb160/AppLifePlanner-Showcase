//-----------------------------------------------------------------------
// <copyright file="AssetDefinition.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines the properties needed to fetch quotes for a specific allocation type.
/// </summary>
public sealed class AssetDefinition
{
  /// <summary>
  /// Gets or sets the allocation type.
  /// </summary>
  public AllocationType AllocationType { get; init; }

  /// <summary>
  /// Gets or sets the asset kind.
  /// </summary>
  public AssetKind Kind { get; init; }

  /// <summary>
  /// Gets or sets the currency for quotes.
  /// </summary>
  public Currency QuoteCurrency { get; init; } = Currency.USD;

  /// <summary>
  /// Gets or sets the stock or ETF symbol.
  /// </summary>
  public string? Symbol { get; init; }

  /// <summary>
  /// Gets or sets the Stooq symbol for stocks/ETFs.
  /// </summary>
  public string? StooqSymbol { get; init; }

  /// <summary>
  /// Gets or sets the CoinGecko identifier for crypto.
  /// </summary>
  public string? CoinGeckoId { get; init; }

  /// <summary>
  /// Gets or sets the CoinCap identifier for crypto.
  /// </summary>
  public string? CoinCapId { get; init; }

  /// <summary>
  /// Gets or sets the Binance symbol for crypto.
  /// </summary>
  public string? BinanceSymbol { get; init; }

  /// <summary>
  /// Gets or sets the FX pair for foreign exchange.
  /// </summary>
  public string? FxPair { get; init; }
}

/// <summary>
/// Result of a quote fetch attempt.
/// </summary>
public sealed class QuoteResult
{
  /// <summary>
  /// Gets or sets a value indicating whether the quote fetch was successful.
  /// </summary>
  public bool Success { get; init; }

  /// <summary>
  /// Gets or sets the fetched price.
  /// </summary>
  public decimal? Price { get; init; }

  /// <summary>
  /// Gets or sets the quote timestamp in UTC.
  /// </summary>
  public DateTime? QuoteTimestampUtc { get; init; }

  /// <summary>
  /// Gets or sets the data source name.
  /// </summary>
  public string Source { get; init; } = string.Empty;

  /// <summary>
  /// Gets or sets the error message if any.
  /// </summary>
  public string? Error { get; init; }

  /// <summary>
  /// Gets or sets the warning message if any.
  /// </summary>
  public string? Warning { get; init; }
}
