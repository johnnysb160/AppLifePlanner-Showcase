//-----------------------------------------------------------------------
// <copyright file="AllocationsPortfolioSummary.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Allocations
{
  using System.Globalization;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Shared.Models;
  using AppControlOrganizer.V2.Web.Helpers;
  using Microsoft.AspNetCore.Components;

  /// <summary>
  /// Rich portfolio summary shown above allocation list and grouped views.
  /// </summary>
  public partial class AllocationsPortfolioSummary : ComponentBase, IDisposable
  {
    private static readonly string[] ChartColors =
    {
      "#3b82f6",
      "#22c55e",
      "#f97316",
      "#8b5cf6",
      "#ec4899",
      "#14b8a6",
      "#eab308",
      "#64748b",
    };

    [Inject]
    private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether summary values are still loading.
    /// </summary>
    [Parameter]
    public bool IsLoadingSummary { get; set; }

    /// <summary>
    /// Gets or sets the total amount invested in BRL.
    /// </summary>
    [Parameter]
    public decimal TotalInvested { get; set; }

    /// <summary>
    /// Gets or sets the current portfolio value in BRL.
    /// </summary>
    [Parameter]
    public decimal TotalCurrent { get; set; }

    /// <summary>
    /// Gets or sets the absolute portfolio profit or loss in BRL.
    /// </summary>
    [Parameter]
    public decimal Profit { get; set; }

    /// <summary>
    /// Gets or sets the portfolio profit or loss percentage.
    /// </summary>
    [Parameter]
    public decimal ProfitPercentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the portfolio has multiple currencies.
    /// </summary>
    [Parameter]
    public bool HasMultipleCurrencies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether some currencies could not be converted.
    /// </summary>
    [Parameter]
    public bool HasUnconvertedCurrencies { get; set; }

    /// <summary>
    /// Gets or sets portfolio totals grouped by allocation type.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TypeSummaryVm> TypeSummaries { get; set; } = Array.Empty<TypeSummaryVm>();

    /// <summary>
    /// Gets or sets portfolio totals grouped by account.
    /// </summary>
    [Parameter]
    public IReadOnlyList<AccountSummaryVm> AccountSummaries { get; set; } = Array.Empty<AccountSummaryVm>();

    /// <summary>
    /// Gets or sets portfolio totals grouped by currency.
    /// </summary>
    [Parameter]
    public IReadOnlyList<CurrencySummaryVm> CurrencySummaries { get; set; } = Array.Empty<CurrencySummaryVm>();

    /// <summary>
    /// Gets or sets the largest portfolio positions.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TopPositionVm> TopPositions { get; set; } = Array.Empty<TopPositionVm>();

    /// <summary>
    /// Gets or sets upcoming allocation expirations.
    /// </summary>
    [Parameter]
    public IReadOnlyList<UpcomingExpirationVm> UpcomingExpirations { get; set; } = Array.Empty<UpcomingExpirationVm>();

    /// <summary>
    /// Gets or sets the link that clears filters and opens all allocations.
    /// </summary>
    [Parameter]
    public string AllAllocationsHref { get; set; } = "allocations";

    /// <summary>
    /// Gets or sets the link used by KPI cards to keep the user in the current allocation view.
    /// </summary>
    [Parameter]
    public string DashboardHref { get; set; } = "allocations";

    /// <summary>
    /// Gets or sets the callback used to open an allocation from summary rows.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnOpenAllocation { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
      ValuesVis.OnChange -= OnValuesVisibilityChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
      ValuesVis.OnChange += OnValuesVisibilityChanged;
    }

    private static string GetChartColor(int index)
    {
      return ChartColors[index % ChartColors.Length];
    }

    private string GetPortfolioMeta()
    {
      int positionCount = TypeSummaries.Sum(item => item.Count);
      string positions = $"{positionCount} {Loc[positionCount == 1 ? "position" : "positions"]}";
      string types = $"{TypeSummaries.Count} {Loc[TypeSummaries.Count == 1 ? "type" : "types"]}";
      return $"{positions} • {types}";
    }

    private string GetCurrentMeta()
    {
      if (HasUnconvertedCurrencies)
      {
        return Loc["Approx BRL (partial)"];
      }

      if (HasMultipleCurrencies || CurrencySummaries.Count > 1)
      {
        return Loc["Approx BRL (quotes)"];
      }

      Currency currency = CurrencySummaries.FirstOrDefault()?.Currency ?? Currency.BRL;
      return currency.ToString();
    }

    private string GetProfitMeta()
    {
      string percent = $"{ProfitPercentage.ToString("F2", CultureInfo.InvariantCulture)}%";
      return Profit >= 0m
        ? $"+{percent} • {Loc["Gain"]}"
        : $"{percent} • {Loc["Loss"]}";
    }

    private string BuildTypePieStyle()
    {
      List<PortfolioSegmentItem> segments = GetTypeLegendItems();
      return BuildPieStyle(segments);
    }

    private string BuildTypeAriaLabel()
    {
      List<PortfolioSegmentItem> segments = GetTypeLegendItems();
      if (segments.Count == 0)
      {
        return $"{Loc["Allocation Mix"]}: {Loc["No data."]}";
      }

      IEnumerable<string> details = segments.Select(item => $"{item.FullLabel}: {item.Percentage}");
      return $"{Loc["Allocation Mix"]}: {string.Join(", ", details)}";
    }

    private static string BuildPieStyle(IReadOnlyList<PortfolioSegmentItem> segments)
    {
      if (segments.Count == 0)
      {
        return "conic-gradient(#e2e8f0 0% 100%)";
      }

      decimal start = 0m;
      List<string> stops = new List<string>();
      for (int index = 0; index < segments.Count; index++)
      {
        decimal numericPercentage = decimal.Parse(segments[index].Percentage.TrimEnd('%'), CultureInfo.InvariantCulture);
        decimal end = index == segments.Count - 1 ? 100m : start + numericPercentage;
        string startText = start.ToString("F2", CultureInfo.InvariantCulture);
        string endText = end.ToString("F2", CultureInfo.InvariantCulture);
        stops.Add($"{segments[index].Color} {startText}% {endText}%");
        start = end;
      }

      return $"conic-gradient({string.Join(", ", stops)})";
    }

    private List<PortfolioSegmentItem> GetTypeLegendItems()
    {
      decimal total = TypeSummaries.Sum(item => item.CurrentTotal);
      if (total <= 0m)
      {
        return new List<PortfolioSegmentItem>();
      }

      List<TypeSummaryVm> visibleItems = TypeSummaries.Take(4).ToList();
      List<PortfolioSegmentItem> segments = visibleItems
        .Select((item, index) => new PortfolioSegmentItem
        {
          Color = GetChartColor(index),
          FullLabel = item.TypeName,
          Href = GetTypeHref(item.Type),
          Label = GetCompactLabel(item.TypeName),
          Percentage = $"{item.CurrentTotal / total * 100m:F0}%",
          Value = item.CurrentTotal,
        })
        .ToList();

      if (TypeSummaries.Count > visibleItems.Count)
      {
        decimal othersTotal = TypeSummaries.Skip(visibleItems.Count).Sum(item => item.CurrentTotal);
        segments.Add(new PortfolioSegmentItem
        {
          Color = GetChartColor(segments.Count),
          FullLabel = Loc["Others"],
          Href = AllAllocationsHref,
          Label = Loc["Others"],
          Percentage = $"{othersTotal / total * 100m:F0}%",
          Value = othersTotal,
        });
      }

      return segments;
    }

    private List<PortfolioCurrencyItem> GetCurrencyItems()
    {
      decimal total = CurrencySummaries.Sum(item => item.CurrentTotal);
      if (total <= 0m)
      {
        return new List<PortfolioCurrencyItem>();
      }

      return CurrencySummaries
        .Take(5)
        .Select((item, index) =>
        {
          decimal percentage = item.CurrentTotal / total * 100m;
          return new PortfolioCurrencyItem
          {
            Currency = item.Currency,
            Color = GetChartColor(index),
            Percentage = $"{percentage:F0}%",
            FullLabel = $"{item.Currency}: {percentage:F0}% • {Helper.FormatBrl(item.CurrentTotal)}",
          };
        })
        .ToList();
    }

    private static string GetTypeHref(AllocationType type)
    {
      return $"allocations?view=list&type={type}";
    }

    private static string GetCurrencyHref(Currency currency)
    {
      return $"allocations?view=list&currency={currency}";
    }

    private static string GetAccountHref(int accountId)
    {
      return $"allocations?view=list&accountId={accountId}";
    }

    private static string GetUpcomingHref()
    {
      return "allocations?view=list&expires=upcoming";
    }

    private static string GetPositionLabel(TopPositionVm item)
    {
      return string.IsNullOrWhiteSpace(item.Initials)
        ? GetFallbackLabel(item.Name)
        : item.Initials;
    }

    private static string GetPositionLabel(UpcomingExpirationVm item)
    {
      return string.IsNullOrWhiteSpace(item.Initials)
        ? GetFallbackLabel(item.Name)
        : item.Initials;
    }

    private static string GetCompactLabel(string? label)
    {
      string normalizedLabel = GetFallbackLabel(label);
      if (normalizedLabel.Length <= 10)
      {
        return normalizedLabel;
      }

      string[] words = normalizedLabel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length > 1)
      {
        string lastWord = words[^1];
        if (lastWord.Length <= 10)
        {
          return lastWord;
        }

        string firstWord = words[0];
        if (firstWord.Length <= 10)
        {
          return firstWord;
        }
      }

      return normalizedLabel[..10];
    }

    private static string GetFallbackLabel(string? label)
    {
      return string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
    }

    private Task OpenAllocationAsync(int allocationId)
    {
      return OnOpenAllocation.InvokeAsync(allocationId);
    }

    private void OnValuesVisibilityChanged()
    {
      _ = InvokeAsync(StateHasChanged);
    }

    private sealed class PortfolioSegmentItem
    {
      public string Color { get; set; } = string.Empty;

      public string FullLabel { get; set; } = string.Empty;

      public string Href { get; set; } = string.Empty;

      public string Label { get; set; } = string.Empty;

      public string Percentage { get; set; } = string.Empty;

      public decimal Value { get; set; }
    }

    private sealed class PortfolioCurrencyItem
    {
      public string Color { get; set; } = string.Empty;

      public Currency Currency { get; set; }

      public string FullLabel { get; set; } = string.Empty;

      public string Percentage { get; set; } = string.Empty;
    }
  }
}
