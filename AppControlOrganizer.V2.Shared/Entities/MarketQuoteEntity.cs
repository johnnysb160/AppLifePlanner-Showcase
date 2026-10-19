//-----------------------------------------------------------------------
// <copyright file="MarketQuoteEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Represents a market quote record with price, status, and metadata for bounded rolling history.
/// </summary>
public class MarketQuoteEntity : BaseEntity
{
  /// <summary>
  /// Gets or sets the allocation type for which this quote applies.
  /// </summary>
  public AllocationType AllocationType { get; set; }

  /// <summary>
  /// Gets or sets the ticker symbol for this quote (e.g. "PETR4", "AAPL", "bitcoin").
  /// Null for legacy hardcoded types (BTC, IBM, SPY…) where AllocationType alone is unique.
  /// For generic types (Stocks, FII, ETF, Crypto…) this discriminates individual assets.
  /// </summary>
  public string? Symbol { get; set; }

  /// <summary>
  /// Gets or sets the currency in which the quote price is denominated.
  /// </summary>
  public Currency QuoteCurrency { get; set; }

  /// <summary>
  /// Gets or sets the quoted price. Null when status is Failed or Unsupported.
  /// </summary>
  public decimal? Price { get; set; }

  /// <summary>
  /// Gets or sets the UTC timestamp when the quote was retrieved by the worker.
  /// </summary>
  public DateTime RetrievedAtUtc { get; set; }

  /// <summary>
  /// Gets or sets the UTC timestamp provided by the quote source. Null if unavailable.
  /// </summary>
  public DateTime? QuoteTimestampUtc { get; set; }

  /// <summary>
  /// Gets or sets the data source name (e.g., "Finnhub", "TwelveData").
  /// </summary>
  public string Source { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the retrieval status of this quote.
  /// </summary>
  public MarketQuoteStatus Status { get; set; }

  /// <summary>
  /// Gets or sets the error message if retrieval failed. Null on success.
  /// </summary>
  public string? Error { get; set; }

  /// <summary>
  /// Gets or sets a warning message for partial or degraded data. Null if none.
  /// </summary>
  public string? Warning { get; set; }
}
