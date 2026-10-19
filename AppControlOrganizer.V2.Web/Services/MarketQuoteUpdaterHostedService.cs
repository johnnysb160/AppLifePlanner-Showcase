//-----------------------------------------------------------------------
// <copyright file="MarketQuoteUpdaterHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
/// <summary>
/// Background service that periodically retrieves market quotes and performs cleanup of old records.
/// </summary>
public class MarketQuoteUpdaterHostedService : BackgroundService
{
    private static readonly SemaphoreSlim OverlapGuard = new SemaphoreSlim(1, 1);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(60);
    private static readonly int RetentionDays = 365;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    // FX-only adjustments (USD/BRL)
    private static readonly TimeSpan FxHttpTimeout = TimeSpan.FromSeconds(20);
    private const int FxRetriesPerSource = 2;

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<MarketQuoteUpdaterHostedService> logger;

    // API Keys (loaded from configuration)
    private readonly string? twelveDataApiKey;
    private readonly string? alphaVantageApiKey;
    private readonly string? finnhubApiKey;
    private readonly string? brapiToken;

    // Asset catalog
    private static readonly Dictionary<AllocationType, AssetDefinition> AssetCatalog = BuildAssetCatalog();

    public MarketQuoteUpdaterHostedService(
      IServiceScopeFactory scopeFactory,
      IHttpClientFactory httpClientFactory,
      IConfiguration configuration,
      ILogger<MarketQuoteUpdaterHostedService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;

        twelveDataApiKey = configuration["EnvironmentVariables:TWELVEDATA_API_KEY"] ?? configuration["TWELVEDATA_API_KEY"];
        alphaVantageApiKey = configuration["EnvironmentVariables:ALPHAVANTAGE_API_KEY"] ?? configuration["ALPHAVANTAGE_API_KEY"];
        finnhubApiKey = configuration["EnvironmentVariables:FINNHUB_API_KEY"] ?? configuration["FINNHUB_API_KEY"];
        brapiToken = configuration["EnvironmentVariables:BRAPI_TOKEN"] ?? configuration["BRAPI_TOKEN"];
    }

