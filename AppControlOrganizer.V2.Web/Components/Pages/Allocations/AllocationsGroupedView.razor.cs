//-----------------------------------------------------------------------
// <copyright file="AllocationsGroupedView.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using System;
using System.Globalization;
using System.Linq;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AppControlOrganizer.V2.Web.Components.Pages.Allocations
{
  public partial class AllocationsGroupedView : ComponentBase, IDisposable
  {
    [Inject]
    private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;
    [Inject]
    public AllocationQuotePricingService PricingService { get; set; } = null!;

    [Parameter] public IReadOnlyList<GroupedTypeVm> Groups { get; set; } = Array.Empty<GroupedTypeVm>();
    [Parameter] public IReadOnlyList<CurrencySummaryVm> CurrencySummaries { get; set; } = Array.Empty<CurrencySummaryVm>();

    [Parameter] public decimal TotalCurrentApproxBrl { get; set; }
    [Parameter] public bool HasMultipleCurrencies { get; set; }
    [Parameter] public bool HasUnconvertedCurrencies { get; set; }

    // Summary values (passed from parent)
    [Parameter] public decimal TotalInvested { get; set; }
    [Parameter] public decimal TotalCurrent { get; set; }
    [Parameter] public decimal Profit { get; set; }
    [Parameter] public decimal ProfitPercentage { get; set; }
    [Parameter] public bool IsLoadingSummary { get; set; }

    [Parameter] public IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> LatestQuotes { get; set; } = new Dictionary<QuoteKey, MarketQuoteSnapshot>();
    [Parameter] public MarketQuoteSnapshot? UsdBrlQuote { get; set; }
    [Parameter] public bool HasUsdBrlQuote { get; set; }

    [Parameter] public EventCallback<AllocationEntity> OnEdit { get; set; }
    [Parameter] public EventCallback<AllocationEntity> OnDelete { get; set; }
    [Parameter] public EventCallback<int> OnOpenAllocation { get; set; }

    /// <summary>
    /// Builds a localized tooltip string for a market-quote pricing result.
    /// </summary>
    private string BuildPricingTooltip(AllocationQuotePriceResult pricing)
    {
      if (pricing.UsedQuote)
      {
        string tooltip = $"{Loc["Source:"]} {pricing.Source ?? Loc["Unknown"]}";

        if (pricing.QuoteTimestampUtc.HasValue)
        {
          tooltip += $"\n{Loc["Quote time:"]} {pricing.QuoteTimestampUtc.Value:yyyy-MM-dd HH:mm} UTC";
        }

        if (pricing.RetrievedAtUtc.HasValue)
        {
          tooltip += $"\n{Loc["Retrieved:"]} {pricing.RetrievedAtUtc.Value:yyyy-MM-dd HH:mm} UTC";
        }

        if (pricing.UsdBrlRateUsed.HasValue)
        {
          tooltip += $"\nUSD/BRL: {pricing.UsdBrlRateUsed.Value:N4}";
        }

        if (pricing.UsedFallback && !string.IsNullOrEmpty(pricing.Note))
        {
          tooltip += $"\n{pricing.Note}";
        }

        return tooltip;
      }

      return LocalizeNote(pricing.Note) ?? Loc["No quote available; using fallback value."];
    }

    /// <summary>
    /// Translates a known service note string through LocService.
    /// Falls back to the original string for unknown or parametrized messages.
    /// </summary>
    private string? LocalizeNote(string? note) =>
      string.IsNullOrEmpty(note) ? note : Loc[note];

    /// <summary>
    /// Builds a localized structured tooltip for auto-calculated fixed income allocations.
    /// </summary>
    private string BuildAutoCalcTooltip(AllocationEntity item)
    {
      string tooltip = $"{Loc["Source:"]} BACEN SGS (API)";
      tooltip += $"\n{Loc["Yield:"]} {item.ProfitType} {item.ProfitPercentage}%";

      if (item.ProfitDate.HasValue)
      {
        tooltip += $"\n{Loc["Start:"]} {item.ProfitDate.Value.ToString("d", Loc.Culture)}";

        int businessDays = CountBusinessDays(item.ProfitDate.Value.AddDays(1), DateTime.UtcNow.Date);
        tooltip += $"\n{Loc["Business days:"]} {businessDays}";
      }

      if (item.CurrentUnitValue.HasValue)
      {
        var culture = Loc.Language == "pt"
          ? System.Globalization.CultureInfo.GetCultureInfo("pt-BR")
          : System.Globalization.CultureInfo.InvariantCulture;
        tooltip += $"\n{Loc["Current value:"]} {item.CurrentUnitValue.Value.ToString("N2", culture)}";
      }

      tooltip += $"\n{Loc["Updated: automatically (60 min)"]}";
      return tooltip;
    }

    /// <summary>
    /// Counts Mon-Fri business days between two dates (inclusive of end, exclusive of start).
    /// </summary>
    private static int CountBusinessDays(DateTime start, DateTime end)
    {
      int count = 0;
      for (DateTime d = start.Date; d <= end.Date; d = d.AddDays(1))
      {
        if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
          count++;
      }
      return count;
    }

    /// <summary>
    /// Formats quantity using N2 when integer; otherwise up to 8 decimals trimming trailing zeros.
    /// </summary>
    private static string FormatQuantity(decimal quantity)
    {
      decimal truncated = decimal.Truncate(quantity);
      if (quantity == truncated)
      {
        return quantity.ToString("N2", CultureInfo.InvariantCulture);
      }

      string text = quantity.ToString("N8", CultureInfo.InvariantCulture);
      text = text.TrimEnd('0');
      if (text.EndsWith(".", StringComparison.Ordinal))
      {
        text = text.Substring(0, text.Length - 1);
      }

      return text;
    }

    private static string GetAccountHref(int accountId)
    {
      return $"accounts?accountId={accountId}";
    }

    private static string GetTypeHref(AllocationType type)
    {
      return $"allocations?view=list&type={type}";
    }

    private static string GetAccountTypeHref(int accountId, AllocationType type)
    {
      return $"allocations?view=list&accountId={accountId}&type={type}";
    }

    private static string GetQuoteHref(AllocationEntity allocation)
    {
      string quote = string.IsNullOrWhiteSpace(allocation.Initials)
        ? allocation.AllocationType.ToString()
        : allocation.Initials.Trim();
      return $"allocations?panel=quotes&quote={Uri.EscapeDataString(quote)}";
    }

    private static string GetSearchHref(string search)
    {
      return $"allocations?view=list&search={Uri.EscapeDataString(search)}";
    }

    private static string GetUpcomingHref()
    {
      return "allocations?view=list&expires=upcoming";
    }

    private Task OpenItemAsync(AllocationEntity allocation)
    {
      if (OnOpenAllocation.HasDelegate)
      {
        return OnOpenAllocation.InvokeAsync(allocation.Id);
      }

      return OnEdit.InvokeAsync(allocation);
    }

    /// <inheritdoc />
    public void Dispose() => ValuesVis.OnChange -= OnValuesVisibilityChanged;

    private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void OnInitialized() => ValuesVis.OnChange += OnValuesVisibilityChanged;

  }
}
