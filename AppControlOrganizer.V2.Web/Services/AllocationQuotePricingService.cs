//-----------------------------------------------------------------------
// <copyright file="AllocationQuotePricingService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;

/// <summary>
/// Result of converting an allocation amount to BRL using market quotes.
/// </summary>
public sealed class AllocationBrlConversionResult
{
    /// <summary>
    /// Indicates whether the conversion to BRL was successful.
    /// </summary>
    public bool IsConvertible { get; init; }

    /// <summary>
    /// The converted amount in BRL. Zero if conversion was not possible.
    /// </summary>
    public decimal AmountBrl { get; init; }

    /// <summary>
    /// Explanatory note when conversion is not possible.
    /// </summary>
    public string? Note { get; init; }
}

/// <summary>
/// Result of calculating the unit price for an allocation using market quotes.
/// </summary>
public sealed class AllocationQuotePriceResult
{
    /// <summary>
    /// The unit price in the allocation's currency.
    /// </summary>
    public decimal UnitPriceInAllocationCurrency { get; init; }

    /// <summary>
    /// True if a market quote was used for the calculation.
    /// </summary>
    public bool UsedQuote { get; init; }

    /// <summary>
    /// True if a fallback value was used due to missing quote or conversion rate.
    /// </summary>
    public bool UsedFallback { get; init; }

    /// <summary>
    /// The data source name (e.g., "Finnhub", "CoinGecko") if a quote was used.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The UTC timestamp provided by the quote source, if available.
    /// </summary>
    public DateTime? QuoteTimestampUtc { get; init; }

    /// <summary>
    /// The UTC timestamp when the quote was retrieved.
    /// </summary>
    public DateTime? RetrievedAtUtc { get; init; }

    /// <summary>
    /// The currency of the raw quote price.
    /// </summary>
    public Currency? QuoteCurrency { get; init; }

    /// <summary>
    /// The raw price from the market quote before any conversion.
    /// </summary>
    public decimal? RawQuotePrice { get; init; }

    /// <summary>
    /// The USD/BRL rate used when currency conversion was performed.
    /// </summary>
    public decimal? UsdBrlRateUsed { get; init; }

    /// <summary>
    /// Explanatory note when fallback is used or conversion was not possible.
    /// </summary>
    public string? Note { get; init; }
}