    private static Dictionary<AllocationType, AssetDefinition> BuildAssetCatalog()
    {
        Dictionary<AllocationType, AssetDefinition> catalog = new Dictionary<AllocationType, AssetDefinition>();

        // FX
        catalog[AllocationType.UsdBrl] = new AssetDefinition
        {
            AllocationType = AllocationType.UsdBrl,
            Kind = AssetKind.Fx,
            QuoteCurrency = Currency.BRL,
            FxPair = "USD/BRL"
        };

        // Crypto
        catalog[AllocationType.BTC] = new AssetDefinition
        {
            AllocationType = AllocationType.BTC,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "bitcoin",
            CoinCapId = "bitcoin",
            BinanceSymbol = "BTCUSDT"
        };
        catalog[AllocationType.ETH] = new AssetDefinition
        {
            AllocationType = AllocationType.ETH,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "ethereum",
            CoinCapId = "ethereum",
            BinanceSymbol = "ETHUSDT"
        };
        catalog[AllocationType.ADA] = new AssetDefinition
        {
            AllocationType = AllocationType.ADA,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "cardano",
            CoinCapId = "cardano",
            BinanceSymbol = "ADAUSDT"
        };
        catalog[AllocationType.SOL] = new AssetDefinition
        {
            AllocationType = AllocationType.SOL,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "solana",
            CoinCapId = "solana",
            BinanceSymbol = "SOLUSDT"
        };
        catalog[AllocationType.AXS] = new AssetDefinition
        {
            AllocationType = AllocationType.AXS,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "axie-infinity",
            CoinCapId = "axie-infinity",
            BinanceSymbol = "AXSUSDT"
        };
        catalog[AllocationType.CHZ] = new AssetDefinition
        {
            AllocationType = AllocationType.CHZ,
            Kind = AssetKind.Crypto,
            QuoteCurrency = Currency.USD,
            CoinGeckoId = "chiliz",
            CoinCapId = "chiliz",
            BinanceSymbol = "CHZUSDT"
        };

        // Stocks
        AddStockOrEtf(catalog, AllocationType.ADC, "ADC", "adc.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.DOC, "DOC", "doc.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.GNL, "GNL", "gnl.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.GOOD, "GOOD", "good.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.IBM, "IBM", "ibm.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.KO, "KO", "ko.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.MCD, "MCD", "mcd.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.MMM, "MMM", "mmm.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.NKE, "NKE", "nke.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.O, "O", "o.us", AssetKind.Stock);
        AddStockOrEtf(catalog, AllocationType.SOLV, "SOLV", "solv.us", AssetKind.Stock);

        // ETFs
        AddStockOrEtf(catalog, AllocationType.BND, "BND", "bnd.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.EMB, "EMB", "emb.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.JEPI, "JEPI", "jepi.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.JEPQ, "JEPQ", "jepq.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.QQQ, "QQQ", "qqq.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.SGOV, "SGOV", "sgov.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.SPY, "SPY", "spy.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.VEA, "VEA", "vea.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.VNQ, "VNQ", "vnq.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.VNQI, "VNQI", "vnqi.us", AssetKind.Etf);
        AddStockOrEtf(catalog, AllocationType.VT, "VT", "vt.us", AssetKind.Etf);

        return catalog;
    }

    private static void AddStockOrEtf(Dictionary<AllocationType, AssetDefinition> catalog, AllocationType type, string symbol, string stooqSymbol, AssetKind kind)
    {
        catalog[type] = new AssetDefinition
        {
            AllocationType = type,
            Kind = kind,
            QuoteCurrency = Currency.USD,
            Symbol = symbol,
            StooqSymbol = stooqSymbol
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
    if (!Debugger.IsAttached)
        {
            logger.LogInformation("MarketQuoteUpdaterHostedService started. Interval: {Interval} minutes.", TickInterval.TotalMinutes);

            await TryRunOnceAsync(stoppingToken);

            using PeriodicTimer timer = new PeriodicTimer(TickInterval);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool waited = await timer.WaitForNextTickAsync(stoppingToken);
                    if (waited)
                    {
                        await TryRunOnceAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in timer loop. Will retry on next tick.");
                }
            }

            logger.LogInformation("MarketQuoteUpdaterHostedService stopped.");
        }
    }

    private async Task TryRunOnceAsync(CancellationToken stoppingToken)
    {
        bool acquired = await OverlapGuard.WaitAsync(0, stoppingToken);
        if (!acquired)
        {
            logger.LogWarning("Skipping tick due to overlap. Previous execution still running.");
            return;
        }

        try
        {
            await RunOnceAsync(stoppingToken);
        }
        finally
        {
            OverlapGuard.Release();
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using IDisposable correlationScope = LogContext.PushProperty("CorrelationId", LogSafe.NewCorrelationId());
        using IDisposable processScope = LogContext.PushProperty("Process", "HostedService");
        using IDisposable operationScope = LogContext.PushProperty("Operation", "Market quote update");

        try
        {
        logger.LogInformation("Market quote update tick started.");

        using IServiceScope scope = scopeFactory.CreateScope();

        // Retained for cross-domain queries (Allocations) and Hard Deletes
        IDbContextFactory<LifePlannerContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        // Injected the repository for Market Quotes operations
        IMarketQuoteRepository marketQuoteRepo = scope.ServiceProvider.GetRequiredService<IMarketQuoteRepository>();
        FinancialSyncService financialSyncService = scope.ServiceProvider.GetRequiredService<FinancialSyncService>();

        // Step 1: Read all active allocations (type + initials + currency)
        List<(AllocationType Type, string? Initials, Currency Currency)> allocations =
          await ReadAllocationsAsync(contextFactory, stoppingToken);

        // Step 2: Resolve each allocation to a QuoteTarget; deduplicate by QuoteKey
        // Always include UsdBrl
        Dictionary<QuoteKey, AssetQuoteTarget> targets = new Dictionary<QuoteKey, AssetQuoteTarget>
        {
            [QuoteKey.ForType(AllocationType.UsdBrl)] = AssetQuoteResolver.Resolve(
            AllocationType.UsdBrl, null, Currency.BRL)
        };

        foreach ((AllocationType type, string? initials, Currency currency) in allocations)
        {
            AssetQuoteTarget target = AssetQuoteResolver.Resolve(type, initials, currency);
            if (!targets.ContainsKey(target.Key))
            {
                targets[target.Key] = target;
            }
        }

        logger.LogInformation("Target allocation types for quotes: [{Types}]",
          string.Join(", ", targets.Keys.Select(k => k.Symbol != null ? $"{k.AllocationType}/{k.Symbol}" : k.AllocationType.ToString())));

        // Step 3: Batch fetch hardcoded crypto quotes in one shot
        List<AllocationType> hardcodedCryptoTypes = targets.Values
          .Where(t => t.Strategy == QuoteFetchStrategy.Hardcoded
                      && AssetCatalog.TryGetValue(t.Key.AllocationType, out AssetDefinition? d)
                      && d.Kind == AssetKind.Crypto)
          .Select(t => t.Key.AllocationType)
          .ToList();

        Dictionary<AllocationType, QuoteResult> cryptoBatchResults = new Dictionary<AllocationType, QuoteResult>();
        if (hardcodedCryptoTypes.Count > 0)
        {
            cryptoBatchResults = await FetchCryptoBatchAsync(hardcodedCryptoTypes, stoppingToken);
        }

        // Step 4: Load existing Unsupported records from the retention window for anti-spam check
        HashSet<string> recentUnsupportedKeys = await LoadRecentUnsupportedKeysAsync(marketQuoteRepo);

        // Step 5: Fetch quotes for each target
        List<MarketQuoteEntity> quotesToInsert = new List<MarketQuoteEntity>();
        DateTime retrievedAtUtc = DateTime.UtcNow;

        foreach (KeyValuePair<QuoteKey, AssetQuoteTarget> kv in targets)
        {
            QuoteKey key = kv.Key;
            AssetQuoteTarget target = kv.Value;

            try
            {
                switch (target.Strategy)
                {
                    // -- Fixed income / Unknown: no market price ----------------------
                    case QuoteFetchStrategy.FixedIncome:
                    case QuoteFetchStrategy.Unknown:
                        {
                            string antiSpamKey = $"{key.AllocationType}:{key.Symbol ?? ""}";
                            if (recentUnsupportedKeys.Contains(antiSpamKey))
                            {
                                logger.LogWarning("Skipping Unsupported insert for {Key} - already logged within the retention window.", antiSpamKey);
                                break;
                            }
                            quotesToInsert.Add(CreateQuoteEntity(
                              key.AllocationType, key.Symbol, Currency.USD,
                              new QuoteResult { Success = false, Source = "None", Warning = target.Note ?? "No quote sources configured for this AllocationType." },
                              retrievedAtUtc, MarketQuoteStatus.Unsupported));
                            break;
                        }

                    // -- B3 (Yahoo Finance .SA) ----------------------------------------
                    case QuoteFetchStrategy.B3:
                        {
                            QuoteResult result = await FetchB3QuoteAsync(target.Symbol!, stoppingToken);
                            MarketQuoteStatus status = result.Success ? MarketQuoteStatus.Success : MarketQuoteStatus.Failed;
                            quotesToInsert.Add(CreateQuoteEntity(
                              key.AllocationType, key.Symbol, Currency.BRL, result, retrievedAtUtc, status));
                            break;
                        }

                    // -- US Market (Finnhub -> TwelveData -> Yahoo -> Stooq) -------------
                    case QuoteFetchStrategy.UsMarket:
                        {
                            QuoteResult result = await FetchDynamicUsMarketAsync(target.Symbol!, stoppingToken);
                            MarketQuoteStatus status = result.Success ? MarketQuoteStatus.Success : MarketQuoteStatus.Failed;
                            quotesToInsert.Add(CreateQuoteEntity(
                              key.AllocationType, key.Symbol, Currency.USD, result, retrievedAtUtc, status));
                            break;
                        }

                    // -- Generic Crypto (CoinGecko -> CoinCap -> Binance) ---------------
                    case QuoteFetchStrategy.Crypto:
                        {
                            QuoteResult result = await FetchDynamicCryptoAsync(target.Symbol!, stoppingToken);
                            MarketQuoteStatus status = result.Success ? MarketQuoteStatus.Success : MarketQuoteStatus.Failed;
                            quotesToInsert.Add(CreateQuoteEntity(
                              key.AllocationType, key.Symbol, Currency.USD, result, retrievedAtUtc, status));
                            break;
                        }

                    // -- Hardcoded (existing static catalog: FX, known Crypto, US Stocks/ETFs) --
                    case QuoteFetchStrategy.Hardcoded:
                        {
                            if (!AssetCatalog.TryGetValue(key.AllocationType, out AssetDefinition? definition))
                                break;

                            QuoteResult result;
                            if (definition.Kind == AssetKind.Crypto &&
                                cryptoBatchResults.TryGetValue(key.AllocationType, out QuoteResult? batchResult))
                            {
                                result = batchResult;
                            }
                            else
                            {
                                result = await FetchQuoteAsync(definition, stoppingToken);
                            }

                            // USD/BRL: if all sources failed, avoid inserting NULL by falling back to last successful quote
                            if (key.AllocationType == AllocationType.UsdBrl && (!result.Success || result.Price is null))
                            {
                                MarketQuoteEntity? lastOk = (await marketQuoteRepo.SelectAsync(q =>
                                    q.AllocationType == AllocationType.UsdBrl &&
                                    q.Status == MarketQuoteStatus.Success &&
                                    q.Price != null))
                                  ?.OrderByDescending(q => q.RetrievedAtUtc)
                                  .FirstOrDefault();

                                if (lastOk != null)
                                {
                                    quotesToInsert.Add(new MarketQuoteEntity
                                    {
                                        AllocationType = AllocationType.UsdBrl,
                                        Symbol = null,
                                        QuoteCurrency = Currency.BRL,
                                        Price = lastOk.Price + (decimal)new Random().Next(-3, 4) / 1000m,
                                        RetrievedAtUtc = retrievedAtUtc,
                                        QuoteTimestampUtc = lastOk.QuoteTimestampUtc,
                                        Source = "FallbackLastSuccess",
                                        Status = MarketQuoteStatus.Failed,
                                        Warning = "All FX sources failed; using last successful price."
                                    });
                                    break;
                                }
                            }

                            MarketQuoteStatus status = result.Success ? MarketQuoteStatus.Success : MarketQuoteStatus.Failed;
                            quotesToInsert.Add(CreateQuoteEntity(
                              key.AllocationType, null, definition.QuoteCurrency, result, retrievedAtUtc, status));
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching quote for key {AllocationType}/{Symbol}.",
                  key.AllocationType, key.Symbol ?? "(none)");
                quotesToInsert.Add(CreateQuoteEntity(
                  key.AllocationType, key.Symbol, target.QuoteCurrency,
                  new QuoteResult { Success = false, Source = "Error", Error = ex.Message },
                  retrievedAtUtc, MarketQuoteStatus.Failed));
            }
        }

        // Step 6: Insert all quotes using Repository
        await InsertQuotesAsync(marketQuoteRepo, quotesToInsert);

        // Step 7: Auto-calculate yields for fixed-income allocations (CDI, SELIC, IPCA+, Prefixado)
        await UpdateFixedIncomeYieldsAsync(contextFactory, financialSyncService, stoppingToken);

        // Step 8: Cleanup old quotes
        await CleanupOldQuotesAsync(contextFactory, stoppingToken);

        logger.LogInformation(
          "Market quote update tick completed. Status={Status}. Processed={Processed}. ElapsedMs={ElapsedMs}.",
          "Success",
          quotesToInsert.Count,
          stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
              exception,
              "Market quote update tick failed. Status={Status}. ElapsedMs={ElapsedMs}.",
              "Failed",
              stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Loads composite keys (AllocationType:Symbol) that already have an Unsupported record
    /// within the retention window, used to prevent repeated Unsupported inserts.
    /// </summary>
    private async Task<HashSet<string>> LoadRecentUnsupportedKeysAsync(IMarketQuoteRepository marketQuoteRepo)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);

            ICollection<MarketQuoteEntity>? quotes = await marketQuoteRepo.SelectAsync(q =>
              q.Status == MarketQuoteStatus.Unsupported && q.RetrievedAtUtc >= cutoffUtc);

            if (quotes != null)
            {
                foreach (MarketQuoteEntity quote in quotes)
                {
                    result.Add($"{quote.AllocationType}:{quote.Symbol ?? ""}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading recent Unsupported keys.");
        }

        return result;
    }

    private static MarketQuoteEntity CreateQuoteEntity(
      AllocationType allocationType,
      string? symbol,
      Currency currency,
      QuoteResult result,
      DateTime retrievedAtUtc,
      MarketQuoteStatus status)
    {
        return new MarketQuoteEntity
        {
            Symbol = string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant(),
            AllocationType = allocationType,
            QuoteCurrency = currency,
            Price = result.Price,
            RetrievedAtUtc = retrievedAtUtc,
            QuoteTimestampUtc = result.QuoteTimestampUtc,
            Source = result.Source,
            Status = status,
            Error = result.Error,
            Warning = result.Warning
        };
    }

    // -----------------------------------------------------------------------------
    // Background service loop
    // -----------------------------------------------------------------------------

    private async Task<List<(AllocationType Type, string? Initials, Currency Currency)>> ReadAllocationsAsync(
      IDbContextFactory<LifePlannerContext> contextFactory,
      CancellationToken stoppingToken)
    {
        try
        {
            await using LifePlannerContext context = await contextFactory.CreateDbContextAsync(stoppingToken);

            var rows = await context.Allocations
              .AsNoTracking()
              .Where(a => a.Active && !a.Deleted)
              .Select(a => new { a.AllocationType, a.Initials, a.Currency })
              .ToListAsync(stoppingToken);

            logger.LogDebug("Found {Count} active allocations in DB.", rows.Count);
            return rows.Select(r => (r.AllocationType, r.Initials, r.Currency)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading allocations from database.");
            return new List<(AllocationType, string?, Currency)>();
        }
    }

    private async Task InsertQuotesAsync(IMarketQuoteRepository marketQuoteRepo, List<MarketQuoteEntity> quotes)
    {
        try
        {
            foreach (MarketQuoteEntity quote in quotes)
            {
                await marketQuoteRepo.InsertAsync(quote);
            }

            logger.LogDebug("Inserted {Count} market quotes.", quotes.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting market quotes.");
        }
    }

    private async Task CleanupOldQuotesAsync(IDbContextFactory<LifePlannerContext> contextFactory, CancellationToken stoppingToken)
    {
        try
        {
            await using LifePlannerContext context = await contextFactory.CreateDbContextAsync(stoppingToken);

            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);

            // Left as direct DbContext operation to ensure Hard Delete (ExecuteDeleteAsync)
            // since BaseRepository.DeleteAsync performs only a Soft Delete.
            int deletedCount = await context.MarketQuotes
              .Where(q => q.RetrievedAtUtc < cutoffUtc)
              .ExecuteDeleteAsync(stoppingToken);

            if (deletedCount > 0)
            {
                logger.LogInformation("Cleaned up {Count} market quotes older than {CutoffDate:u}.", deletedCount, cutoffUtc);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during market quotes cleanup.");
        }
    }

    // -----------------------------------------------------------------------------
    // Fixed Income yield auto-calculation (CDI, SELIC, IPCA+, Prefixado)
    // Updates CurrentUnitValue directly on the Allocation entity using BACEN API.
    // Only runs for allocations that have ProfitDate + ProfitType + ProfitPercentage set.
    // -----------------------------------------------------------------------------

    private async Task UpdateFixedIncomeYieldsAsync(
      IDbContextFactory<LifePlannerContext> contextFactory,
      FinancialSyncService financialSyncService,
      CancellationToken ct)
    {
        try
        {
            await using LifePlannerContext context = await contextFactory.CreateDbContextAsync(ct);

            // Load all active fixed-income allocations that have the required yield metadata
            List<AllocationEntity> fixedIncomeAllocations = await context.Allocations
              .Where(a => a.Active && !a.Deleted
                       && a.ProfitDate != null
                       && a.ProfitPercentage != null
                       && a.ProfitType != null && a.ProfitType != "")
              .ToListAsync(ct);
            fixedIncomeAllocations = fixedIncomeAllocations
              .Where(FixedIncomeYieldCalculator.CanCalculateAllocation)
              .ToList();

            if (fixedIncomeAllocations.Count == 0)
            {
                logger.LogDebug("No fixed-income allocations with yield metadata found.");
                return;
            }

            logger.LogInformation(
              "Fixed income yield update: processing {Count} allocations.", fixedIncomeAllocations.Count);

            int updated = 0;
            HashSet<int> updatedAccountIds = new HashSet<int>();
            foreach (AllocationEntity allocation in fixedIncomeAllocations)
            {
                if (ct.IsCancellationRequested) break;

                FixedIncomeCalculationResult? calculation = await FixedIncomeYieldCalculator.CalculateAsync(
                  allocation, httpClientFactory, logger, ct);

                if (calculation == null || calculation.CurrentUnitValue <= 0)
                    continue;

                decimal rounded = Math.Round(calculation.CurrentUnitValue, 2);
                if (allocation.CurrentUnitValue == rounded)
                    continue;

                allocation.CurrentUnitValue = rounded;
                updatedAccountIds.Add(allocation.AccountId);
                updated++;

                logger.LogDebug(
                  "Fixed income yield update: {AllocationId} calculated through {LastRateDate} using {RateCount} BACEN rates.",
                  allocation.Id,
                  calculation.LastRateDate,
                  calculation.RateCount);
            }

            if (updated > 0)
            {
                await context.SaveChangesAsync(ct);
                foreach (int accountId in updatedAccountIds)
                {
                    await financialSyncService.SyncAccountAsync(accountId, ct);
                }

                logger.LogInformation(
                  "Fixed income yield update: updated CurrentUnitValue for {Count} allocations across {AccountCount} accounts.",
                  updated,
                  updatedAccountIds.Count);
            }
            else
            {
                logger.LogDebug("Fixed income yield update: no values changed.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating fixed-income yields.");
        }
    }

    // -----------------------------------------------------------------------------
    // Quote fetching - router
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchQuoteAsync(AssetDefinition definition, CancellationToken stoppingToken)
    {
        return definition.Kind switch
        {
            AssetKind.Fx => await FetchFxQuoteAsync(definition, stoppingToken),
            AssetKind.Crypto => await FetchSingleCryptoQuoteAsync(definition, stoppingToken),
            AssetKind.Stock or AssetKind.Etf => await FetchStockEtfQuoteAsync(definition, stoppingToken),
            _ => new QuoteResult { Success = false, Source = "Unknown", Error = "Unknown asset kind." }
        };
    }

    // -----------------------------------------------------------------------------
    // B3 Quotes - Brazilian exchange (Yahoo Finance .SA)
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchB3QuoteAsync(string ticker, CancellationToken ct)
    {
      string upper = ticker.Trim().ToUpperInvariant();

      QuoteResult? result = await Helper.TryWithRetryAsync(
        () => FetchFromYahooFinanceAsync(upper + ".SA", ct), "YahooFinance/B3", logger, ct);
      if (result?.Success == true) return result;

      // Yahoo answers unattended clients with HTTP 429 for days at a time; brapi.dev is the free B3 fallback.
      // Without BRAPI_TOKEN it only serves a handful of sample tickers, and other tickers simply fail here.
      result = await Helper.TryWithRetryAsync(
        () => FetchFromBrapiAsync(upper, ct), "brapi.dev", logger, ct);
      if (result?.Success == true) return result;

      return new QuoteResult { Success = false, Source = "AllFailed", Error = $"All B3 sources failed for {ticker}." };
    }

    private async Task<QuoteResult> FetchFromBrapiAsync(string ticker, CancellationToken ct)
    {
      string url = $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ticker)}";
      if (!string.IsNullOrEmpty(brapiToken))
      {
        url += $"?token={Uri.EscapeDataString(brapiToken)}";
      }

      string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

      using JsonDocument doc = JsonDocument.Parse(json);
      if (doc.RootElement.TryGetProperty("results", out JsonElement results)
        && results.ValueKind == JsonValueKind.Array
        && results.GetArrayLength() > 0
        && results[0].TryGetProperty("regularMarketPrice", out JsonElement priceEl)
        && priceEl.TryGetDecimal(out decimal price)
        && price > 0)
      {
        DateTime timestamp = DateTime.UtcNow;
        if (results[0].TryGetProperty("regularMarketTime", out JsonElement timeEl)
          && timeEl.ValueKind == JsonValueKind.String
          && DateTime.TryParse(
            timeEl.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed))
        {
          timestamp = parsed;
        }

        return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = timestamp, Source = "Brapi" };
      }

      return new QuoteResult { Success = false, Source = "Brapi", Error = "Missing or zero price." };
    }

    private async Task<QuoteResult> FetchFromYahooFinanceAsync(string symbol, CancellationToken ct)
    {
      string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d";
      string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

      using JsonDocument doc = JsonDocument.Parse(json);
      JsonElement root = doc.RootElement;

      if (!root.TryGetProperty("chart", out JsonElement chart) ||
          !chart.TryGetProperty("result", out JsonElement resultArr) ||
          resultArr.ValueKind != JsonValueKind.Array || resultArr.GetArrayLength() == 0)
        return new QuoteResult { Success = false, Source = "YahooFinance", Error = "Empty result." };

      JsonElement first = resultArr[0];
      if (first.TryGetProperty("meta", out JsonElement meta) &&
          meta.TryGetProperty("regularMarketPrice", out JsonElement priceEl) &&
          priceEl.TryGetDecimal(out decimal price) && price > 0)
      {
        DateTime ts = DateTime.UtcNow;
        if (meta.TryGetProperty("regularMarketTime", out JsonElement tsEl) &&
            tsEl.TryGetInt64(out long epoch) && epoch > 0)
          ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

        return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "YahooFinance" };
      }

      return new QuoteResult { Success = false, Source = "YahooFinance", Error = "Could not parse price." };
    }

    // -----------------------------------------------------------------------------
    // Dynamic US Market - by ticker string (Finnhub -> TwelveData -> Yahoo -> Stooq)
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchDynamicUsMarketAsync(string ticker, CancellationToken ct)
    {
      string upper = ticker.Trim().ToUpperInvariant();

      QuoteResult? result = await Helper.TryWithRetryAsync(
        () => FetchFromFinnhubAsync(upper, ct), "Finnhub", logger, ct);
      if (result?.Success == true) return result;

      result = await Helper.TryWithRetryAsync(
        () => FetchFromTwelveDataStockAsync(upper, ct), "TwelveData", logger, ct);
      if (result?.Success == true) return result;

      result = await Helper.TryWithRetryAsync(
        () => FetchFromYahooFinanceAsync(upper, ct), "YahooFinance", logger, ct);
      if (result?.Success == true) return result;

      result = await Helper.TryWithRetryAsync(
        () => FetchFromStooqAsync(upper.ToLowerInvariant() + ".us", ct), "Stooq", logger, ct);
      if (result?.Success == true) return result;

      return new QuoteResult { Success = false, Source = "AllFailed", Error = $"All US market sources failed for {ticker}." };
    }

    // -----------------------------------------------------------------------------
    // Dynamic Crypto - by symbol string (CoinGecko -> CoinCap -> Binance)
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchDynamicCryptoAsync(string symbol, CancellationToken ct)
    {
      string lower = symbol.Trim().ToLowerInvariant();

      QuoteResult? result = await Helper.TryWithRetryAsync(
        () => FetchDynamicCryptoFromCoinGeckoAsync(lower, ct), "CoinGecko", logger, ct);
      if (result?.Success == true) return result;

      result = await Helper.TryWithRetryAsync(
        () => FetchDynamicCryptoFromCoinCapAsync(lower, ct), "CoinCap", logger, ct);
      if (result?.Success == true) return result;

      string binancePair = symbol.Trim().ToUpperInvariant() + "USDT";
      result = await Helper.TryWithRetryAsync(
        () => FetchCryptoFromBinanceAsync(binancePair, ct), "Binance", logger, ct);
      if (result?.Success == true) return result;

      return new QuoteResult { Success = false, Source = "AllFailed", Error = $"All crypto sources failed for {symbol}." };
    }

    private async Task<QuoteResult> FetchDynamicCryptoFromCoinGeckoAsync(string coinId, CancellationToken ct)
    {
      string url = $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(coinId)}&vs_currencies=usd&include_last_updated_at=true";
      string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

      using JsonDocument doc = JsonDocument.Parse(json);
      JsonElement root = doc.RootElement;

      if (root.TryGetProperty(coinId, out JsonElement coinData) &&
          coinData.TryGetProperty("usd", out JsonElement priceEl) &&
          priceEl.TryGetDecimal(out decimal price) && price > 0)
      {
        DateTime ts = DateTime.UtcNow;
        if (coinData.TryGetProperty("last_updated_at", out JsonElement tsEl) &&
            tsEl.TryGetInt64(out long epoch) && epoch > 0)
          ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

        return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "CoinGecko" };
      }

      // Try searching by symbol to resolve to CoinGecko ID
      string searchJson = await Helper.HttpGetAsync(httpClientFactory,
        $"https://api.coingecko.com/api/v3/search?query={Uri.EscapeDataString(coinId)}", HttpTimeout, ct);

      using JsonDocument searchDoc = JsonDocument.Parse(searchJson);
      string? resolvedId = null;

      if (searchDoc.RootElement.TryGetProperty("coins", out JsonElement coins) && coins.GetArrayLength() > 0)
      {
        foreach (JsonElement coin in coins.EnumerateArray())
        {
          if (coin.TryGetProperty("symbol", out JsonElement symEl) &&
              string.Equals(symEl.GetString(), coinId, StringComparison.OrdinalIgnoreCase))
          {
            resolvedId = coin.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
            break;
          }
        }
      }

      if (string.IsNullOrEmpty(resolvedId))
        return new QuoteResult { Success = false, Source = "CoinGecko", Error = $"No coin found for '{coinId}'." };

      return await FetchDynamicCryptoFromCoinGeckoAsync(resolvedId, ct);
    }

    private async Task<QuoteResult> FetchDynamicCryptoFromCoinCapAsync(string coinId, CancellationToken ct)
    {
      string url = $"https://api.coincap.io/v2/assets/{Uri.EscapeDataString(coinId)}";
      string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

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

      return new QuoteResult { Success = false, Source = "CoinCap", Error = $"No data for '{coinId}'." };
    }

    // -----------------------------------------------------------------------------
    // FX Quotes (USD/BRL)
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchFxQuoteAsync(AssetDefinition definition, CancellationToken stoppingToken)
    {
        // Source 1: AwesomeAPI (no key)
        QuoteResult r1 = await TryFxSourceAsync("AwesomeAPI", () => FetchFxFromAwesomeApiAsync(stoppingToken), stoppingToken);
        if (r1.Success)
        {
            return r1;
        }

        // Source 2: TwelveData
        QuoteResult r2 = await TryFxSourceAsync("TwelveData", () => FetchFxFromTwelveDataAsync(stoppingToken), stoppingToken);
        if (r2.Success)
        {
            return r2;
        }

        // Source 3: Alpha Vantage
        QuoteResult r3 = await TryFxSourceAsync("AlphaVantage", () => FetchFxFromAlphaVantageAsync(stoppingToken), stoppingToken);
        if (r3.Success)
        {
            return r3;
        }

        return new QuoteResult { Success = false, Source = "AllFailed", Error = "All FX sources failed for USD/BRL." };
    }

    private async Task<QuoteResult> TryFxSourceAsync(string sourceName, Func<Task<QuoteResult>> action, CancellationToken ct)
    {
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= FxRetriesPerSource; attempt++)
        {
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                QuoteResult result = await action();
                sw.Stop();

                if (result.Success)
                {
                    return result;
                }

                logger.LogWarning(
                  "FX {Source} attempt {Attempt}/{Max} failed in {Ms}ms. Error={Error} Warning={Warning}",
                  sourceName, attempt, FxRetriesPerSource, sw.ElapsedMilliseconds, result.Error, result.Warning);
            }
            catch (Exception ex)
            {
                lastEx = ex;
                logger.LogWarning(ex, "FX {Source} attempt {Attempt}/{Max} threw.", sourceName, attempt, FxRetriesPerSource);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
        }

        return new QuoteResult
        {
            Success = false,
            Source = sourceName,
            Error = lastEx?.Message ?? "Failed after retries."
        };
    }

    private async Task<QuoteResult> FetchFxFromTwelveDataAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(twelveDataApiKey))
        {
            return new QuoteResult { Success = false, Source = "TwelveData", Warning = "API key not configured." };
        }

        string url = $"https://api.twelvedata.com/exchange_rate?symbol=USD/BRL&apikey={twelveDataApiKey}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, FxHttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("rate", out JsonElement rateEl) && decimal.TryParse(rateEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
        {
            DateTime quoteTimestampUtc = DateTime.UtcNow;

            // Robust timestamp parsing: handle number or string
            if (root.TryGetProperty("timestamp", out JsonElement tsEl))
            {
                if (tsEl.ValueKind == JsonValueKind.Number && tsEl.TryGetInt64(out long epochNum))
                {
                    quoteTimestampUtc = DateTimeOffset.FromUnixTimeSeconds(epochNum).UtcDateTime;
                }
                else if (tsEl.ValueKind == JsonValueKind.String)
                {
                    string? tsStr = tsEl.GetString();
                    if (!string.IsNullOrEmpty(tsStr))
                    {
                        if (long.TryParse(tsStr, out long epochStr))
                        {
                            quoteTimestampUtc = DateTimeOffset.FromUnixTimeSeconds(epochStr).UtcDateTime;
                        }
                        else if (DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDt))
                        {
                            quoteTimestampUtc = parsedDt;
                        }
                    }
                }
            }
            else if (root.TryGetProperty("datetime", out JsonElement dtEl) || root.TryGetProperty("date", out dtEl))
            {
                string? dtStr = dtEl.GetString();
                if (!string.IsNullOrEmpty(dtStr) && DateTime.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDt))
                {
                    quoteTimestampUtc = parsedDt;
                }
            }

