//-----------------------------------------------------------------------
// <copyright file="AllocationsListView.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace AppControlOrganizer.V2.Web.Components.Pages.Allocations
{
	public partial class AllocationsListView : ComponentBase, IDisposable
	{
		// ── Sort ──────────────────────────────────────────────────────────────────

		/// <summary>Sortable columns available in List view.</summary>
		internal enum SortColumn { Name, Account, Type, Qty, Invested, Current }

		/// <summary>Single sort step: column + direction.</summary>
		internal record SortStep(SortColumn Column, bool Ascending);

		/// <summary>
		/// Ordered chain of active sort steps. First entry is primary, next is tiebreaker, etc.
		/// Clicking a column header: if Shift is held, appends/toggles it; otherwise resets chain.
		/// </summary>
		private List<SortStep> sortSteps = new List<SortStep>();

		/// <summary>
		/// Handles a header click. Shift+click appends/toggles the column; plain click resets.
		/// </summary>
		private void OnColumnClick(SortColumn col, MouseEventArgs e)
		{
			SortStep? existing = sortSteps.FirstOrDefault(s => s.Column == col);

			if (e.ShiftKey)
			{
				if (existing == null)
				{
					sortSteps.Add(new SortStep(col, true));
				}
				else if (existing.Ascending)
				{
					sortSteps[sortSteps.IndexOf(existing)] = existing with { Ascending = false };
				}
				else
				{
					sortSteps.Remove(existing);
				}
			}
			else
			{
				bool newAsc = existing == null || !existing.Ascending;
				sortSteps = new List<SortStep> { new SortStep(col, newAsc) };
			}
		}

		/// <summary>Handles the Name column click.</summary>
		private void OnNameClick(MouseEventArgs e) => OnColumnClick(SortColumn.Name, e);

		/// <summary>Handles the Account column click.</summary>
		private void OnAccountClick(MouseEventArgs e) => OnColumnClick(SortColumn.Account, e);

		/// <summary>Handles the Type column click.</summary>
		private void OnTypeClick(MouseEventArgs e) => OnColumnClick(SortColumn.Type, e);

		/// <summary>Handles the Qty column click.</summary>
		private void OnQtyClick(MouseEventArgs e) => OnColumnClick(SortColumn.Qty, e);

		/// <summary>Handles the Invested column click.</summary>
		private void OnInvestedClick(MouseEventArgs e) => OnColumnClick(SortColumn.Invested, e);

		/// <summary>Handles the Current column click.</summary>
		private void OnCurrentClick(MouseEventArgs e) => OnColumnClick(SortColumn.Current, e);

		/// <summary>Clears all active sort steps and restores the default order.</summary>
		private void ClearSort()
		{
			sortSteps.Clear();
		}

		/// <summary>Returns a sort indicator character for the column header.</summary>
		private string SortIndicator(SortColumn col)
		{
			SortStep? step = sortSteps.FirstOrDefault(s => s.Column == col);
			if (step == null)
			{
				return string.Empty;
			}

			int rank = sortSteps.IndexOf(step) + 1;
			string arrow = step.Ascending ? "↑" : "↓";
			return sortSteps.Count > 1 ? $"{arrow}{rank}" : arrow;
		}

		/// <summary>Returns active sort CSS class for a header column.</summary>
		private string SortCss(SortColumn col) =>
		  sortSteps.Any(s => s.Column == col) ? "text-white" : "text-secondary";

		/// <summary>Applies the active sort chain to the allocation list.</summary>
		private IReadOnlyList<AllocationEntity> GetSorted()
		{
			if (!sortSteps.Any())
			{
				return Allocations;
			}

			IOrderedEnumerable<AllocationEntity>? ordered = null;

			foreach (SortStep step in sortSteps)
			{
				Func<AllocationEntity, object?> key = step.Column switch
				{
					SortColumn.Name => a => a.Name,
					SortColumn.Account => a => a.Account?.Name ?? string.Empty,
					SortColumn.Type => a => a.AllocationType.ToString(),
					SortColumn.Qty => a => (object)a.Quantity,
					SortColumn.Invested => a => (object)(a.BuyUnitValue * a.Quantity),
					SortColumn.Current => a => (object)GetCurrentForSort(a),
					_ => a => a.Name,
				};

				if (ordered == null)
				{
					ordered = step.Ascending
					  ? Allocations.OrderBy(key)
					  : Allocations.OrderByDescending(key);
				}
				else
				{
					ordered = step.Ascending
					  ? ordered.ThenBy(key)
					  : ordered.ThenByDescending(key);
				}
			}

			return ordered!.ToList();
		}

		/// <summary>Returns the current BRL value used for sort (0 when not convertible).</summary>
		private decimal GetCurrentForSort(AllocationEntity a)
		{
      FinancialAllocationPosition position = FinancialPositionCalculator.CalculateAllocationPosition(
        a,
        LatestQuotes,
        PricingService,
        FinancialFactsService.ResolvePurpose);
      return position.CurrentIsConvertible ? position.CurrentAmountBrl : 0m;
		}

		[Inject]
		private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;
		[Inject]
		public AllocationQuotePricingService PricingService { get; set; } = null!;

		[Parameter] public IReadOnlyList<AllocationEntity> Allocations { get; set; } = Array.Empty<AllocationEntity>();

		[Parameter] public IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> LatestQuotes { get; set; } = new Dictionary<QuoteKey, MarketQuoteSnapshot>();
		[Parameter] public MarketQuoteSnapshot? UsdBrlQuote { get; set; }
		[Parameter] public bool HasUsdBrlQuote { get; set; }
    [Parameter] public string QuotesHref { get; set; } = "allocations?panel=quotes";

		[Parameter] public EventCallback<AllocationEntity> OnEdit { get; set; }
		[Parameter] public EventCallback<AllocationEntity> OnDelete { get; set; }

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
		private string BuildAutoCalcTooltip(AllocationEntity alloc)
		{
			string tooltip = $"{Loc["Source:"]} BACEN SGS (API)";
			tooltip += $"\n{Loc["Yield:"]} {alloc.ProfitType} {alloc.ProfitPercentage}%";

			if (alloc.ProfitDate.HasValue)
			{
				tooltip += $"\n{Loc["Start:"]} {alloc.ProfitDate.Value.ToString("d", Loc.Culture)}";

				int businessDays = CountBusinessDays(alloc.ProfitDate.Value.AddDays(1), DateTime.UtcNow.Date);
				tooltip += $"\n{Loc["Business days:"]} {businessDays}";
			}

			if (alloc.CurrentUnitValue.HasValue)
			{
				var culture = Loc.Language == "pt"
				  ? System.Globalization.CultureInfo.GetCultureInfo("pt-BR")
				  : System.Globalization.CultureInfo.InvariantCulture;
				tooltip += $"\n{Loc["Current value:"]} {alloc.CurrentUnitValue.Value.ToString("N2", culture)}";
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
		/// Formats quantity with appropriate decimal places.
		/// Shows N2 for integers; up to 8 decimals with trailing zeros trimmed for decimals.
		/// </summary>
		private static string FormatQuantity(decimal quantity)
		{
			if (quantity == decimal.Truncate(quantity))
			{
				return quantity.ToString("N2", CultureInfo.InvariantCulture);
			}

			string formatted = quantity.ToString("N8", CultureInfo.InvariantCulture);
			formatted = formatted.TrimEnd('0');

			if (formatted.EndsWith('.'))
			{
				formatted = formatted.TrimEnd('.');
			}

			return formatted;
		}

    private static string GetAccountHref(int accountId)
    {
      return $"accounts?accountId={accountId}";
    }

    private static string GetTypeHref(AllocationType type)
    {
      return $"allocations?view=list&type={type}";
    }

    private static string GetCurrencyHref(Currency currency)
    {
      return $"allocations?view=list&currency={currency}";
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

		/// <inheritdoc />
		public void Dispose() => ValuesVis.OnChange -= OnValuesVisibilityChanged;

		private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

		/// <inheritdoc />
		protected override void OnInitialized() => ValuesVis.OnChange += OnValuesVisibilityChanged;

	}
}
