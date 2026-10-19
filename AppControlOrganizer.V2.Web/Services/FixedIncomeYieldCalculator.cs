//-----------------------------------------------------------------------
// <copyright file="FixedIncomeYieldCalculator.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Helpers;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Calculates the current estimated unit value for fixed-income allocations
/// (CDB, LCA, LCI, Tesouro Direto, etc.) using the public BACEN SGS API.
///
/// Supported ProfitType values (stored in AllocationEntity.ProfitType, case-insensitive):
///   "CDI"       - X% do CDI.  ProfitPercentage = 104 means 104% CDI.
///   "SELIC"     - X% da SELIC. ProfitPercentage = 100 means 100% SELIC.
///   "IPCA+"     - IPCA acumulado + spread anual. ProfitPercentage = 6 means IPCA+6% a.a.
///   "Prefixado" - Taxa prefixada. ProfitPercentage = 12 means 12% a.a.
///
/// BACEN SGS series used:
///   12  = Taxa CDI Over (% a.d., quoted daily) - returns the DAILY rate, e.g. 0.057128 = 0.057128% per day
///   11  = Taxa Selic Over (% a.d., quoted daily) - returns the DAILY rate
///   433 = IPCA (% a.m., quoted monthly)
///
/// CDI/SELIC methodology:
///   The BACEN series 12/11 returns the DAILY over rate (% a.d.) for each business day.
///   We fetch the daily BACEN rates published between ProfitDate and today, then compound each
///   published rate with the factor applied.
///   Formula: current = buy * product(1 + daily_rate * factor)
///
/// IPCA+ methodology:
///   Fetches monthly IPCA rates from BACEN for the full investment period,
///   compounds them to get accumulated IPCA, then applies the annual spread.
///   Formula: current = buy * ipca_accumulated * (1 + spread/100)^(days/365)
///
/// Prefixado methodology:
///   Compounds the annual rate over calendar days.
///   Formula: current = buy * (1 + annual_rate/100)^(days/365)
/// </summary>
public static class FixedIncomeYieldCalculator
{
  private const int CdiSeriesId = 12;
  private const int SelicSeriesId = 11;
  private const int IpcaSeriesId = 433;

