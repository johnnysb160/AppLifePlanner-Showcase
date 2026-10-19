//-----------------------------------------------------------------------
// <copyright file="MarketQuoteFetcher.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Provides methods to fetch market quotes for B3, US market, and generic crypto assets.
/// All fetchers follow the same pattern: multiple sources tried in priority order,
/// returning the first successful result. Null means all sources failed.
/// </summary>
public sealed class MarketQuoteFetcher
{
  private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(12);

  private readonly IHttpClientFactory httpClientFactory;
  private readonly ILogger logger;
  private readonly string? brapiToken;
  private readonly string? finnhubApiKey;
  private readonly string? twelveDataApiKey;

  public MarketQuoteFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    string? brapiToken,
    string? finnhubApiKey,
    string? twelveDataApiKey)
  {
    this.httpClientFactory = httpClientFactory;
    this.logger = logger;
    this.brapiToken = brapiToken;
    this.finnhubApiKey = finnhubApiKey;
    this.twelveDataApiKey = twelveDataApiKey;
  }

  // ===========================================================================
  // B3 - Brazilian exchange (stocks, FIIs, BDRs, BR-ETFs)
  // Sources: brapi.dev -> Yahoo Finance BR (.SA suffix)
  // ===========================================================================

  /// <summary>
  /// Fetches a quote for a B3 asset identified by its ticker (e.g. "PETR4", "HGLG11").
  /// </summary>
  public async Task<QuoteResult> FetchB3Async(string ticker, CancellationToken ct)
  {
    string upper = ticker.Trim().ToUpperInvariant();

    // Source 1: brapi.dev
    QuoteResult? result = await TryAsync(
      () => this.FetchB3FromBrapiAsync(upper, ct), "brapi.dev", ct);
    if (result?.Success == true) return result;

    // Source 2: Yahoo Finance (symbol + ".SA")
    result = await TryAsync(
      () => this.FetchFromYahooFinanceAsync(upper + ".SA", ct), "YahooFinance/B3", ct);
    if (result?.Success == true) return result;

    return Fail("All B3 sources failed.", "AllFailed");
  }

  private async Task<QuoteResult> FetchB3FromBrapiAsync(string ticker, CancellationToken ct)
  {
    string url = string.IsNullOrEmpty(this.brapiToken)
      ? $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ticker)}?fundamental=false"
      : $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ticker)}?fundamental=false&token={Uri.EscapeDataString(this.brapiToken)}";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (!root.TryGetProperty("results", out JsonElement results) ||
        results.GetArrayLength() == 0)
    {
      return Fail("brapi.dev: no results.", "brapi.dev");
    }

    JsonElement first = results[0];

    // Check for error marker inside the result
    if (first.TryGetProperty("error", out JsonElement errEl) && errEl.GetBoolean())
    {
      string msg = first.TryGetProperty("message", out JsonElement msgEl)
        ? msgEl.GetString() ?? "unknown"
        : "unknown";
      return Fail($"brapi.dev error: {msg}", "brapi.dev");
    }

    if (!first.TryGetProperty("regularMarketPrice", out JsonElement priceEl) ||
        !priceEl.TryGetDecimal(out decimal price) || price <= 0)
    {
      return Fail("brapi.dev: missing or zero price.", "brapi.dev");
    }

    DateTime ts = DateTime.UtcNow;
    if (first.TryGetProperty("regularMarketTime", out JsonElement tsEl))
    {
      if (tsEl.ValueKind == JsonValueKind.Number && tsEl.TryGetInt64(out long epoch))
        ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
      else if (tsEl.ValueKind == JsonValueKind.String &&
               long.TryParse(tsEl.GetString(), out long epochStr) && epochStr > 0)
        ts = DateTimeOffset.FromUnixTimeSeconds(epochStr).UtcDateTime;
    }

    return new QuoteResult
    {
      Success = true,
      Price = price,
      QuoteTimestampUtc = ts,
      Source = "brapi.dev",
    };
  }

  // ===========================================================================
  // US Market - stocks and ETFs (Finnhub -> TwelveData -> Yahoo Finance -> Stooq)
  // ===========================================================================

  /// <summary>
  /// Fetches a quote for a US-listed stock or ETF identified by its ticker (e.g. "AAPL", "SPY").
  /// </summary>
  public async Task<QuoteResult> FetchUsMarketAsync(string ticker, CancellationToken ct)
  {
    string upper = ticker.Trim().ToUpperInvariant();

    // Source 1: Finnhub
    if (!string.IsNullOrEmpty(this.finnhubApiKey))
    {
      QuoteResult? r = await TryAsync(
        () => this.FetchFromFinnhubAsync(upper, ct), "Finnhub", ct);
      if (r?.Success == true) return r;
    }

    // Source 2: TwelveData
    if (!string.IsNullOrEmpty(this.twelveDataApiKey))
    {
      QuoteResult? r = await TryAsync(
        () => this.FetchFromTwelveDataStockAsync(upper, ct), "TwelveData", ct);
      if (r?.Success == true) return r;
    }

    // Source 3: Yahoo Finance (no key required)
    QuoteResult? yahoo = await TryAsync(
      () => this.FetchFromYahooFinanceAsync(upper, ct), "YahooFinance", ct);
    if (yahoo?.Success == true) return yahoo;

    // Source 4: Stooq (no key required)
    QuoteResult? stooq = await TryAsync(
      () => this.FetchFromStooqAsync(upper.ToLowerInvariant() + ".us", ct), "Stooq", ct);
    if (stooq?.Success == true) return stooq;

    return Fail("All US market sources failed.", "AllFailed");
  }

  private async Task<QuoteResult> FetchFromFinnhubAsync(string symbol, CancellationToken ct)
  {
    string url = $"https://finnhub.io/api/v1/quote?symbol={Uri.EscapeDataString(symbol)}&token={this.finnhubApiKey}";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (root.TryGetProperty("c", out JsonElement cEl) &&
        cEl.TryGetDecimal(out decimal price) && price > 0)
    {
      DateTime ts = DateTime.UtcNow;
      if (root.TryGetProperty("t", out JsonElement tEl) && tEl.TryGetInt64(out long epoch) && epoch > 0)
        ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "Finnhub" };
    }

    return Fail("Finnhub: missing or zero price.", "Finnhub");
  }

  private async Task<QuoteResult> FetchFromTwelveDataStockAsync(string symbol, CancellationToken ct)
  {
    string url = $"https://api.twelvedata.com/price?symbol={Uri.EscapeDataString(symbol)}&apikey={this.twelveDataApiKey}";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (root.TryGetProperty("price", out JsonElement priceEl) &&
        decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) &&
        price > 0)
    {
      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = DateTime.UtcNow, Source = "TwelveData" };
    }

    return Fail("TwelveData: missing or zero price.", "TwelveData");
  }

  // ===========================================================================
  // Yahoo Finance - universal fallback (works for US, B3 with .SA, ETFs, etc.)
  // Unofficial but free and broadly supported.
  // ===========================================================================

  private async Task<QuoteResult> FetchFromYahooFinanceAsync(string symbol, CancellationToken ct)
  {
    // v8 chart endpoint - JSON, no API key required
    string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d";

    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (!root.TryGetProperty("chart", out JsonElement chart) ||
        !chart.TryGetProperty("result", out JsonElement resultArr) ||
        resultArr.ValueKind != JsonValueKind.Array ||
        resultArr.GetArrayLength() == 0)
    {
      return Fail("Yahoo: empty result.", "YahooFinance");
    }

    JsonElement first = resultArr[0];

    // meta.regularMarketPrice is the most reliable field
    if (first.TryGetProperty("meta", out JsonElement meta) &&
        meta.TryGetProperty("regularMarketPrice", out JsonElement priceEl) &&
        priceEl.TryGetDecimal(out decimal price) && price > 0)
    {
      DateTime ts = DateTime.UtcNow;
      if (meta.TryGetProperty("regularMarketTime", out JsonElement tsEl) &&
          tsEl.TryGetInt64(out long epoch) && epoch > 0)
      {
        ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
      }

      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "YahooFinance" };
    }

    return Fail("Yahoo: could not parse price.", "YahooFinance");
  }

  private async Task<QuoteResult> FetchFromStooqAsync(string stooqSymbol, CancellationToken ct)
  {
    string url = $"https://stooq.com/q/l/?s={Uri.EscapeDataString(stooqSymbol)}&f=sd2t2ohlcv&h&e=csv";
    string csv = await HttpGetAsync(url, ct);

    // CSV: Date,Time,Open,High,Low,Close,Volume
    string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    if (lines.Length < 2) return Fail("Stooq: no data rows.", "Stooq");

    string[] cols = lines[1].Split(',');
    if (cols.Length < 6) return Fail("Stooq: unexpected CSV format.", "Stooq");

    // Close is column index 5
    if (!decimal.TryParse(cols[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal close) || close <= 0)
      return Fail("Stooq: invalid close price.", "Stooq");

    DateTime ts = DateTime.UtcNow;
    if (DateTime.TryParse(cols[0].Trim() + " " + cols[1].Trim(),
          CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
    {
      ts = parsed;
    }

    return new QuoteResult { Success = true, Price = close, QuoteTimestampUtc = ts, Source = "Stooq" };
  }

  // ===========================================================================
  // Crypto - generic (by symbol/id) via CoinGecko -> CoinCap -> Binance
  // ===========================================================================

  /// <summary>
  /// Fetches a quote for a generic cryptocurrency using its ticker symbol or common name.
  /// E.g. "BTC", "ETH", "SOL".
  /// </summary>
  public async Task<QuoteResult> FetchCryptoBySymbolAsync(string symbol, CancellationToken ct)
  {
    string lower = symbol.Trim().ToLowerInvariant();

    // Source 1: CoinGecko - try symbol as ID first, then map via /search
    QuoteResult? r = await TryAsync(
      () => this.FetchCryptoFromCoinGeckoByIdAsync(lower, ct), "CoinGecko", ct);
    if (r?.Success == true) return r;

    // Source 2: CoinCap
    r = await TryAsync(
      () => this.FetchCryptoFromCoinCapAsync(lower, ct), "CoinCap", ct);
    if (r?.Success == true) return r;

    // Source 3: Binance (ticker + USDT pair)
    string binanceSymbol = symbol.Trim().ToUpperInvariant() + "USDT";
    r = await TryAsync(
      () => this.FetchCryptoFromBinanceAsync(binanceSymbol, ct), "Binance", ct);
    if (r?.Success == true) return r;

    return Fail($"All crypto sources failed for symbol '{symbol}'.", "AllFailed");
  }

  private async Task<QuoteResult> FetchCryptoFromCoinGeckoByIdAsync(string coinId, CancellationToken ct)
  {
    // Try coinId as a CoinGecko ID directly
    string url = $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(coinId)}&vs_currencies=usd&include_last_updated_at=true";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (root.TryGetProperty(coinId, out JsonElement coinData) &&
        coinData.TryGetProperty("usd", out JsonElement priceEl) &&
        priceEl.TryGetDecimal(out decimal price) && price > 0)
    {
      DateTime ts = DateTime.UtcNow;
      if (coinData.TryGetProperty("last_updated_at", out JsonElement tsEl) &&
          tsEl.TryGetInt64(out long epoch) && epoch > 0)
      {
        ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
      }

      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "CoinGecko" };
    }

    // Try again using CoinGecko /search to resolve symbol -> id
    return await this.FetchCryptoFromCoinGeckoViaSearchAsync(coinId, ct);
  }

  private async Task<QuoteResult> FetchCryptoFromCoinGeckoViaSearchAsync(string symbol, CancellationToken ct)
  {
    string searchUrl = $"https://api.coingecko.com/api/v3/search?query={Uri.EscapeDataString(symbol)}";
    string searchJson = await HttpGetAsync(searchUrl, ct);

    using JsonDocument searchDoc = JsonDocument.Parse(searchJson);
    JsonElement searchRoot = searchDoc.RootElement;

    string? resolvedId = null;
    if (searchRoot.TryGetProperty("coins", out JsonElement coins) && coins.GetArrayLength() > 0)
    {
      foreach (JsonElement coin in coins.EnumerateArray())
      {
        if (coin.TryGetProperty("symbol", out JsonElement symEl) &&
            string.Equals(symEl.GetString(), symbol, StringComparison.OrdinalIgnoreCase))
        {
          resolvedId = coin.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
          break;
        }
      }
    }

    if (string.IsNullOrEmpty(resolvedId))
      return Fail($"CoinGecko search: no coin found for '{symbol}'.", "CoinGecko");

    return await this.FetchCryptoFromCoinGeckoByIdAsync(resolvedId, ct);
  }

  private async Task<QuoteResult> FetchCryptoFromCoinCapAsync(string coinId, CancellationToken ct)
  {
    string url = $"https://api.coincap.io/v2/assets/{Uri.EscapeDataString(coinId)}";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (root.TryGetProperty("data", out JsonElement data) &&
        data.TryGetProperty("priceUsd", out JsonElement priceEl) &&
        decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) &&
        price > 0)
    {
      DateTime ts = DateTime.UtcNow;
      if (data.TryGetProperty("time", out JsonElement tsEl) && tsEl.TryGetInt64(out long ms) && ms > 0)
        ts = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "CoinCap" };
    }

    return Fail($"CoinCap: no data for '{coinId}'.", "CoinCap");
  }

  private async Task<QuoteResult> FetchCryptoFromBinanceAsync(string pairSymbol, CancellationToken ct)
  {
    string url = $"https://api.binance.com/api/v3/ticker/price?symbol={Uri.EscapeDataString(pairSymbol)}";
    string json = await HttpGetAsync(url, ct);

    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    if (root.TryGetProperty("price", out JsonElement priceEl) &&
        decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) &&
        price > 0)
    {
      return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = DateTime.UtcNow, Source = "Binance" };
    }

    return Fail($"Binance: no price for '{pairSymbol}'.", "Binance");
  }

  // ===========================================================================
  // Helpers
  // ===========================================================================

  private async Task<string> HttpGetAsync(string url, CancellationToken ct)
  {
    using HttpClient client = this.httpClientFactory.CreateClient();
    client.Timeout = HttpTimeout;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AppLifePlanner/2.0");
    HttpResponseMessage response = await client.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
  }

  private async Task<QuoteResult?> TryAsync(
    Func<Task<QuoteResult>> action,
    string source,
    CancellationToken ct)
  {
    try
    {
      return await action();
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      this.logger.LogWarning("{Source} fetch failed: {Message}", source, ex.Message);
      return null;
    }
  }

  private static QuoteResult Fail(string error, string source) =>
    new() { Success = false, Error = error, Source = source };
}
