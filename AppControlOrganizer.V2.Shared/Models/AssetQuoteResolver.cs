//-----------------------------------------------------------------------
// <copyright file="AssetQuoteResolver.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using System.Text.RegularExpressions;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Strategy used to fetch a market quote for an asset.
/// </summary>
public enum QuoteFetchStrategy
{
  /// <summary>Asset has no market price (fixed-income, private pension, etc.).</summary>
  FixedIncome,

  /// <summary>FX rate (e.g. USD/BRL).</summary>
  Fx,

  /// <summary>Brazilian exchange (B3) — stock, FII, BDR, BR-ETF.</summary>
  B3,

  /// <summary>US/international market — stock or ETF (Finnhub / TwelveData / Yahoo).</summary>
  UsMarket,

  /// <summary>Cryptocurrency — CoinGecko / CoinCap / Binance.</summary>
  Crypto,

  /// <summary>Type is hardcoded in AssetCatalog; use existing static definition.</summary>
  Hardcoded,

  /// <summary>Cannot determine strategy — no Initials and not a known type.</summary>
  Unknown,
}

/// <summary>
/// Describes how to fetch a quote for a given allocation.
/// </summary>
public sealed class AssetQuoteTarget
{
  /// <summary>Composite key for storing / looking up the quote.</summary>
  public required QuoteKey Key { get; init; }

  /// <summary>Strategy to use when fetching this quote.</summary>
  public required QuoteFetchStrategy Strategy { get; init; }

  /// <summary>
  /// Ticker symbol for B3 / US market / crypto lookups.
  /// Null for Fx, FixedIncome, and Hardcoded when not needed.
  /// </summary>
  public string? Symbol { get; init; }

  /// <summary>The currency the quote will be denominated in.</summary>
  public Currency QuoteCurrency { get; init; } = Currency.USD;

  /// <summary>Human-readable reason when Strategy is Unknown or FixedIncome.</summary>
  public string? Note { get; init; }
}

/// <summary>
/// Resolves the correct quote-fetch strategy for any allocation type + initials combination.
/// This is the single source of truth for "how do we get market data for this asset?".
/// </summary>
public static class AssetQuoteResolver
{
  // ── Types that NEVER have a market price ──────────────────────────────────
  private static readonly HashSet<AllocationType> FixedIncomeTypes = new()
  {
    AllocationType.CDB,
    AllocationType.LCI,
    AllocationType.LCA,
    AllocationType.LC,
    AllocationType.COE,
    AllocationType.Debentures,
    AllocationType.CRI,
    AllocationType.CRA,
    AllocationType.FIDC,
    AllocationType.DirectTreasure,
    AllocationType.PrivatePension,
    AllocationType.InvestmentFunds,
    AllocationType.ForeignInvestmentFunds,
    AllocationType.Stopped,
  };

  // ── Hardcoded specific-type enum values (each maps 1-to-1 with an asset) ─
  private static readonly HashSet<AllocationType> HardcodedTypes = new()
  {
    // FX
    AllocationType.UsdBrl,
    // Crypto
    AllocationType.BTC, AllocationType.ETH, AllocationType.ADA,
    AllocationType.SOL, AllocationType.AXS, AllocationType.CHZ,
    // US Stocks
    AllocationType.ADC, AllocationType.DOC, AllocationType.GNL,
    AllocationType.GOOD, AllocationType.IBM, AllocationType.KO,
    AllocationType.MCD, AllocationType.MMM, AllocationType.NKE,
    AllocationType.O, AllocationType.SOLV,
    // US ETFs
    AllocationType.BND, AllocationType.EMB, AllocationType.JEPI,
    AllocationType.JEPQ, AllocationType.QQQ, AllocationType.SGOV,
    AllocationType.SPY, AllocationType.VEA, AllocationType.VNQ,
    AllocationType.VNQI, AllocationType.VT,
  };

  // ── Types that always trade on B3 regardless of currency ─────────────────
  private static readonly HashSet<AllocationType> AlwaysB3Types = new()
  {
    AllocationType.FII,
    AllocationType.BDR,
  };

  // ── B3 ticker pattern: 4 upper-case letters + 1 or 2 digits ─────────────
  // Examples: PETR4, VALE3, HGLG11, ITUB4, BBAS3, XPML11
  private static readonly Regex B3Pattern =
    new(@"^[A-Z]{4}\d{1,2}$", RegexOptions.Compiled);