/// <summary>
/// Provides centralized pricing logic for allocations using market quotes.
/// </summary>
public sealed class AllocationQuotePricingService
{
    /// <summary>
    /// Calculates the unit price for an allocation using market quotes when available.
    /// Uses (AllocationType, Initials) as the composite lookup key so each individual
    /// asset (e.g. PETR4, AAPL) gets its own quote rather than sharing one per type.
    /// </summary>
    public AllocationQuotePriceResult Calculate(
        AllocationEntity allocation,
        IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes)
    {
        if (allocation == null)
        {
            return CreateFallbackResult(0m, "Allocation is null.");
        }

        if (latestQuotes == null)
        {
            latestQuotes = new Dictionary<QuoteKey, MarketQuoteSnapshot>();
        }

        decimal fallbackPrice = allocation.CurrentUnitValue ?? allocation.BuyUnitValue;

        // Try to get quote using composite key (type + initials/symbol)
        if (!MarketQuoteCache.TryGetForAllocation(
              latestQuotes, allocation.AllocationType, allocation.Initials, out MarketQuoteSnapshot? quote)
            || quote == null)
        {
            return CreateFallbackResult(fallbackPrice, "No market quote available for this allocation type.");
        }

        // Quote exists - determine if conversion is needed
        decimal rawQuotePrice = quote.Price;
        Currency quoteCurrency = quote.QuoteCurrency;
        Currency allocationCurrency = allocation.Currency;

        // Same currency - use directly
        if (allocationCurrency == quoteCurrency)
        {
            return new AllocationQuotePriceResult
            {
                UnitPriceInAllocationCurrency = rawQuotePrice,
                UsedQuote = true,
                UsedFallback = false,
                Source = quote.Source,
                QuoteTimestampUtc = quote.QuoteTimestampUtc,
                RetrievedAtUtc = quote.RetrievedAtUtc,
                QuoteCurrency = quoteCurrency,
                RawQuotePrice = rawQuotePrice,
                UsdBrlRateUsed = null,
                Note = null
            };
        }

        // Need USD/BRL conversion
        if (!MarketQuoteCache.TryGetByType(latestQuotes, AllocationType.UsdBrl, out MarketQuoteSnapshot? usdBrlQuote)
            || usdBrlQuote == null || usdBrlQuote.Price <= 0)
        {
            return new AllocationQuotePriceResult
            {
                UnitPriceInAllocationCurrency = fallbackPrice,
                UsedQuote = true,
                UsedFallback = true,
                Source = quote.Source,
                QuoteTimestampUtc = quote.QuoteTimestampUtc,
                RetrievedAtUtc = quote.RetrievedAtUtc,
                QuoteCurrency = quoteCurrency,
                RawQuotePrice = rawQuotePrice,
                UsdBrlRateUsed = null,
                Note = "Missing USD/BRL quote for currency conversion; fallback value used."
            };
        }

        decimal usdBrlRate = usdBrlQuote.Price;
        decimal convertedPrice;

        // Quote is in USD, allocation is in BRL - convert USD to BRL
        if (allocationCurrency == Currency.BRL && quoteCurrency == Currency.USD)
        {
            convertedPrice = rawQuotePrice * usdBrlRate;
        }
        // Quote is in BRL, allocation is in USD - convert BRL to USD
        else if (allocationCurrency == Currency.USD && quoteCurrency == Currency.BRL)
        {
            convertedPrice = rawQuotePrice / usdBrlRate;
        }
        else
        {
            // Unsupported currency pair - use fallback
            return new AllocationQuotePriceResult
            {
                UnitPriceInAllocationCurrency = fallbackPrice,
                UsedQuote = true,
                UsedFallback = true,
                Source = quote.Source,
                QuoteTimestampUtc = quote.QuoteTimestampUtc,
                RetrievedAtUtc = quote.RetrievedAtUtc,
                QuoteCurrency = quoteCurrency,
                RawQuotePrice = rawQuotePrice,
                UsdBrlRateUsed = null,
                Note = $"Cannot convert from {quoteCurrency} to {allocationCurrency}; fallback value used."
            };
        }

        return new AllocationQuotePriceResult
        {
            UnitPriceInAllocationCurrency = convertedPrice,
            UsedQuote = true,
            UsedFallback = false,
            Source = quote.Source,
            QuoteTimestampUtc = quote.QuoteTimestampUtc,
            RetrievedAtUtc = quote.RetrievedAtUtc,
            QuoteCurrency = quoteCurrency,
            RawQuotePrice = rawQuotePrice,
            UsdBrlRateUsed = usdBrlRate,
            Note = null
        };
    }

    private static AllocationQuotePriceResult CreateFallbackResult(decimal fallbackPrice, string note)
    {
        return new AllocationQuotePriceResult
        {
            UnitPriceInAllocationCurrency = fallbackPrice,
            UsedQuote = false,
            UsedFallback = true,
            Source = null,
            QuoteTimestampUtc = null,
            RetrievedAtUtc = null,
            QuoteCurrency = null,
            RawQuotePrice = null,
            UsdBrlRateUsed = null,
            Note = note
        };
    }

