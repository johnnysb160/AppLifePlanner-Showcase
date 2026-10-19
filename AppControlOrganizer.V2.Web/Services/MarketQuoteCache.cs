//-----------------------------------------------------------------------
// <copyright file="MarketQuoteCache.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using System.Globalization;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Represents a snapshot of the latest successful market quote for an asset.
/// The key is (AllocationType, Symbol) — Symbol is null for hardcoded specific types.
/// </summary>
public sealed class MarketQuoteSnapshot
{
  public AllocationType AllocationType { get; init; }

  /// <summary>
  /// Ticker symbol for dynamic assets (PETR4, AAPL, bitcoin…).
  /// Null for hardcoded specific-type enums (BTC, IBM, SPY…).
  /// </summary>
  public string? Symbol { get; init; }

  public Currency QuoteCurrency { get; init; }
  public decimal Price { get; init; }
  public DateTime RetrievedAtUtc { get; init; }
  public DateTime? QuoteTimestampUtc { get; init; }
  public string Source { get; init; } = string.Empty;
}

/// <summary>
/// Provides cached access to the latest successful market quotes from the database.
/// Keyed by <see cref="QuoteKey"/> which is (AllocationType, Symbol?).
/// </summary>
public sealed class MarketQuoteCache
{
  private const string CacheKey = "MarketQuotes.Latest.Success.v2";
  private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
  private static readonly TimeSpan DataRetentionWindow = TimeSpan.FromDays(31);

  private readonly IMemoryCache memoryCache;
  private readonly IDbContextFactory<Data.LifePlannerContext> contextFactory;
  private readonly ILogger<MarketQuoteCache> logger;

  public MarketQuoteCache(
    IMemoryCache memoryCache,
    IDbContextFactory<Data.LifePlannerContext> contextFactory,
    ILogger<MarketQuoteCache> logger)
  {
    this.memoryCache = memoryCache;
    this.contextFactory = contextFactory;
    this.logger = logger;
  }

  /// <summary>
  /// Gets the latest successful market quote dictionary, keyed by QuoteKey.
  /// </summary>
  public async Task<IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot>> GetLatestSuccessQuotesAsync(
    CancellationToken cancellationToken)
  {
    if (this.memoryCache.TryGetValue(
          CacheKey,
          out IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot>? cached) && cached != null)
    {
      return cached;
    }

    Dictionary<QuoteKey, MarketQuoteSnapshot> result = await this.LoadFromDatabaseAsync(cancellationToken);

    this.memoryCache.Set(
      CacheKey,
      (IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot>)result,
      new MemoryCacheEntryOptions().SetAbsoluteExpiration(CacheExpiration));

    return result;
  }

  /// <summary>
  /// Invalidates the quote cache so the next call reloads from the database.
  /// </summary>
  public void Invalidate() => this.memoryCache.Remove(CacheKey);

  // ── Backward-compat helpers for consumers that query by AllocationType only ──

  /// <summary>
  /// Tries to find a quote using just AllocationType (for hardcoded types where Symbol is null).
  /// </summary>
  public static bool TryGetByType(
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    AllocationType type,
    out MarketQuoteSnapshot? snapshot)
  {
    return quotes.TryGetValue(QuoteKey.ForType(type), out snapshot);
  }

  /// <summary>
  /// Tries to find a quote using (AllocationType, Symbol).
  /// Falls back to (AllocationType, null) for hardcoded types.
  /// </summary>
  public static bool TryGetForAllocation(
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes,
    AllocationType type,
    string? initials,
    out MarketQuoteSnapshot? snapshot)
  {
    // Try dynamic key first
    if (!string.IsNullOrWhiteSpace(initials))
    {
      QuoteKey dynamicKey = QuoteKey.ForSymbol(type, initials);
      if (quotes.TryGetValue(dynamicKey, out snapshot))
      {
        return true;
      }
    }

    // Fall back to type-only key (hardcoded specific types)
    return quotes.TryGetValue(QuoteKey.ForType(type), out snapshot);
  }

  private async Task<Dictionary<QuoteKey, MarketQuoteSnapshot>> LoadFromDatabaseAsync(
    CancellationToken cancellationToken)
  {
    Dictionary<QuoteKey, MarketQuoteSnapshot> result = new();

    try
    {
      await using Data.LifePlannerContext context =
        await this.contextFactory.CreateDbContextAsync(cancellationToken);

      DateTime cutoffUtc = DateTime.UtcNow.Subtract(DataRetentionWindow);
      string cutoffValue = cutoffUtc
        .ToUniversalTime()
        .ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

      List<MarketQuoteEntity> quotes = await context.MarketQuotes
        .FromSqlInterpolated($"""
          SELECT DISTINCT ON ("AllocationType", COALESCE(UPPER("Symbol"), ''))
            "Id",
            "AllocationType",
            "Symbol",
            "QuoteCurrency",
            "Price",
            "RetrievedAtUtc",
            "QuoteTimestampUtc",
            "Source",
            "Status",
            "Error",
            "Warning",
            "RefId",
            "ExternalId",
            "Active",
            "Deleted",
            "CreationDate"
          FROM "MarketQuotes"
          WHERE "Status" = {(int)MarketQuoteStatus.Success}
            AND "Price" IS NOT NULL
            AND "RetrievedAtUtc" >= {cutoffValue}
          ORDER BY
            "AllocationType",
            COALESCE(UPPER("Symbol"), ''),
            COALESCE("QuoteTimestampUtc", "RetrievedAtUtc") DESC,
            "RetrievedAtUtc" DESC
          """)
        .AsNoTracking()
        .ToListAsync(cancellationToken);

      foreach (MarketQuoteEntity quote in quotes)
      {
        if (quote.Price == null)
        {
          continue;
        }

        string? symbol = string.IsNullOrWhiteSpace(quote.Symbol)
          ? null
          : quote.Symbol.ToUpperInvariant();
        QuoteKey key = symbol == null
          ? QuoteKey.ForType(quote.AllocationType)
          : QuoteKey.ForSymbol(quote.AllocationType, symbol);

        result[key] = new MarketQuoteSnapshot
        {
          AllocationType = quote.AllocationType,
          Symbol = quote.Symbol,
          QuoteCurrency = quote.QuoteCurrency,
          Price = quote.Price.Value,
          RetrievedAtUtc = quote.RetrievedAtUtc,
          QuoteTimestampUtc = quote.QuoteTimestampUtc,
          Source = quote.Source,
        };
      }

      this.logger.LogDebug(
        "Loaded {Count} latest successful market quotes from database.", result.Count);
    }
    catch (Exception ex)
    {
      this.logger.LogError(ex, "Error loading market quotes from database. Returning empty dictionary.");
    }

    return result;
  }
}