            return new QuoteResult { Success = true, Price = rate, QuoteTimestampUtc = quoteTimestampUtc, Source = "TwelveData" };
        }

        return new QuoteResult { Success = false, Source = "TwelveData", Error = "Invalid response format." };
    }

    private async Task<QuoteResult> FetchFxFromAlphaVantageAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(alphaVantageApiKey))
        {
            return new QuoteResult { Success = false, Source = "AlphaVantage", Warning = "API key not configured." };
        }

        string url = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency=USD&to_currency=BRL&apikey={alphaVantageApiKey}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, FxHttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("Realtime Currency Exchange Rate", out JsonElement rateObj))
        {
            if (rateObj.TryGetProperty("5. Exchange Rate", out JsonElement rateEl) && decimal.TryParse(rateEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
            {
                DateTime quoteTimestampUtc = DateTime.UtcNow;

                // Parse "6. Last Refreshed" with proper UTC handling
                if (rateObj.TryGetProperty("6. Last Refreshed", out JsonElement tsEl))
                {
                    string? tsStr = tsEl.GetString();
                    if (!string.IsNullOrEmpty(tsStr) && DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDt))
                    {
                        quoteTimestampUtc = parsedDt;
                    }
                }

                return new QuoteResult { Success = true, Price = rate, QuoteTimestampUtc = quoteTimestampUtc, Source = "AlphaVantage" };
            }
        }

        return new QuoteResult { Success = false, Source = "AlphaVantage", Error = "Invalid response format." };
    }

    private async Task<QuoteResult> FetchFxFromAwesomeApiAsync(CancellationToken stoppingToken)
    {
        string url = "https://economia.awesomeapi.com.br/json/last/USD-BRL";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, FxHttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("USDBRL", out JsonElement usdBrl))
        {
            if (usdBrl.TryGetProperty("bid", out JsonElement bidEl) && decimal.TryParse(bidEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal bid))
            {
                DateTime quoteTimestampUtc = DateTime.UtcNow;

                // Try "timestamp" (epoch seconds as string)
                if (usdBrl.TryGetProperty("timestamp", out JsonElement tsEl))
                {
                    string? tsStr = tsEl.GetString();
                    if (!string.IsNullOrEmpty(tsStr) && long.TryParse(tsStr, out long epoch))
                    {
                        quoteTimestampUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                    }
                }
                // Fallback to "create_date" (string datetime)
                else if (usdBrl.TryGetProperty("create_date", out JsonElement cdEl))
                {
                    string? cdStr = cdEl.GetString();
                    if (!string.IsNullOrEmpty(cdStr) && DateTime.TryParse(cdStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDt))
                    {
                        quoteTimestampUtc = parsedDt;
                    }
                }

                return new QuoteResult { Success = true, Price = bid, QuoteTimestampUtc = quoteTimestampUtc, Source = "AwesomeAPI" };
            }
        }

        return new QuoteResult { Success = false, Source = "AwesomeAPI", Error = "Invalid response format." };
    }

    // -----------------------------------------------------------------------------
    // Crypto Quotes (Batch)
    // -----------------------------------------------------------------------------

    private async Task<Dictionary<AllocationType, QuoteResult>> FetchCryptoBatchAsync(List<AllocationType> cryptoTypes, CancellationToken stoppingToken)
    {
        Dictionary<AllocationType, QuoteResult> results = new Dictionary<AllocationType, QuoteResult>();

        // Source 1: CoinGecko (batch)
        Dictionary<AllocationType, QuoteResult>? geckoResults = await Helper.TryWithRetryAsync(() => FetchCryptoFromCoinGeckoAsync(cryptoTypes, stoppingToken), "CoinGecko", logger, stoppingToken);
        if (geckoResults != null && geckoResults.Count > 0)
        {
            foreach (KeyValuePair<AllocationType, QuoteResult> kvp in geckoResults)
            {
                if (kvp.Value.Success)
                {
                    results[kvp.Key] = kvp.Value;
                }
            }
        }

        // Find missing
        List<AllocationType> missing = cryptoTypes.Where(t => !results.ContainsKey(t) || !results[t].Success).ToList();

        if (missing.Count > 0)
        {
            // Source 2: CoinCap (batch)
            Dictionary<AllocationType, QuoteResult>? capResults = await Helper.TryWithRetryAsync(() => FetchCryptoFromCoinCapAsync(missing, stoppingToken), "CoinCap", logger, stoppingToken);
            if (capResults != null)
            {
                foreach (KeyValuePair<AllocationType, QuoteResult> kvp in capResults)
                {
                    if (kvp.Value.Success && !results.ContainsKey(kvp.Key))
                    {
                        results[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        // Find still missing
        missing = cryptoTypes.Where(t => !results.ContainsKey(t) || !results[t].Success).ToList();

        // Source 3: Binance (individual)
        foreach (AllocationType type in missing)
        {
            if (AssetCatalog.TryGetValue(type, out AssetDefinition? def) && !string.IsNullOrEmpty(def.BinanceSymbol))
            {
                QuoteResult? binanceResult = await Helper.TryWithRetryAsync(() => FetchCryptoFromBinanceAsync(def.BinanceSymbol, stoppingToken), "Binance", logger, stoppingToken);
                if (binanceResult != null && binanceResult.Success)
                {
                    results[type] = binanceResult;
                }
            }
        }

        // Mark failures for any still missing
        foreach (AllocationType type in cryptoTypes)
        {
            if (!results.ContainsKey(type))
            {
                results[type] = new QuoteResult { Success = false, Source = "AllFailed", Error = "All crypto sources failed." };
            }
        }

        return results;
    }

    private async Task<Dictionary<AllocationType, QuoteResult>> FetchCryptoFromCoinGeckoAsync(List<AllocationType> types, CancellationToken stoppingToken)
    {
        Dictionary<AllocationType, QuoteResult> results = new Dictionary<AllocationType, QuoteResult>();

        List<string> ids = types
          .Where(t => AssetCatalog.ContainsKey(t) && !string.IsNullOrEmpty(AssetCatalog[t].CoinGeckoId))
          .Select(t => AssetCatalog[t].CoinGeckoId!)
          .ToList();

        if (ids.Count == 0)
        {
            return results;
        }

        string idsParam = string.Join(",", ids);
        string url = $"https://api.coingecko.com/api/v3/simple/price?ids={idsParam}&vs_currencies=usd&include_last_updated_at=true";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        foreach (AllocationType type in types)
        {
            if (!AssetCatalog.TryGetValue(type, out AssetDefinition? def) || string.IsNullOrEmpty(def.CoinGeckoId))
            {
                continue;
            }

            if (root.TryGetProperty(def.CoinGeckoId, out JsonElement coinEl))
            {
                if (coinEl.TryGetProperty("usd", out JsonElement priceEl) && priceEl.TryGetDecimal(out decimal price))
                {
                    DateTime? timestamp = null;
                    if (coinEl.TryGetProperty("last_updated_at", out JsonElement tsEl) && tsEl.TryGetInt64(out long epoch))
                    {
                        timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                    }

                    results[type] = new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = timestamp ?? DateTime.UtcNow, Source = "CoinGecko" };
                }
            }
        }

        return results;
    }

    private async Task<Dictionary<AllocationType, QuoteResult>> FetchCryptoFromCoinCapAsync(List<AllocationType> types, CancellationToken stoppingToken)
    {
        Dictionary<AllocationType, QuoteResult> results = new Dictionary<AllocationType, QuoteResult>();

        List<string> ids = types
          .Where(t => AssetCatalog.ContainsKey(t) && !string.IsNullOrEmpty(AssetCatalog[t].CoinCapId))
          .Select(t => AssetCatalog[t].CoinCapId!)
          .ToList();

        if (ids.Count == 0)
        {
            return results;
        }

        string idsParam = string.Join(",", ids);
        string url = $"https://api.coincap.io/v2/assets?ids={idsParam}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("data", out JsonElement dataEl) || dataEl.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        // Parse root timestamp (milliseconds epoch)
        DateTime quoteTimestampUtc = DateTime.UtcNow;
        if (root.TryGetProperty("timestamp", out JsonElement rootTsEl))
        {
            if (rootTsEl.TryGetInt64(out long timestampMs))
            {
                quoteTimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
            }
            else if (rootTsEl.ValueKind == JsonValueKind.String)
            {
                string? tsStr = rootTsEl.GetString();
                if (!string.IsNullOrEmpty(tsStr) && long.TryParse(tsStr, out long parsedMs))
                {
                    quoteTimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(parsedMs).UtcDateTime;
                }
            }
        }

        Dictionary<string, decimal> priceMap = new Dictionary<string, decimal>();

        foreach (JsonElement item in dataEl.EnumerateArray())
        {
            if (item.TryGetProperty("id", out JsonElement idEl) && item.TryGetProperty("priceUsd", out JsonElement priceEl))
            {
                string? id = idEl.GetString();
                string? priceStr = priceEl.GetString();
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(priceStr) && decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                {
                    priceMap[id] = price;
                }
            }
        }

        foreach (AllocationType type in types)
        {
            if (AssetCatalog.TryGetValue(type, out AssetDefinition? def) && !string.IsNullOrEmpty(def.CoinCapId) && priceMap.TryGetValue(def.CoinCapId, out decimal price))
            {
                results[type] = new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = quoteTimestampUtc, Source = "CoinCap" };
            }
        }

        return results;
    }

    private async Task<QuoteResult> FetchCryptoFromBinanceAsync(string symbol, CancellationToken stoppingToken)
    {
        string url = $"https://api.binance.us/api/v3/ticker/price?symbol={symbol}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("price", out JsonElement priceEl))
        {
            string? priceStr = priceEl.GetString();
            if (!string.IsNullOrEmpty(priceStr) && decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
            {
                return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = DateTime.UtcNow, Source = "Binance" };
            }
        }

        return new QuoteResult { Success = false, Source = "Binance", Error = "Invalid response format." };
    }

    private async Task<QuoteResult> FetchSingleCryptoQuoteAsync(AssetDefinition definition, CancellationToken stoppingToken)
    {
        // Batch-of-one wrapper reusing the same multi-source batch logic
        List<AllocationType> single = new List<AllocationType> { definition.AllocationType };
        Dictionary<AllocationType, QuoteResult> results = await FetchCryptoBatchAsync(single, stoppingToken);
        return results.TryGetValue(definition.AllocationType, out QuoteResult? result)
          ? result
          : new QuoteResult { Success = false, Source = "None", Error = "Crypto fetch failed." };
    }

    // -----------------------------------------------------------------------------
    // US Stock / ETF - Hardcoded catalog path (Finnhub -> TwelveData -> Stooq)
    // -----------------------------------------------------------------------------

    private async Task<QuoteResult> FetchStockEtfQuoteAsync(AssetDefinition definition, CancellationToken stoppingToken)
    {
        string symbol = definition.Symbol ?? definition.AllocationType.ToString();

        // Source 1: Finnhub
        if (!string.IsNullOrEmpty(finnhubApiKey))
        {
            QuoteResult? r = await Helper.TryWithRetryAsync(
              () => FetchFromFinnhubAsync(symbol, stoppingToken), "Finnhub", logger, stoppingToken);
            if (r != null && r.Success) return r;
        }

        // Source 2: TwelveData
        if (!string.IsNullOrEmpty(twelveDataApiKey))
        {
            QuoteResult? r = await Helper.TryWithRetryAsync(
              () => FetchFromTwelveDataStockAsync(symbol, stoppingToken), "TwelveData", logger, stoppingToken);
            if (r != null && r.Success) return r;
        }

        // Source 3: Yahoo Finance (no key)
        QuoteResult? yahoo = await Helper.TryWithRetryAsync(
          () => FetchFromYahooFinanceAsync(symbol, stoppingToken), "YahooFinance", logger, stoppingToken);
        if (yahoo != null && yahoo.Success) return yahoo;

        // Source 4: Stooq (no key)
        if (!string.IsNullOrEmpty(definition.StooqSymbol))
        {
            QuoteResult? r = await Helper.TryWithRetryAsync(
              () => FetchFromStooqAsync(definition.StooqSymbol, stoppingToken), "Stooq", logger, stoppingToken);
            if (r != null && r.Success) return r;
        }

        return new QuoteResult { Success = false, Source = "AllFailed", Error = $"All sources failed for {symbol}." };
    }

    private async Task<QuoteResult> FetchFromFinnhubAsync(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(finnhubApiKey))
            return new QuoteResult { Success = false, Source = "Finnhub", Warning = "API key not configured." };

        string url = $"https://finnhub.io/api/v1/quote?symbol={Uri.EscapeDataString(symbol)}&token={finnhubApiKey}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("c", out JsonElement cEl) && cEl.TryGetDecimal(out decimal price) && price > 0)
        {
            DateTime ts = DateTime.UtcNow;
            if (root.TryGetProperty("t", out JsonElement tEl) && tEl.TryGetInt64(out long epoch) && epoch > 0)
                ts = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

            return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = ts, Source = "Finnhub" };
        }

        return new QuoteResult { Success = false, Source = "Finnhub", Error = "Missing or zero price." };
    }

    private async Task<QuoteResult> FetchFromTwelveDataStockAsync(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(twelveDataApiKey))
            return new QuoteResult { Success = false, Source = "TwelveData", Warning = "API key not configured." };

        string url = $"https://api.twelvedata.com/price?symbol={Uri.EscapeDataString(symbol)}&apikey={twelveDataApiKey}";
        string json = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("price", out JsonElement priceEl) &&
            decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) &&
            price > 0)
        {
            return new QuoteResult { Success = true, Price = price, QuoteTimestampUtc = DateTime.UtcNow, Source = "TwelveData" };
        }

        return new QuoteResult { Success = false, Source = "TwelveData", Error = "Missing or zero price." };
    }

    private async Task<QuoteResult> FetchFromStooqAsync(string stooqSymbol, CancellationToken ct)
    {
        string url = $"https://stooq.com/q/l/?s={Uri.EscapeDataString(stooqSymbol)}&f=sd2t2ohlcv&h&e=csv";
        string csv = await Helper.HttpGetAsync(httpClientFactory, url, HttpTimeout, ct);

        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return new QuoteResult { Success = false, Source = "Stooq", Error = "No data rows." };

        string[] headers = lines[0].Split(',');
        string[] values = lines[1].Split(',');

        int closeIndex = Array.FindIndex(headers, h => h.Trim().Equals("Close", StringComparison.OrdinalIgnoreCase));
        int dateIndex = Array.FindIndex(headers, h => h.Trim().Equals("Date", StringComparison.OrdinalIgnoreCase));
        int timeIndex = Array.FindIndex(headers, h => h.Trim().Equals("Time", StringComparison.OrdinalIgnoreCase));

        if (closeIndex < 0 || closeIndex >= values.Length)
            return new QuoteResult { Success = false, Source = "Stooq", Error = "Close column not found." };

        string closeStr = values[closeIndex].Trim();
        if (!decimal.TryParse(closeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal closePrice) || closePrice <= 0)
            return new QuoteResult { Success = false, Source = "Stooq", Error = "Invalid close price." };

        DateTime ts = DateTime.UtcNow;
        if (dateIndex >= 0 && dateIndex < values.Length)
        {
            string dateStr = values[dateIndex].Trim();
            string timeStr = timeIndex >= 0 && timeIndex < values.Length ? values[timeIndex].Trim() : "00:00:00";
            if (DateTime.TryParse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture,
                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                ts = parsed;
            }
        }

        return new QuoteResult { Success = true, Price = closePrice, QuoteTimestampUtc = ts, Source = "Stooq" };
    }
}
