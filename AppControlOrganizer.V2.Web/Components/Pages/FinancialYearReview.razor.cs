//-----------------------------------------------------------------------
// <copyright file="FinancialYearReview.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages;

using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Security.Claims;

/// <summary>
/// Code-behind for the annual financial review page.
/// </summary>
public partial class FinancialYearReview : ComponentBase, IDisposable
{
  [Inject]
  private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

  [Inject]
  private FinancialYearReviewService YearReviewService { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  /// <summary>Gets or sets the requested year from the query string.</summary>
  [Parameter]
  [SupplyParameterFromQuery(Name = "year")]
  public int? RequestedYear { get; set; }

  private AppControlOrganizer.V2.Web.Services.Financial.FinancialYearReview? review;
  private string userId = LifePlannerContext.DemoUserId;
  private int selectedYear = DateTime.Now.Year;
  private bool isLoading = true;
  private string errorMessage = string.Empty;
  private decimal maxMonthValue = 1m;
  private decimal cashChartMin = 0m;
  private decimal cashChartMax = 1m;

  private IEnumerable<int> AvailableYears
  {
    get
    {
      if (this.review?.AvailableYears.Count > 0)
      {
        return this.review.AvailableYears;
      }

      return new[] { this.selectedYear };
    }
  }

  private IEnumerable<FinancialYearReviewMonth> TopIncomeMonths
  {
    get
    {
      return this.review?.Months
        .Where(month => month.OperatingIncome > 0m)
        .OrderByDescending(month => month.OperatingIncome)
        .Take(3) ?? Enumerable.Empty<FinancialYearReviewMonth>();
    }
  }

  private IEnumerable<FinancialYearReviewMonth> TopExpenseMonths
  {
    get
    {
      return this.review?.Months
        .Where(month => month.OperatingExpense > 0m)
        .OrderByDescending(month => month.OperatingExpense)
        .Take(3) ?? Enumerable.Empty<FinancialYearReviewMonth>();
    }
  }

  private FinancialYearReviewCategory? LeadingExpenseCategory
  {
    get
    {
      return this.review?.TopExpenseCategories.FirstOrDefault();
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.ValuesVis.OnChange -= this.OnValuesVisibilityChanged;
  }

  /// <inheritdoc />
  protected override void OnInitialized()
  {
    this.ValuesVis.OnChange += this.OnValuesVisibilityChanged;
  }

  /// <inheritdoc />
  protected override async Task OnParametersSetAsync()
  {
    AuthenticationState state = await this.AuthStateProvider.GetAuthenticationStateAsync();
    this.userId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? LifePlannerContext.DemoUserId;
    int requested = this.RequestedYear ?? DateTime.Now.Year;
    if (requested < 2000 || requested > DateTime.Now.Year + 1)
    {
      requested = DateTime.Now.Year;
    }

    if (this.review?.Year == requested && !this.isLoading)
    {
      return;
    }

    this.selectedYear = requested;
    await this.LoadAsync();
  }

  private async Task LoadAsync()
  {
    this.isLoading = true;
    this.errorMessage = string.Empty;
    try
    {
      this.review = await this.YearReviewService.GetAsync(this.userId, this.selectedYear, CancellationToken.None);
      this.maxMonthValue = this.review.Months
        .SelectMany(month => new[] { month.OperatingIncome, month.OperatingExpense })
        .DefaultIfEmpty(1m)
        .Max();
      if (this.maxMonthValue <= 0m)
      {
        this.maxMonthValue = 1m;
      }

      this.cashChartMin = this.review.Months
        .SelectMany(month => new[] { month.OperatingIncome, month.OperatingExpense, month.Result })
        .Append(0m)
        .Min();
      this.cashChartMax = this.review.Months
        .SelectMany(month => new[] { month.OperatingIncome, month.OperatingExpense, month.Result })
        .Append(1m)
        .Max();
      if (this.cashChartMax <= this.cashChartMin)
      {
        this.cashChartMax = this.cashChartMin + 1m;
      }
    }
    catch (Exception exception)
    {
      this.errorMessage = this.Loc.Format("YearReview.LoadError", exception.Message);
    }
    finally
    {
      this.isLoading = false;
    }
  }

  private Task RefreshAsync()
  {
    return this.LoadAsync();
  }

  private void OnYearChanged(ChangeEventArgs args)
  {
    if (args.Value == null || !int.TryParse(args.Value.ToString(), out int year))
    {
      return;
    }

    Dictionary<string, string?> query = new Dictionary<string, string?>
    {
      ["year"] = year.ToString(CultureInfo.InvariantCulture),
    };
    this.Navigation.NavigateTo(QueryHelpers.AddQueryString("finance/year-review", query));
  }

  private void OnValuesVisibilityChanged()
  {
    _ = this.InvokeAsync(this.StateHasChanged);
  }

  private string FormatMoney(decimal value)
  {
    return this.ValuesVis.Mask(Helper.FormatBrl(value));
  }

  private string FormatCompactMoney(decimal value)
  {
    decimal absolute = Math.Abs(value);
    if (absolute >= 1000000m)
    {
      return this.ValuesVis.Mask($"{(value / 1000000m).ToString("0.#", this.Loc.Culture)}M");
    }

    if (absolute >= 1000m)
    {
      return this.ValuesVis.Mask($"{(value / 1000m).ToString("0.#", this.Loc.Culture)}k");
    }

    return this.FormatMoney(value);
  }

  private string FormatPercent(decimal value)
  {
    return $"{value.ToString("0.#", this.Loc.Culture)}%";
  }

  private string FormatPositionMoney(FinancialYearReviewInvestmentPosition position, decimal value)
  {
    if (string.Equals(position.Currency, "BRL", StringComparison.OrdinalIgnoreCase))
    {
      return this.FormatMoney(value);
    }

    return this.ValuesVis.Mask($"{position.Currency} {value.ToString("N2", this.Loc.Culture)}");
  }

  private string FormatInvariantPercent(decimal value)
  {
    decimal clamped = Math.Clamp(value, 0m, 100m);
    return $"{clamped.ToString("0.##", CultureInfo.InvariantCulture)}%";
  }

  private string GetGaugePercent(decimal value)
  {
    return Math.Clamp(value, 0m, 100m).ToString("0.##", CultureInfo.InvariantCulture);
  }

  private string GetSignedGaugePercent(decimal value)
  {
    return Math.Clamp(Math.Abs(value), 0m, 100m).ToString("0.##", CultureInfo.InvariantCulture);
  }

  private string BuildCashChartPoints(Func<FinancialYearReviewMonth, decimal> selector)
  {
    if (this.review == null || this.review.Months.Count == 0)
    {
      return string.Empty;
    }

    decimal range = this.cashChartMax - this.cashChartMin;
    return string.Join(
      " ",
      this.review.Months.Select((month, index) =>
      {
        decimal x = 16m + (index * 70m);
        decimal y = 164m - ((selector(month) - this.cashChartMin) / range * 128m);
        return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
      }));
  }

  private string BuildCashChartAreaPoints(Func<FinancialYearReviewMonth, decimal> selector)
  {
    string points = this.BuildCashChartPoints(selector);
    if (string.IsNullOrWhiteSpace(points))
    {
      return string.Empty;
    }

    return $"16,164 {points} 786,164";
  }

  private string GetCashChartZeroY()
  {
    decimal range = this.cashChartMax - this.cashChartMin;
    decimal y = 164m - ((0m - this.cashChartMin) / range * 128m);
    return Math.Clamp(y, 36m, 164m).ToString("0.##", CultureInfo.InvariantCulture);
  }

  private string GetCategoryTone(int index)
  {
    string[] colors =
    {
      "#ef4444",
      "#f97316",
      "#eab308",
      "#06b6d4",
      "#6366f1",
      "#8b5cf6",
    };
    return colors[index % colors.Length];
  }

  private string GetInvestmentReturnWidth(FinancialYearReviewInvestmentPosition position)
  {
    decimal maxReturn = this.review?.InvestmentPositions
      .Select(item => Math.Abs(item.ReturnPercentage))
      .DefaultIfEmpty(1m)
      .Max() ?? 1m;
    if (maxReturn <= 0m)
    {
      maxReturn = 1m;
    }

    decimal width = Math.Clamp(Math.Abs(position.ReturnPercentage) / maxReturn * 100m, 4m, 100m);
    return width.ToString("0.##", CultureInfo.InvariantCulture);
  }

  private string GetInvestmentToneColor()
  {
    if (this.review?.InvestmentProfit < 0m)
    {
      return "#dc2626";
    }

    return "#16a34a";
  }

  private string BuildCategoryPieStyle()
  {
    if (this.review == null || this.review.TopExpenseCategories.Count == 0)
    {
      return "conic-gradient(#e5e7eb 0 100%)";
    }

    string[] colors =
    {
      "#ef4444",
      "#f97316",
      "#eab308",
      "#06b6d4",
      "#6366f1",
    };
    decimal cursor = 0m;
    List<string> segments = new List<string>();
    for (int index = 0; index < this.review.TopExpenseCategories.Count; index++)
    {
      FinancialYearReviewCategory category = this.review.TopExpenseCategories[index];
      decimal next = Math.Clamp(cursor + category.Percentage, 0m, 100m);
      segments.Add($"{colors[index % colors.Length]} {cursor.ToString("0.##", CultureInfo.InvariantCulture)}% {next.ToString("0.##", CultureInfo.InvariantCulture)}%");
      cursor = next;
    }

    if (cursor < 100m)
    {
      segments.Add($"#e5e7eb {cursor.ToString("0.##", CultureInfo.InvariantCulture)}% 100%");
    }

    return $"conic-gradient({string.Join(", ", segments)})";
  }

  private IReadOnlyList<FinancialCompositionItem> GetCompositionItems()
  {
    if (this.review == null)
    {
      return Array.Empty<FinancialCompositionItem>();
    }

    decimal total = new[] { this.review.AccountCash, this.review.EmergencyReserve, this.review.IdleCash, this.review.LongTermInvestments }
      .Where(value => value > 0m)
      .DefaultIfEmpty(0m)
      .Sum();
    if (total <= 0m)
    {
      return Array.Empty<FinancialCompositionItem>();
    }

    return new[]
    {
      new FinancialCompositionItem(this.Loc["Account cash"], this.review.AccountCash / total * 100m, "#2563eb"),
      new FinancialCompositionItem(this.Loc["Emergency reserve"], this.review.EmergencyReserve / total * 100m, "#16a34a"),
      new FinancialCompositionItem(this.Loc["Idle cash"], this.review.IdleCash / total * 100m, "#f59e0b"),
      new FinancialCompositionItem(this.Loc["Long-term investments"], this.review.LongTermInvestments / total * 100m, "#7c3aed"),
    }.Where(item => item.Percentage > 0m).ToList();
  }

  private string GetAnalysisIcon(string key)
  {
    return key switch
    {
      "YearReview.Analysis.PositiveCashFlow" => "bi bi-graph-up-arrow text-success",
      "YearReview.Analysis.NegativeCashFlow" => "bi bi-graph-down-arrow text-danger",
      "YearReview.Analysis.StrongSavings" => "bi bi-piggy-bank text-success",
      "YearReview.Analysis.LowSavings" => "bi bi-piggy-bank text-warning",
      "YearReview.Analysis.ConcentratedExpenses" => "bi bi-bullseye text-danger",
      "YearReview.Analysis.DistributedExpenses" => "bi bi-diagram-3 text-primary",
      "YearReview.Analysis.PositiveInvestments" => "bi bi-arrow-up-right-circle text-success",
      "YearReview.Analysis.NegativeInvestments" => "bi bi-arrow-down-right-circle text-danger",
      "YearReview.Analysis.UnpricedPositions" => "bi bi-exclamation-circle text-warning",
      "YearReview.Analysis.PricedPositions" => "bi bi-check-circle text-success",
      _ => "bi bi-lightbulb text-primary",
    };
  }

  private string GetMonthName(int month)
  {
    return this.Loc[CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)];
  }

  private string GetShortMonthName(int month)
  {
    string monthName = this.GetMonthName(month);
    return monthName.Length <= 3 ? monthName : monthName[..3];
  }

  private string GetTransactionsHref(int? month = null, bool? income = null)
  {
    Dictionary<string, string?> query = new Dictionary<string, string?>
    {
      ["year"] = this.selectedYear.ToString(CultureInfo.InvariantCulture),
    };
    if (month.HasValue)
    {
      query["month"] = month.Value.ToString(CultureInfo.InvariantCulture);
    }

    if (income.HasValue)
    {
      query["type"] = income.Value ? "income" : "expense";
    }

    return QueryHelpers.AddQueryString("transactions", query);
  }

  private string GetCategoryHref(FinancialYearReviewCategory category)
  {
    Dictionary<string, string?> query = new Dictionary<string, string?>
    {
      ["year"] = this.selectedYear.ToString(CultureInfo.InvariantCulture),
      ["type"] = "expense",
    };
    if (category.CategoryId.HasValue)
    {
      query["categoryId"] = category.CategoryId.Value.ToString(CultureInfo.InvariantCulture);
    }
    else if (category.LegacyCategory.HasValue)
    {
      query["category"] = category.LegacyCategory.Value.ToString();
    }

    return QueryHelpers.AddQueryString("transactions", query);
  }

  private string GetMonthBarHeight(decimal value)
  {
    decimal height = Math.Clamp(value / this.maxMonthValue * 100m, 4m, 100m);
    return height.ToString("0.##", CultureInfo.InvariantCulture);
  }

  private sealed class FinancialCompositionItem
  {
    public FinancialCompositionItem(string label, decimal percentage, string color)
    {
      this.Label = label;
      this.Percentage = percentage;
      this.Color = color;
    }

    public string Label { get; }

    public decimal Percentage { get; }

    public string Color { get; }
  }
}