  /// <summary>
  /// Determines the fetch strategy for an allocation identified by its type,
  /// initials (ticker), and primary currency.
  /// </summary>
  /// <param name="allocationType">The allocation's AllocationType.</param>
  /// <param name="initials">The allocation's Initials field (ticker / symbol).</param>
  /// <param name="currency">The allocation's primary Currency.</param>
  /// <returns>An <see cref="AssetQuoteTarget"/> describing how to fetch the quote.</returns>
  public static AssetQuoteTarget Resolve(
    AllocationType allocationType,
    string? initials,
    Currency currency)
  {
    // 1. Fixed income — never has a market price
    if (FixedIncomeTypes.Contains(allocationType))
    {
      return new AssetQuoteTarget
      {
        Key = QuoteKey.ForType(allocationType),
        Strategy = QuoteFetchStrategy.FixedIncome,
        Note = "Fixed-income instrument — no market quote available.",
      };
    }

    // 2. Hardcoded type — delegate to existing static AssetCatalog
    if (HardcodedTypes.Contains(allocationType))
    {
      return new AssetQuoteTarget
      {
        Key = QuoteKey.ForType(allocationType),
        Strategy = QuoteFetchStrategy.Hardcoded,
        Symbol = initials,
        QuoteCurrency = allocationType == AllocationType.UsdBrl ? Currency.BRL : Currency.USD,
      };
    }

    // 3. From here on we need the Initials to do anything useful
    string? symbol = string.IsNullOrWhiteSpace(initials)
      ? null
      : initials.Trim().ToUpperInvariant();

    // ── Generic Crypto ─────────────────────────────────────────────────────
    if (allocationType == AllocationType.Crypto || allocationType == AllocationType.Token)
    {
      if (symbol == null)
      {
        return Unknown(allocationType, "Crypto type requires a symbol in Initials.");
      }

      return new AssetQuoteTarget
      {
        Key = QuoteKey.ForSymbol(allocationType, symbol),
        Strategy = QuoteFetchStrategy.Crypto,
        Symbol = symbol,
        QuoteCurrency = Currency.USD,
      };
    }

    // ── Types that are always on B3 ────────────────────────────────────────
    if (AlwaysB3Types.Contains(allocationType))
    {
      if (symbol == null)
      {
        return Unknown(allocationType, $"{allocationType} requires a B3 ticker in Initials.");
      }

      return new AssetQuoteTarget
      {
        Key = QuoteKey.ForSymbol(allocationType, symbol),
        Strategy = QuoteFetchStrategy.B3,
        Symbol = symbol,
        QuoteCurrency = Currency.BRL,
      };
    }

    // ── Stocks / ETFs / ForeignStocks — resolve by currency + symbol pattern
    if (allocationType is AllocationType.Stocks
        or AllocationType.ETF
        or AllocationType.ForeignStocks
        or AllocationType.Opportunity
        or AllocationType.Others)
    {
      if (symbol == null)
      {
        return Unknown(allocationType, $"{allocationType} requires a ticker in Initials.");
      }

      // Free-form types (Others, Opportunity) often hold a name rather than a ticker (e.g. a vehicle model):
      // looking it up on B3 fails every hour and only pollutes the quote history.
      bool isFreeForm = allocationType is AllocationType.Opportunity or AllocationType.Others;
      if (currency == Currency.BRL && isFreeForm && !B3Pattern.IsMatch(symbol))
      {
        return Unknown(allocationType, $"'{symbol}' is not a B3 ticker; no quote lookup.");
      }

      // BRL currency → B3
      if (currency == Currency.BRL)
      {
        return new AssetQuoteTarget
        {
          Key = QuoteKey.ForSymbol(allocationType, symbol),
          Strategy = QuoteFetchStrategy.B3,
          Symbol = symbol,
          QuoteCurrency = Currency.BRL,
        };
      }

      // Symbol matches B3 pattern → B3 even if currency is USD (BDRs sometimes)
      if (B3Pattern.IsMatch(symbol))
      {
        return new AssetQuoteTarget
        {
          Key = QuoteKey.ForSymbol(allocationType, symbol),
          Strategy = QuoteFetchStrategy.B3,
          Symbol = symbol,
          QuoteCurrency = Currency.BRL,
        };
      }

      // Otherwise treat as US / international market
      return new AssetQuoteTarget
      {
        Key = QuoteKey.ForSymbol(allocationType, symbol),
        Strategy = QuoteFetchStrategy.UsMarket,
        Symbol = symbol,
        QuoteCurrency = Currency.USD,
      };
    }

    // ── Fallback for any other unrecognised type ───────────────────────────
    return Unknown(allocationType, $"No resolution strategy for {allocationType}.");
  }

  /// <summary>
  /// Returns true for types that are hardcoded in the static AssetCatalog.
  /// </summary>
  public static bool IsHardcoded(AllocationType type) => HardcodedTypes.Contains(type);

  /// <summary>
  /// Returns true for fixed-income types that never have a market price.
  /// </summary>
  public static bool IsFixedIncome(AllocationType type) => FixedIncomeTypes.Contains(type);

  private static AssetQuoteTarget Unknown(AllocationType type, string note) =>
    new()
    {
      Key = QuoteKey.ForType(type),
      Strategy = QuoteFetchStrategy.Unknown,
      Note = note,
    };
}
