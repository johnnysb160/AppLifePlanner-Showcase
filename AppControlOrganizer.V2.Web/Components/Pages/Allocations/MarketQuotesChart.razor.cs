//-----------------------------------------------------------------------
// <copyright file="MarketQuotesChart.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Allocations
{
  using AppControlOrganizer.V2.Shared.Entities;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
  using Microsoft.AspNetCore.Components;
  using Microsoft.JSInterop;

  /// <summary>
  /// Code-behind for MarketQuotesChart component.
  /// Displays USD/BRL and BTC/USD market quote charts using historical data.
  /// </summary>
  public partial class MarketQuotesChart : ComponentBase
  {
    private static readonly IReadOnlyList<QuoteRangeOption> RangeOptions = new List<QuoteRangeOption>
    {
      new QuoteRangeOption(1, "1D"),
      new QuoteRangeOption(7, "7D"),
      new QuoteRangeOption(30, "1M"),
      new QuoteRangeOption(60, "2M"),
      new QuoteRangeOption(90, "3M"),
      new QuoteRangeOption(180, "6M"),
      new QuoteRangeOption(270, "9M"),
      new QuoteRangeOption(365, "1Y")
    };

    /// <summary>
    /// Gets or sets the quote key that should receive visual focus.
    /// </summary>
    [Parameter]
    public string? FocusQuote { get; set; }

    /// <summary>Gets or sets the market quote repository.</summary>
    [Inject]
    public IMarketQuoteRepository QuoteRepo { get; set; } = null!;

    /// <summary>Gets or sets the JS runtime for chart rendering.</summary>
    [Inject]
    public IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>Gets or sets the selected USD range in days.</summary>
    protected int usdRange = 7;

    /// <summary>Gets or sets the selected BTC range in days.</summary>
    protected int btcRange = 7;

    /// <summary>Gets or sets a value indicating whether USD data is loading.</summary>
    protected bool isLoadingUsd = true;

    /// <summary>Gets or sets a value indicating whether BTC data is loading.</summary>
    protected bool isLoadingBtc = true;

    /// <summary>Gets or sets the list of USD/BRL market quotes.</summary>
    protected List<MarketQuoteEntity> usdQuotes = new List<MarketQuoteEntity>();

    /// <summary>Gets or sets the list of BTC/USD market quotes.</summary>
    protected List<MarketQuoteEntity> btcQuotes = new List<MarketQuoteEntity>();

    /// <summary>
    /// Returns whether the specified quote is focused by contextual navigation.
    /// </summary>
    /// <param name="quote">The quote identifier.</param>
    /// <returns>True when the quote should be highlighted.</returns>
    protected bool IsFocused(string quote)
    {
      return string.Equals(FocusQuote, quote, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes the component by loading both USD and BTC data concurrently.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
      await Task.WhenAll(LoadUsdData(usdRange), LoadBtcData(btcRange));
    }

    /// <summary>
    /// Loads USD/BRL quote data for the specified number of days.
    /// </summary>
    /// <param name="days">Number of past days to retrieve.</param>
    protected async Task LoadUsdData(int days)
    {
      usdRange = days;
      isLoadingUsd = true;
      StateHasChanged();

      DateTime endDate = DateTime.UtcNow;
      DateTime startDate = endDate.AddDays(-days);

      usdQuotes = await QuoteRepo.SelectByAllocationTypeAndDateRangeAsync(
        AllocationType.UsdBrl,
        startDate,
        endDate) ?? new List<MarketQuoteEntity>();

      isLoadingUsd = false;
      StateHasChanged();

      if (usdQuotes.Any())
      {
        List<MarketQuoteEntity> orderedQuotes = PrepareChartQuotes(usdQuotes, days);
        string[] labels = orderedQuotes
          .Select(q => FormatChartLabel(q.RetrievedAtUtc, days))
          .ToArray();
        decimal?[] values = orderedQuotes.Select(q => q.Price).ToArray();

        await Task.Delay(50);
        await JSRuntime.InvokeVoidAsync("renderMarketChart", "usdChart", "USD/BRL", labels, values, "#198754");
      }
    }

    /// <summary>
    /// Loads BTC/USD quote data for the specified number of days.
    /// </summary>
    /// <param name="days">Number of past days to retrieve.</param>
    protected async Task LoadBtcData(int days)
    {
      btcRange = days;
      isLoadingBtc = true;
      StateHasChanged();

      DateTime endDate = DateTime.UtcNow;
      DateTime startDate = endDate.AddDays(-days);

      btcQuotes = await QuoteRepo.SelectByAllocationTypeAndDateRangeAsync(
        AllocationType.BTC,
        startDate,
        endDate) ?? new List<MarketQuoteEntity>();

      isLoadingBtc = false;
      StateHasChanged();

      if (btcQuotes.Any())
      {
        List<MarketQuoteEntity> orderedQuotes = PrepareChartQuotes(btcQuotes, days);
        string[] labels = orderedQuotes
          .Select(q => FormatChartLabel(q.RetrievedAtUtc, days))
          .ToArray();
        decimal?[] values = orderedQuotes.Select(q => q.Price).ToArray();

        await Task.Delay(50);
        await JSRuntime.InvokeVoidAsync("renderMarketChart", "btcChart", "BTC/USD", labels, values, "#dc3545");
      }
    }

    private string FormatChartLabel(DateTime retrievedAtUtc, int days)
    {
      DateTime localDate = retrievedAtUtc.ToLocalTime();
      return localDate.ToString(days == 1 ? "HH:mm" : Loc.CompactDayMonthPattern, Loc.Culture);
    }

    private static List<MarketQuoteEntity> PrepareChartQuotes(IEnumerable<MarketQuoteEntity> quotes, int days)
    {
      IEnumerable<MarketQuoteEntity> orderedQuotes = quotes.Reverse();

      if (days <= 30)
      {
        return orderedQuotes.ToList();
      }

      return orderedQuotes
        .GroupBy(quote => quote.RetrievedAtUtc.ToLocalTime().Date)
        .Select(group => group.Last())
        .ToList();
    }

    private sealed class QuoteRangeOption
    {
      public QuoteRangeOption(int days, string label)
      {
        this.Days = days;
        this.Label = label;
      }

      public int Days { get; }

      public string Label { get; }
    }
  }
}