    /// <summary>
    /// Converts an amount in the allocation's currency to BRL using market quotes.
    /// </summary>
    /// <param name="allocation">The allocation whose currency is used to determine the conversion path.</param>
    /// <param name="amountInAllocationCurrency">The amount expressed in the allocation's native currency.</param>
    /// <param name="latestQuotes">Dictionary of the most recent market quotes keyed by <see cref="QuoteKey"/>.</param>
    /// <returns>A <see cref="AllocationBrlConversionResult"/> indicating whether conversion succeeded and the resulting BRL amount.</returns>
    public AllocationBrlConversionResult ConvertAmountToBrl(
        AllocationEntity allocation,
        decimal amountInAllocationCurrency,
        IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes)
    {
        if (allocation == null)
        {
            return new AllocationBrlConversionResult { IsConvertible = false, AmountBrl = 0m, Note = "Allocation is null." };
        }

        if (latestQuotes == null)
        {
            latestQuotes = new Dictionary<QuoteKey, MarketQuoteSnapshot>();
        }

        if (allocation.Currency == Currency.BRL)
        {
            return new AllocationBrlConversionResult { IsConvertible = true, AmountBrl = amountInAllocationCurrency };
        }

        // All non-BRL paths require the USD/BRL rate
        if (!MarketQuoteCache.TryGetByType(latestQuotes, AllocationType.UsdBrl, out MarketQuoteSnapshot? usdBrl)
            || usdBrl == null || usdBrl.Price <= 0m)
        {
            return new AllocationBrlConversionResult { IsConvertible = false, AmountBrl = 0m, Note = "Missing USD/BRL quote." };
        }

        if (allocation.Currency == Currency.USD)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = true,
                AmountBrl = amountInAllocationCurrency * usdBrl.Price,
            };
        }

        if (allocation.Currency == Currency.CRYPTO)
        {
            return ConvertGenericCryptoToBrl(allocation, amountInAllocationCurrency, latestQuotes, usdBrl.Price);
        }

        if (IsCryptoCurrency(allocation.Currency))
        {
            return ConvertSpecificCryptoToBrl(allocation, amountInAllocationCurrency, latestQuotes, usdBrl.Price);
        }

        return new AllocationBrlConversionResult
        {
            IsConvertible = false,
            AmountBrl = 0m,
            Note = $"Currency {allocation.Currency} not supported for BRL conversion.",
        };
    }

    /// <summary>
    /// Converts a generic CRYPTO allocation to BRL by resolving its asset price via market quotes.
    /// </summary>
    private static AllocationBrlConversionResult ConvertGenericCryptoToBrl(
        AllocationEntity allocation,
        decimal amount,
        IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes,
        decimal usdBrlRate)
    {
        if (!MarketQuoteCache.TryGetForAllocation(
                latestQuotes, allocation.AllocationType, allocation.Initials, out MarketQuoteSnapshot? assetQuote)
            || assetQuote == null || assetQuote.Price <= 0m)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = false,
                AmountBrl = 0m,
                Note = "No market quote available for this CRYPTO allocation.",
            };
        }

        if (assetQuote.QuoteCurrency == Currency.USD)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = true,
                AmountBrl = amount * assetQuote.Price * usdBrlRate,
            };
        }

        if (assetQuote.QuoteCurrency == Currency.BRL)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = true,
                AmountBrl = amount * assetQuote.Price,
            };
        }

        return new AllocationBrlConversionResult
        {
            IsConvertible = false,
            AmountBrl = 0m,
            Note = $"Unsupported quote currency {assetQuote.QuoteCurrency} for CRYPTO allocation.",
        };
    }

    /// <summary>
    /// Converts a specific named crypto currency (BTC, ETH, ADA, …) to BRL using its USD market price.
    /// </summary>
    private static AllocationBrlConversionResult ConvertSpecificCryptoToBrl(
        AllocationEntity allocation,
        decimal amount,
        IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes,
        decimal usdBrlRate)
    {
        AllocationType? cryptoType = MapCryptoCurrencyToAllocationType(allocation.Currency);
        if (cryptoType == null)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = false,
                AmountBrl = 0m,
                Note = "Unsupported crypto currency for BRL conversion.",
            };
        }

        if (!MarketQuoteCache.TryGetByType(latestQuotes, cryptoType.Value, out MarketQuoteSnapshot? cryptoQuote)
            || cryptoQuote == null || cryptoQuote.QuoteCurrency != Currency.USD || cryptoQuote.Price <= 0m)
        {
            return new AllocationBrlConversionResult
            {
                IsConvertible = false,
                AmountBrl = 0m,
                Note = "Missing crypto/USD quote for BRL conversion.",
            };
        }

        return new AllocationBrlConversionResult
        {
            IsConvertible = true,
            AmountBrl = amount * cryptoQuote.Price * usdBrlRate,
        };
    }

    /// <summary>
    /// Determines whether the specified currency is a cryptocurrency.
    /// </summary>
    private static bool IsCryptoCurrency(Currency currency)
    {
        return (int)currency >= (int)Currency.BTC;
    }

    /// <summary>
    /// Maps a crypto currency to its corresponding allocation type for quote lookup.
    /// </summary>
    private static AllocationType? MapCryptoCurrencyToAllocationType(Currency currency)
    {
        return currency switch
        {
            Currency.BTC => AllocationType.BTC,
            Currency.ETH => AllocationType.ETH,
            Currency.ADA => AllocationType.ADA,
			Currency.CRYPTO => AllocationType.Crypto,
            _ => null
        };
    }
}