  private static readonly TimeSpan BacenTimeout = TimeSpan.FromSeconds(15);
  private static readonly HashSet<AllocationType> YieldMetadataTypes = new HashSet<AllocationType>
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
    AllocationType.PrivatePension
  };

  /// <summary>
  /// Determines whether the allocation type supports automatic yield metadata.
  /// </summary>
  /// <param name="allocation">The allocation to inspect.</param>
  /// <returns>True when the type can use automatic yield metadata; otherwise, false.</returns>
  public static bool IsSupportedYieldType(AllocationEntity allocation)
  {
    return YieldMetadataTypes.Contains(allocation.AllocationType);
  }

  /// <summary>
  /// Determines whether the allocation has a supported automatic yield rule.
  /// </summary>
  /// <param name="allocation">The allocation to inspect.</param>
  /// <returns>True when the allocation can be calculated automatically; otherwise, false.</returns>
  public static bool CanCalculateAllocation(AllocationEntity allocation)
  {
    return IsSupportedYieldType(allocation) &&
      allocation.ProfitDate is not null &&
      allocation.ProfitPercentage is not null &&
      !string.IsNullOrWhiteSpace(allocation.ProfitType);
  }

  /// <summary>
  /// Calculates the current estimated unit value for the given allocation.
  /// Returns null when data is missing, the profit type is unsupported, or BACEN is unreachable.
  /// </summary>
  public static async Task<decimal?> CalculateCurrentValueAsync(
    AllocationEntity allocation,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    CancellationToken ct)
  {
    FixedIncomeCalculationResult? result = await CalculateAsync(allocation, httpClientFactory, logger, ct);
    return result?.CurrentUnitValue;
  }

  /// <summary>
  /// Calculates the current estimated unit value and exposes the index period used for diagnostics.
  /// </summary>
  /// <param name="allocation">The allocation to calculate.</param>
  /// <param name="httpClientFactory">The HTTP client factory used to access BACEN SGS.</param>
  /// <param name="logger">The logger for non-fatal provider failures.</param>
  /// <param name="ct">A cancellation token.</param>
  /// <returns>The calculated result, or null when the allocation cannot be calculated.</returns>
  public static async Task<FixedIncomeCalculationResult?> CalculateAsync(
    AllocationEntity allocation,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    CancellationToken ct)
  {
    if (!CanCalculateAllocation(allocation))
    {
      return null;
    }

    DateTime startDate = allocation.ProfitDate!.Value.Date;
    DateTime endDate = DateTime.UtcNow.Date;

    if (startDate >= endDate)
    {
      return null;
    }

    decimal percentage = allocation.ProfitPercentage!.Value;
    string profitType = allocation.ProfitType!.Trim().ToUpperInvariant();

    try
    {
      return profitType switch
      {
        "CDI" => await CalcCdiSelicAsync(
          allocation.BuyUnitValue, percentage, CdiSeriesId, "CDI",
          httpClientFactory, logger, startDate, endDate, ct),

        "SELIC" => await CalcCdiSelicAsync(
          allocation.BuyUnitValue, percentage, SelicSeriesId, "SELIC",
          httpClientFactory, logger, startDate, endDate, ct),

        "IPCA+" => await CalcIpcaPlusAsync(
          allocation.BuyUnitValue, percentage, httpClientFactory, logger, startDate, endDate, ct),

        "PREFIXADO" => new FixedIncomeCalculationResult
        {
          CurrentUnitValue = CalcPrefixado(allocation.BuyUnitValue, percentage, startDate, endDate),
          Source = "Prefixado",
          StartDate = startDate,
          EndDate = endDate
        },

        _ => null,
      };
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex,
        "FixedIncomeYieldCalculator: error calculating yield for allocation {Id} (type={Type}).",
        allocation.Id, allocation.ProfitType);
      return null;
    }
  }

  // ---------------------------------------------------------------------------
  // CDI / SELIC:
  //   1. Fetch daily rates from BACEN series 12/11 (% a.d.) for the investment period.
  //      e.g. "0.057128" means 0.057128% per business day
  //   2. Use directly as daily fraction: daily = dailyRatePct / 100
  //   3. Apply factor to each published daily rate.
  //   4. Compound: current = buy * product(1 + daily * factor)
  //      where factor = percentage/100 (e.g. 1.04 for 104% CDI)
  // ---------------------------------------------------------------------------

  private static async Task<FixedIncomeCalculationResult?> CalcCdiSelicAsync(
    decimal buyValue,
    decimal percentage,
    int seriesId,
    string label,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    DateTime startDate,
    DateTime endDate,
    CancellationToken ct)
  {
    List<FixedIncomeRatePoint> dailyRates = await FetchPeriodRatesAsync(
      seriesId,
      label,
      httpClientFactory,
      logger,
      startDate.AddDays(1),
      endDate,
      ct);

    if (dailyRates.Count == 0)
    {
      return null;
    }

    decimal currentUnitValue = CalculateDailyIndexedValue(
      buyValue,
      percentage,
      dailyRates.Select(rate => rate.RatePercentage));

    return new FixedIncomeCalculationResult
    {
      CurrentUnitValue = currentUnitValue,
      Source = $"BACEN SGS {seriesId} ({label})",
      FirstRateDate = dailyRates.Min(rate => rate.Date),
      LastRateDate = dailyRates.Max(rate => rate.Date),
      RateCount = dailyRates.Count,
      StartDate = startDate,
      EndDate = endDate
    };
  }

  /// <summary>
  /// Compounds a principal value by daily published index rates and a percentage factor.
  /// </summary>
  /// <param name="buyValue">Initial unit value.</param>
  /// <param name="percentage">Percentage of the index, for example 115 for 115% CDI.</param>
  /// <param name="dailyRatePercentages">Daily rates expressed as percentages.</param>
  /// <returns>The compounded current unit value.</returns>
  public static decimal CalculateDailyIndexedValue(
    decimal buyValue,
    decimal percentage,
    IEnumerable<decimal> dailyRatePercentages)
  {
    decimal factor = percentage / 100m;
    decimal accumulated = 1m;

    foreach (decimal dailyRatePct in dailyRatePercentages)
    {
      decimal dailyRate = dailyRatePct / 100m;
      accumulated *= 1m + dailyRate * factor;
    }

    return buyValue * accumulated;
  }

  // ---------------------------------------------------------------------------
  // IPCA+:
  //   1. Fetch monthly IPCA rates from BACEN for the full period
  //   2. Compound monthly rates to get accumulated IPCA factor
  //   3. Apply annual spread compounded over calendar days
  //   Formula: current = buy * ipca_accumulated * (1 + spread/100)^(days/365)
  // ---------------------------------------------------------------------------

  private static async Task<FixedIncomeCalculationResult?> CalcIpcaPlusAsync(
    decimal buyValue,
    decimal spreadPercent,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    DateTime startDate,
    DateTime endDate,
    CancellationToken ct)
  {
    List<FixedIncomeRatePoint> monthlyRates = await FetchPeriodRatesAsync(
      IpcaSeriesId, "IPCA", httpClientFactory, logger, startDate, endDate, ct);

    if (monthlyRates.Count == 0)
    {
      return null;
    }

    // Compound monthly IPCA rates
    decimal ipcaAccumulated = 1m;
    foreach (FixedIncomeRatePoint rate in monthlyRates)
    {
      ipcaAccumulated *= 1m + rate.RatePercentage / 100m;
    }

    // Add annual spread compounded over calendar days
    double days = (endDate - startDate).TotalDays;
    double spreadFactor = Math.Pow(1.0 + (double)spreadPercent / 100.0, days / 365.0);

    return new FixedIncomeCalculationResult
    {
      CurrentUnitValue = buyValue * ipcaAccumulated * (decimal)spreadFactor,
      Source = $"BACEN SGS {IpcaSeriesId} (IPCA)",
      FirstRateDate = monthlyRates.Min(rate => rate.Date),
      LastRateDate = monthlyRates.Max(rate => rate.Date),
      RateCount = monthlyRates.Count,
      StartDate = startDate,
      EndDate = endDate
    };
  }

  // ---------------------------------------------------------------------------
  // Prefixado:
  //   Simple annual compounding over calendar days.
  //   current = buy * (1 + annual_rate/100)^(days/365)
  //   where annual_rate = ProfitPercentage (e.g. 12 for 12% a.a.)
  // ---------------------------------------------------------------------------

  private static decimal CalcPrefixado(
    decimal buyValue,
    decimal annualRatePercent,
    DateTime startDate,
    DateTime endDate)
  {
    double days = (endDate - startDate).TotalDays;
    double growthFactor = Math.Pow(1.0 + (double)annualRatePercent / 100.0, days / 365.0);
    return buyValue * (decimal)growthFactor;
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  /// <summary>
  /// Fetches all data points for a BACEN SGS series between two dates.
  /// Used for IPCA monthly rates over the investment period.
  /// GET https://api.bcb.gov.br/dados/serie/bcdata.sgs.{id}/dados?formato=json&dataInicial=DD/MM/YYYY&dataFinal=DD/MM/YYYY
  /// Response: [{"data":"01/2026","valor":"0.56"}, ...]
  /// </summary>
  private static async Task<List<FixedIncomeRatePoint>> FetchPeriodRatesAsync(
    int seriesId,
    string label,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    DateTime startDate,
    DateTime endDate,
    CancellationToken ct)
  {
    string startStr = startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    string endStr = endDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    string url = $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{seriesId}/dados" +
                 $"?formato=json&dataInicial={Uri.EscapeDataString(startStr)}&dataFinal={Uri.EscapeDataString(endStr)}";

    List<FixedIncomeRatePoint> rates = new();
    try
    {
      string json = await Helper.HttpGetAsync(httpClientFactory, url, BacenTimeout, ct);
      using JsonDocument doc = JsonDocument.Parse(json);
      JsonElement root = doc.RootElement;

      if (root.ValueKind != JsonValueKind.Array)
      {
        return rates;
      }

      foreach (JsonElement el in root.EnumerateArray())
      {
        if (!el.TryGetProperty("valor", out JsonElement valorEl))
        {
          continue;
        }

        string? valorStr = valorEl.ValueKind == JsonValueKind.String
          ? valorEl.GetString()
          : valorEl.GetRawText();

        if (!TryReadBacenDate(el, out DateTime rateDate))
        {
          continue;
        }

        if (decimal.TryParse(valorStr?.Replace(",", "."),
              NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
        {
          rates.Add(new FixedIncomeRatePoint
          {
            Date = rateDate,
            RatePercentage = rate
          });
        }
      }
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex,
        "FixedIncomeYieldCalculator: failed to fetch BACEN series {Id} ({Label}) from {Start} to {End}.",
        seriesId, label, startStr, endStr);
    }

    return rates;
  }

  private static bool TryReadBacenDate(JsonElement element, out DateTime date)
  {
    date = default;

    if (!element.TryGetProperty("data", out JsonElement dataElement))
    {
      return false;
    }

    string? rawDate = dataElement.GetString();
    if (string.IsNullOrWhiteSpace(rawDate))
    {
      return false;
    }

    return DateTime.TryParseExact(
      rawDate,
      new[] { "dd/MM/yyyy", "MM/yyyy" },
      CultureInfo.InvariantCulture,
      DateTimeStyles.None,
      out date);
  }
}
