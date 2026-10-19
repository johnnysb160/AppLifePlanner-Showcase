//-----------------------------------------------------------------------
// <copyright file="Transactions.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Components.Shared.Categories;
using AppControlOrganizer.V2.Web.Components.Shared.Imports;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Components.Shared.Common;
using AppControlOrganizer.V2.Shared.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AppControlOrganizer.V2.Web.Components.Pages
{
  /// <summary>
  /// Code-behind for Transactions.razor (Transactions page). Manages income/expense records with CRUD operations.
  /// </summary>
  public partial class Transactions : ComponentBase, IDisposable
  {
    private static readonly string[] IncomeSummaryChartColors =
    {
      "#16a34a",
      "#22c55e",
      "#14b8a6",
      "#0ea5e9",
      "#94a3b8",
    };

    private static readonly string[] ExpenseSummaryChartColors =
    {
      "#ef4444",
      "#f97316",
      "#eab308",
      "#d946ef",
      "#94a3b8",
    };

    private static readonly string[] BalanceSummaryChartColors =
    {
      "#22c55e",
      "#d97706",
      "#ef4444",
    };

    private sealed class SummarySegmentLegendItem
    {
      public string Color { get; set; } = string.Empty;

      public string Label { get; set; } = string.Empty;

      public string Percentage { get; set; } = string.Empty;
    }

    [Inject]
    private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;

    /// <summary>
    /// Flag to show/hide filter section.
    /// </summary>
    private bool showFilters = false;

    /// <summary>
    /// Gets or sets the repository used to manage Transaction data.
    /// </summary>
    [Inject]
        public ITransactionRepository transactionRepository { get; set; } = null!;

    /// <summary>
    /// Gets or sets the repository used to access account data.
    /// </summary>
    [Inject]
    public IAccountRepository accountRepository { get; set; } = null!;

    [Inject]
    public CategoryService CategoryService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    [Inject]
    private IDbContextFactory<LifePlannerContext> DbContextFactory { get; set; } = null!;

    /// <summary>Gets or sets the requested transaction month.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "month")]
    public int? RequestedMonth { get; set; }

    /// <summary>Gets or sets the requested transaction year.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "year")]
    public int? RequestedYear { get; set; }

    /// <summary>Gets or sets the requested income or expense filter.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "type")]
    public string? RequestedType { get; set; }

    /// <summary>Gets or sets the requested transaction category.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "category")]
    public string? RequestedCategory { get; set; }

    /// <summary>Gets or sets the requested category identifier.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "categoryId")]
    public int? RequestedCategoryId { get; set; }

    /// <summary>Gets or sets the requested transaction day.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "day")]
    public int? RequestedDay { get; set; }

    /// <summary>Gets or sets the requested account filter.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "accountId")]
    public int? RequestedAccountId { get; set; }

    /// <summary>Gets or sets the requested credit-card filter.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "cardId")]
    public int? RequestedCardId { get; set; }

    /// <summary>Gets or sets the requested transaction source filter.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "sourceKind")]
    public string? RequestedSourceKind { get; set; }

    /// <summary>Gets or sets the requested planning impact filter.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "planningImpact")]
    public string? RequestedPlanningImpact { get; set; }

    /// <summary>
    /// Financial service.
    /// </summary>
    [Inject]
    public FinancialSyncService financialSyncService { get; set;} = null!;

    #region Dropdown Items

    private IEnumerable<DropdownItem<int?>> MonthFilterItems =>
      Enumerable.Range(1, 12).Select(m => new DropdownItem<int?>(m, GetMonthName(m)));

    private static IEnumerable<DropdownItem<int?>> YearFilterItems =>
      Enumerable.Range(DateTime.Now.Year - 5, 7).Select(y => new DropdownItem<int?>(y, y.ToString()));

    private IEnumerable<DropdownItem<string>> CategoryFilterItems =>
      new[] { new DropdownItem<string>(string.Empty, Loc["All"]) }
        .Concat(Enum.GetValues<Category>()
              .Select(c => new DropdownItem<string>(c.ToString(), Loc[c.GetDisplayName()])));

    private IEnumerable<DropdownItem<string>> TypeFilterItems => new[]
    {
      new DropdownItem<string>(string.Empty, Loc["All"]),
      new DropdownItem<string>("income", Loc["Income"]),
      new DropdownItem<string>("expense", Loc["Expenses"]),
    };

    private IEnumerable<DropdownItem<int?>> DayFilterItems =>
      new[] { new DropdownItem<int?>(null, Loc["All"]) }
        .Concat(Enumerable.Range(1, GetDaysInSelectedMonth())
          .Select(day => new DropdownItem<int?>(day, day.ToString())));

    private IEnumerable<DropdownItem<int?>> AccountFilterItems =>
      new[] { new DropdownItem<int?>(null, Loc["All"]) }
        .Concat(accounts.Select(account => new DropdownItem<int?>(account.Id, account.Name)));

    private IEnumerable<DropdownItem<string>> SourceKindFilterItems =>
      new[] { new DropdownItem<string>(string.Empty, Loc["All"]) }
        .Concat(Enum.GetValues<TransactionSourceKind>()
          .Select(source => new DropdownItem<string>(source.ToString(), Loc[GetSourceKindLabel(source)])));

    private IEnumerable<DropdownItem<string>> PlanningImpactFilterItems =>
      new[] { new DropdownItem<string>(string.Empty, Loc["All"]) }
        .Concat(Enum.GetValues<TransactionPlanningImpact>()
          .Select(impact => new DropdownItem<string>(impact.ToString(), Loc[GetPlanningImpactLabel(impact)])));

    #endregion

    #region State Fields

    // All user accounts for the account selector in the upsert modal
    private List<AccountEntity> accounts = new List<AccountEntity>();

    private List<CategoryDisplayDto> categories = new List<CategoryDisplayDto>();

    // List of transactions for current filter
    private List<TransactionEntity> transactions = new List<TransactionEntity>();

    // Current transaction being edited/created
    private TransactionEntity currentTransaction = new TransactionEntity();

    // Transaction selected for deletion
    private TransactionEntity? transactionToDelete;

    // Filter state
    private int? selectedMonth = DateTime.Now.Month;
    private int? selectedYear = DateTime.Now.Year;
    private string selectedCategory = string.Empty;
    private int? selectedCategoryId;
    private string selectedType = string.Empty;
    private int? selectedDay;
    private int? selectedAccountId;
    private int? selectedCardId;
    private string selectedSourceKind = string.Empty;
    private string selectedPlanningImpact = string.Empty;
    private bool hasRouteFilters;

    // Summary totals
    private decimal totalIncome;
    private decimal totalExpense;
    private decimal totalContributions;
    private decimal balance;
    private bool usesMonthlyContributionResult;

    // UI state flags
    private bool isLoading = true;
    // Section-specific loading flags for summary cards and list refresh
    private bool isLoadingSummary = false;
    private bool isLoadingList = false;
    private bool showModal = false;
    private bool showDeleteConfirm = false;
    private bool showBulkDeleteConfirm = false;
    private bool showConfirmDiscard = false;
    private bool isEditing = false;
    private bool isSaving = false;
    private bool isDirty = false;
    private string message = string.Empty;
    private bool isError = false;

    // Import flow reference
    private TransactionImportFlow? importFlow;

    // Category modal reference
    private CategoryManagementModal? categoryModal;

    // Sorting state
    private string sortColumn = "Date";
    private bool sortAscending = false;

    // Selection state for bulk operations
    private HashSet<int> selectedIds = new HashSet<int>();

    #endregion

    #region Computed Properties

    // Check if all visible transactions are selected
    private bool IsAllSelected => transactions.Count > 0 && selectedIds.Count == transactions.Count;

    #endregion

    #region Summary Helpers

    private int GetIncomeCount()
    {
      return transactions.Count(transaction => transaction.Income);
    }

    private int GetExpenseCount()
    {
      return transactions.Count(transaction => !transaction.Income);
    }

    private decimal GetAverageIncome()
    {
      int count = GetIncomeCount();
      return count == 0 ? 0m : totalIncome / count;
    }

    private decimal GetAverageExpense()
    {
      int count = GetExpenseCount();
      return count == 0 ? 0m : totalExpense / count;
    }

    private decimal GetLargestIncome()
    {
      return transactions
        .Where(transaction => transaction.Income)
        .Select(transaction => transaction.Value)
        .DefaultIfEmpty(0m)
        .Max();
    }

    private int GetActiveDayCount()
    {
      return transactions
        .Select(transaction => transaction.Date.Date)
        .Distinct()
        .Count();
    }

    private string GetBalanceStateLabel()
    {
      if (transactions.Count == 0)
      {
        return Loc["No activity"];
      }

      return balance >= 0m ? Loc["Surplus"] : Loc["Deficit"];
    }

    private string GetTransactionCategoryName(TransactionEntity transaction)
    {
      if (transaction.CategoryId.HasValue)
      {
        CategoryDisplayDto? category = categories.FirstOrDefault(item => item.Id == transaction.CategoryId.Value);
        if (category != null)
        {
          return Loc[category.Name];
        }
      }

      return Loc[transaction.Category.GetDisplayName()];
    }

    private List<(string Label, decimal Value)> GetCategorySegments(bool income)
    {
      List<(string Label, decimal Value)> categoriesByValue = transactions
        .Where(transaction => transaction.Income == income)
        .GroupBy(GetTransactionCategoryName)
        .Select(group => (Label: group.Key, Value: group.Sum(transaction => transaction.Value)))
        .OrderByDescending(item => item.Value)
        .ToList();

      List<(string Label, decimal Value)> segments = categoriesByValue.Take(4).ToList();
      if (categoriesByValue.Count > 4)
      {
        decimal remainingValue = categoriesByValue.Skip(4).Sum(item => item.Value);
        segments.Add((Loc["Others"], remainingValue));
      }

      return segments;
    }

    private List<(string Label, decimal Value)> GetBalanceSegments()
    {
      List<(string Label, decimal Value)> segments = new List<(string Label, decimal Value)>
      {
        (Loc["Operating income"], totalIncome),
        (Loc["Expenses"], totalExpense),
      };

      if (usesMonthlyContributionResult && totalContributions > 0m)
      {
        segments.Insert(1, (Loc["Contributions"], totalContributions));
      }

      return segments;
    }

    private string BuildIncomeDonutStyle()
    {
      return BuildDonutStyle(GetCategorySegments(true), IncomeSummaryChartColors);
    }

    private string BuildExpenseDonutStyle()
    {
      return BuildDonutStyle(GetCategorySegments(false), ExpenseSummaryChartColors);
    }

    private string BuildBalanceDonutStyle()
    {
      return BuildDonutStyle(GetBalanceSegments(), BalanceSummaryChartColors);
    }

    private static string BuildDonutStyle(
      IReadOnlyList<(string Label, decimal Value)> segments,
      IReadOnlyList<string> colors)
    {
      decimal total = segments.Sum(segment => segment.Value);
      if (total <= 0m)
      {
        return "conic-gradient(#e2e8f0 0% 100%)";
      }

      decimal start = 0m;
      List<string> stops = new List<string>();
      for (int index = 0; index < segments.Count; index++)
      {
        decimal end = index == segments.Count - 1
          ? 100m
          : start + (segments[index].Value / total * 100m);
        string startText = start.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string endText = end.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string color = index == 0
          ? colors[index % colors.Count]
          : GetSoftChartColor(colors[index % colors.Count]);
        stops.Add($"{color} {startText}% {endText}%");
        start = end;
      }

      return $"conic-gradient({string.Join(", ", stops)})";
    }

    private static string GetSoftChartColor(string color)
    {
      return $"{color}7a";
    }

    private string GetCategoryDonutAriaLabel(bool income)
    {
      string heading = income ? Loc["Income distribution"] : Loc["Expense distribution"];
      return BuildDonutAriaLabel(heading, GetCategorySegments(income));
    }

    private string GetBalanceDonutAriaLabel()
    {
      return BuildDonutAriaLabel(Loc["Cash flow composition"], GetBalanceSegments());
    }

    private string BuildDonutAriaLabel(
      string heading,
      IReadOnlyList<(string Label, decimal Value)> segments)
    {
      decimal total = segments.Sum(segment => segment.Value);
      if (total <= 0m)
      {
        return $"{heading}: {Loc["No activity"]}";
      }

      IEnumerable<string> details = segments.Select(segment =>
      {
        decimal percentage = segment.Value / total * 100m;
        return $"{segment.Label}: {percentage:F0}%";
      });
      return $"{heading}: {string.Join(", ", details)}";
    }

    private string GetLeadingExpenseCategoryName()
    {
      List<(string Label, decimal Value)> segments = GetCategorySegments(false);
      return segments.Count == 0 ? Loc["No activity"] : segments[0].Label;
    }

    private decimal GetLeadingExpenseCategoryPercentage()
    {
      List<(string Label, decimal Value)> segments = GetCategorySegments(false);
      if (segments.Count == 0 || totalExpense <= 0m)
      {
        return 0m;
      }

      return segments[0].Value / totalExpense * 100m;
    }

    private List<SummarySegmentLegendItem> GetSegmentLegendItems(
      IReadOnlyList<(string Label, decimal Value)> segments,
      IReadOnlyList<string> colors)
    {
      decimal total = segments.Sum(segment => segment.Value);
      if (total <= 0m)
      {
        return new List<SummarySegmentLegendItem>
        {
          new SummarySegmentLegendItem
          {
            Color = "#cbd5e1",
            Label = Loc["None"],
            Percentage = "0%",
          },
        };
      }

      return segments
        .OrderByDescending(segment => segment.Value)
        .Take(5)
        .Select((segment, index) => new SummarySegmentLegendItem
        {
          Color = index == 0
            ? colors[index % colors.Count]
            : GetSoftChartColor(colors[index % colors.Count]),
          Label = GetCompactSegmentLabel(segment.Label),
          Percentage = $"{segment.Value / total * 100m:F0}%",
        })
        .ToList();
    }

    private string GetCompactSegmentLabel(string label)
    {
      string normalizedLabel = label.Trim();
      if (normalizedLabel.Length <= 8)
      {
        return normalizedLabel;
      }

      string[] words = normalizedLabel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length > 1)
      {
        string lastWord = words[^1];
        if (lastWord.Length <= 8)
        {
          return lastWord;
        }

        string firstWord = words[0];
        if (firstWord.Length <= 8)
        {
          return firstWord;
        }
      }

      return normalizedLabel[..8];
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Lifecycle: Initialize component and subscribe to events.
    /// </summary>
        private bool initialized = false;
        private string lastAppliedUri = string.Empty;

        /// <inheritdoc />
        public void Dispose() => ValuesVis.OnChange -= OnValuesVisibilityChanged;

        private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            ValuesVis.OnChange += OnValuesVisibilityChanged;
        }

        /// <inheritdoc />
        protected override async Task OnParametersSetAsync()
        {
            if (string.Equals(lastAppliedUri, Navigation.Uri, StringComparison.Ordinal))
            {
              return;
            }

            lastAppliedUri = Navigation.Uri;
            ApplyRouteFilters();

            if (initialized)
            {
              await LoadTransactions(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!initialized)
            {
                initialized = true;
                await SetDefaultMonthForDemoUserAsync();
                accounts = await accountRepository.SelectAsync(CancellationToken.None) ?? new List<AccountEntity>();
                accounts = accounts.Where(a => !a.Deleted).OrderBy(a => a.Name).ToList();
                categories = await CategoryService.GetAllAsync(CancellationToken.None);
                bool hadTransactions = transactions != null && transactions.Any();
                await LoadTransactions(CancellationToken.None, showSpinner: !hadTransactions);
                await InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// When no user is authenticated, automatically selects the most recent month that
        /// has demo transactions, so the page never opens empty for unauthenticated visitors.
        /// </summary>
        private async Task SetDefaultMonthForDemoUserAsync()
        {
            if (RequestedMonth is >= 1 and <= 12 && RequestedYear.HasValue)
            {
                return;
            }

            AuthenticationState auth = await AuthStateProvider.GetAuthenticationStateAsync();
            bool isAuthenticated = auth.User.FindFirst(ClaimTypes.NameIdentifier) != null;
            if (isAuthenticated)
            {
                return;
            }

            // Load all demo transactions (automatically scoped by EF to __DEMO__ user)
            List<TransactionEntity>? all = await transactionRepository.SelectAsync(CancellationToken.None);
            if (all == null || all.Count == 0)
            {
                return;
            }

            DateTime mostRecent = all.Max(t => t.Date);
            selectedMonth = mostRecent.Month;
            selectedYear  = mostRecent.Year;
        }

    /// <summary>
    /// Refresh wrapper used by UI button.
    /// </summary>
    private async Task RefreshTransactions()
    {
      await LoadTransactions(CancellationToken.None);
    }

    #endregion

    #region Data Loading

        private Task LoadTransactions()
        {
            // Called by UI bindings (SearchableDropdown @bind-Value:after). Show section spinners only.
            isLoadingSummary = true;
            isLoadingList = true;
            _ = InvokeAsync(StateHasChanged);

			return LoadTransactions(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
        }

        private async Task LoadTransactions(CancellationToken cancellationToken = default, bool showSpinner = false, bool onlyRefreshSections = false)
        {
            if (showSpinner)
            {
                isLoading = true;
            }

            if (onlyRefreshSections)
            {
                // Ensure summary/list spinners are visible before heavy work
                await InvokeAsync(StateHasChanged);
                await Task.Yield();
            }

            selectedIds.Clear();
            try
            {
                int year = selectedYear ?? DateTime.Now.Year;
                int month = selectedMonth ?? DateTime.Now.Month;
                int daysInMonth = DateTime.DaysInMonth(year, month);
                if (selectedDay.HasValue && selectedDay.Value > daysInMonth)
                {
                  selectedDay = null;
                }

                DateTime startDate = selectedDay.HasValue
                  ? new DateTime(year, month, selectedDay.Value)
                  : new DateTime(year, month, 1);
                DateTime endDate = selectedDay.HasValue
                  ? startDate
                  : startDate.AddMonths(1).AddDays(-1);

                List<TransactionEntity> monthlyTransactions = await transactionRepository.SelectByDateRangeAsync(
                  startDate,
                  endDate,
                  cancellationToken) ?? new List<TransactionEntity>();
                FinancialMonthlyCashFlow? monthlyCashFlow = null;
                if (CanUseMonthlyContributionResult())
                {
                  List<AllocationHistoryEntity> allocationHistories = await LoadMonthlyAllocationHistoriesAsync(
                    startDate,
                    endDate.AddDays(1),
                    cancellationToken);
                  monthlyCashFlow = FinancialMonthlyCashFlowCalculator.Calculate(
                    monthlyTransactions,
                    allocationHistories,
                    new DateOnly(year, month, 1));
                }

                transactions = monthlyTransactions;
                if (!string.IsNullOrEmpty(selectedCategory)
                    && Enum.TryParse<Category>(selectedCategory, out Category cat))
                {
                    transactions = transactions.Where(a => a.Category == cat).ToList();
                }

                if (selectedCategoryId.HasValue)
                {
                  transactions = transactions.Where(transaction => transaction.CategoryId == selectedCategoryId.Value).ToList();
                }

                if (string.Equals(selectedType, "income", StringComparison.OrdinalIgnoreCase))
                {
                    transactions = transactions.Where(transaction => transaction.Income).ToList();
                }
                else if (string.Equals(selectedType, "expense", StringComparison.OrdinalIgnoreCase))
                {
                    transactions = transactions.Where(transaction => !transaction.Income).ToList();
                }

                if (selectedAccountId.HasValue)
                {
                  transactions = transactions.Where(transaction => transaction.AccountId == selectedAccountId).ToList();
                }

                if (selectedCardId.HasValue)
                {
                  transactions = transactions.Where(transaction => transaction.CreditCardId == selectedCardId).ToList();
                }

                if (!string.IsNullOrEmpty(selectedSourceKind)
                  && Enum.TryParse<TransactionSourceKind>(selectedSourceKind, out TransactionSourceKind sourceKind))
                {
                  transactions = transactions.Where(transaction => transaction.SourceKind == sourceKind).ToList();
                }

                if (!string.IsNullOrEmpty(selectedPlanningImpact)
                  && Enum.TryParse<TransactionPlanningImpact>(selectedPlanningImpact, out TransactionPlanningImpact planningImpact))
                {
                  transactions = transactions.Where(transaction => transaction.PlanningImpact == planningImpact).ToList();
                }

                transactions = transactions.OrderByDescending(a => a.Date).ThenBy(a => a.Category).ToList();

                // Calculate totals
                totalContributions = 0m;
                usesMonthlyContributionResult = monthlyCashFlow != null;
                if (monthlyCashFlow != null)
                {
                  totalIncome = monthlyCashFlow.OperatingIncome;
                  totalExpense = monthlyCashFlow.OperatingExpense;
                  totalContributions = monthlyCashFlow.Contributions;
                  balance = monthlyCashFlow.ResultWithContributions;
                }
                else
                {
                  totalIncome = transactions.Where(a => a.Income).Sum(a => a.Value);
                  totalExpense = transactions.Where(a => !a.Income).Sum(a => a.Value);
                  balance = totalIncome - totalExpense;
                }
        showFilters = hasRouteFilters || transactions.Count == 0;
			}
			finally
            {
                if (showSpinner)
                {
                    isLoading = false;
                }

                if (onlyRefreshSections)
                {
                    isLoadingSummary = false;
                    isLoadingList = false;

					await InvokeAsync(StateHasChanged);
                }
            }
        }

    private bool CanUseMonthlyContributionResult()
    {
      return !selectedDay.HasValue
        && string.IsNullOrEmpty(selectedCategory)
        && !selectedCategoryId.HasValue
        && string.IsNullOrEmpty(selectedType)
        && !selectedAccountId.HasValue
        && !selectedCardId.HasValue
        && string.IsNullOrEmpty(selectedSourceKind)
        && string.IsNullOrEmpty(selectedPlanningImpact);
    }

    private async Task<List<AllocationHistoryEntity>> LoadMonthlyAllocationHistoriesAsync(
      DateTime monthStart,
      DateTime monthEndExclusive,
      CancellationToken cancellationToken)
    {
      string userId = await GetCurrentUserIdAsync();
      await using LifePlannerContext context = await DbContextFactory.CreateDbContextAsync(cancellationToken);
      return await context.AllocationHistories
        .AsNoTracking()
        .Where(history => history.OwnerUserId == userId
          && history.OccurredAt >= monthStart
          && history.OccurredAt < monthEndExclusive)
        .OrderByDescending(history => history.OccurredAt)
        .ToListAsync(cancellationToken);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
      AuthenticationState auth = await AuthStateProvider.GetAuthenticationStateAsync();
      return auth.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "__DEMO__";
    }

    private async Task GoToCurrentMonth(CancellationToken cancellationToken = default)
    {
      selectedMonth = DateTime.Now.Month;
      selectedYear = DateTime.Now.Year;
      selectedDay = null;
      await LoadTransactions(cancellationToken);
      NavigateToCurrentFilters(replace: true);
    }

    private Task GoToCurrentMonth()
    {
      return GoToCurrentMonth(CancellationToken.None);
    }

    private void ApplyRouteFilters()
    {
      selectedMonth = DateTime.Now.Month;
      selectedYear = DateTime.Now.Year;
      selectedDay = null;
      selectedCategory = string.Empty;
      selectedCategoryId = null;
      selectedType = string.Empty;
      selectedAccountId = null;
      selectedCardId = null;
      selectedSourceKind = string.Empty;
      selectedPlanningImpact = string.Empty;
      hasRouteFilters = false;

      if (RequestedMonth is >= 1 and <= 12)
      {
        selectedMonth = RequestedMonth;
        hasRouteFilters = true;
      }

      if (RequestedYear is >= 2000 and <= 2100)
      {
        selectedYear = RequestedYear;
        hasRouteFilters = true;
      }

      if (!string.IsNullOrWhiteSpace(RequestedCategory)
        && TryParseCategoryFilter(RequestedCategory, out Category category))
      {
        selectedCategory = category.ToString();
        hasRouteFilters = true;
      }

      if (RequestedCategoryId.HasValue && RequestedCategoryId.Value > 0)
      {
        selectedCategoryId = RequestedCategoryId.Value;
        hasRouteFilters = true;
      }

      if (RequestedDay.HasValue && RequestedDay.Value >= 1 && RequestedDay.Value <= 31)
      {
        selectedDay = RequestedDay.Value;
        hasRouteFilters = true;
      }

      if (RequestedAccountId.HasValue && RequestedAccountId.Value > 0)
      {
        selectedAccountId = RequestedAccountId.Value;
        hasRouteFilters = true;
      }

      if (RequestedCardId.HasValue && RequestedCardId.Value > 0)
      {
        selectedCardId = RequestedCardId.Value;
        hasRouteFilters = true;
      }

      if (string.Equals(RequestedType, "income", StringComparison.OrdinalIgnoreCase)
        || string.Equals(RequestedType, "expense", StringComparison.OrdinalIgnoreCase))
      {
        selectedType = string.Equals(RequestedType, "income", StringComparison.OrdinalIgnoreCase)
          ? "income"
          : "expense";
        hasRouteFilters = true;
      }

      if (!string.IsNullOrWhiteSpace(RequestedSourceKind)
        && Enum.TryParse<TransactionSourceKind>(RequestedSourceKind, true, out TransactionSourceKind sourceKind))
      {
        selectedSourceKind = sourceKind.ToString();
        hasRouteFilters = true;
      }

      if (!string.IsNullOrWhiteSpace(RequestedPlanningImpact)
        && Enum.TryParse<TransactionPlanningImpact>(RequestedPlanningImpact, true, out TransactionPlanningImpact planningImpact))
      {
        selectedPlanningImpact = planningImpact.ToString();
        hasRouteFilters = true;
      }
    }

    private string GetMonthName(int month) =>
      Loc[System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)];

    private int GetDaysInSelectedMonth()
    {
      int year = selectedYear ?? DateTime.Now.Year;
      int month = selectedMonth ?? DateTime.Now.Month;
      return DateTime.DaysInMonth(year, month);
    }

    private async Task OnFiltersChanged()
    {
      if (!string.IsNullOrEmpty(selectedCategory))
      {
        selectedCategoryId = null;
      }

      await LoadTransactions();
      NavigateToCurrentFilters(replace: true);
    }

    private void NavigateToCurrentFilters(bool replace)
    {
      Navigation.NavigateTo(GetTransactionsHref(), replace: replace);
    }

    private string GetTransactionsHref(
      bool? income = null,
      Category? category = null,
      int? accountId = null,
      int? cardId = null,
      DateTime? date = null,
      TransactionSourceKind? sourceKind = null,
      TransactionPlanningImpact? planningImpact = null)
    {
      Dictionary<string, string?> query = GetCurrentQuery();

      if (income.HasValue)
      {
        query["type"] = income.Value ? "income" : "expense";
      }

      if (category.HasValue)
      {
        query["category"] = category.Value.ToString();
        query.Remove("categoryId");
      }

      if (accountId.HasValue)
      {
        query["accountId"] = accountId.Value.ToString();
      }

      if (cardId.HasValue)
      {
        query["cardId"] = cardId.Value.ToString();
      }

      if (date.HasValue)
      {
        query["month"] = date.Value.Month.ToString();
        query["year"] = date.Value.Year.ToString();
        query["day"] = date.Value.Day.ToString();
      }

      if (sourceKind.HasValue)
      {
        query["sourceKind"] = sourceKind.Value.ToString();
      }

      if (planningImpact.HasValue)
      {
        query["planningImpact"] = planningImpact.Value.ToString();
      }

      return QueryHelpers.AddQueryString("transactions", query);
    }

    private Dictionary<string, string?> GetCurrentQuery()
    {
      Dictionary<string, string?> query = new Dictionary<string, string?>();

      if (selectedMonth.HasValue)
      {
        query["month"] = selectedMonth.Value.ToString();
      }

      if (selectedYear.HasValue)
      {
        query["year"] = selectedYear.Value.ToString();
      }

      if (selectedDay.HasValue)
      {
        query["day"] = selectedDay.Value.ToString();
      }

      if (!string.IsNullOrEmpty(selectedCategory))
      {
        query["category"] = selectedCategory;
      }

      if (selectedCategoryId.HasValue)
      {
        query["categoryId"] = selectedCategoryId.Value.ToString();
      }

      if (!string.IsNullOrEmpty(selectedType))
      {
        query["type"] = selectedType;
      }

      if (selectedAccountId.HasValue)
      {
        query["accountId"] = selectedAccountId.Value.ToString();
      }

      if (selectedCardId.HasValue)
      {
        query["cardId"] = selectedCardId.Value.ToString();
      }

      if (!string.IsNullOrEmpty(selectedSourceKind))
      {
        query["sourceKind"] = selectedSourceKind;
      }

      if (!string.IsNullOrEmpty(selectedPlanningImpact))
      {
        query["planningImpact"] = selectedPlanningImpact;
      }

      return query;
    }

    private bool HasActiveFilters()
    {
      return selectedDay.HasValue
        || !string.IsNullOrEmpty(selectedCategory)
        || selectedCategoryId.HasValue
        || !string.IsNullOrEmpty(selectedType)
        || selectedAccountId.HasValue
        || selectedCardId.HasValue
        || !string.IsNullOrEmpty(selectedSourceKind)
        || !string.IsNullOrEmpty(selectedPlanningImpact)
        || hasRouteFilters;
    }

    private async Task ClearFilter(string filter)
    {
      switch (filter)
      {
        case "day":
          selectedDay = null;
          break;
        case "category":
          selectedCategory = string.Empty;
          selectedCategoryId = null;
          break;
        case "type":
          selectedType = string.Empty;
          break;
        case "account":
          selectedAccountId = null;
          break;
        case "card":
          selectedCardId = null;
          break;
        case "source":
          selectedSourceKind = string.Empty;
          break;
        case "planning":
          selectedPlanningImpact = string.Empty;
          break;
      }

      hasRouteFilters = false;
      await LoadTransactions();
      NavigateToCurrentFilters(replace: true);
    }

    private async Task ClearAllFilters()
    {
      selectedDay = null;
      selectedCategory = string.Empty;
      selectedCategoryId = null;
      selectedType = string.Empty;
      selectedAccountId = null;
      selectedCardId = null;
      selectedSourceKind = string.Empty;
      selectedPlanningImpact = string.Empty;
      hasRouteFilters = false;
      await LoadTransactions();
      NavigateToCurrentFilters(replace: true);
    }

    private string GetAccountName(int? accountId)
    {
      if (!accountId.HasValue)
      {
        return Loc["No account"];
      }

      return accounts.FirstOrDefault(account => account.Id == accountId.Value)?.Name ?? Loc["Unknown account"];
    }

    private AccountEntity? GetAccount(int? accountId)
    {
      if (!accountId.HasValue)
      {
        return null;
      }

      return accounts.FirstOrDefault(account => account.Id == accountId.Value);
    }

    private static string GetAccountHref(int accountId)
    {
      return $"accounts?accountId={accountId}";
    }

    private string GetSelectedCategoryName()
    {
      if (!selectedCategoryId.HasValue)
      {
        return string.Empty;
      }

      TransactionEntity? transaction = transactions.FirstOrDefault(item =>
        item.CategoryId == selectedCategoryId.Value);
      if (transaction?.CategoryEntity != null)
      {
        return transaction.CategoryEntity.Name;
      }

      CategoryDisplayDto? category = categories.FirstOrDefault(item => item.Id == selectedCategoryId.Value);
      if (category != null)
      {
        return Loc[category.Name];
      }

      return $"{Loc["Category"]} #{selectedCategoryId.Value}";
    }

    private string GetTypeLabel(string value)
    {
      return string.Equals(value, "income", StringComparison.OrdinalIgnoreCase)
        ? Loc["Income"]
        : Loc["Expense"];
    }

    private static string GetDescriptionTitle(TransactionEntity transaction)
    {
      return string.IsNullOrWhiteSpace(transaction.SourceDetails)
        ? transaction.Description
        : $"{transaction.Description}\n{transaction.SourceDetails}";
    }

    private static string GetSourceKindLabel(TransactionSourceKind sourceKind)
    {
      return sourceKind switch
      {
        TransactionSourceKind.Legacy => "Legacy",
        TransactionSourceKind.Manual => "Manual",
        TransactionSourceKind.BankStatement => "Bank statement source",
        TransactionSourceKind.CardInvoice => "Card invoice source",
        TransactionSourceKind.Email => "Email source",
        TransactionSourceKind.Api => "API source",
        _ => sourceKind.ToString()
      };
    }

    private static string GetPlanningImpactLabel(TransactionPlanningImpact planningImpact)
    {
      return planningImpact switch
      {
        TransactionPlanningImpact.Unclassified => "Automatic or unclassified",
        TransactionPlanningImpact.Operating => "Ordinary income or expense",
        TransactionPlanningImpact.InternalTransfer => "Transfer between my accounts",
        TransactionPlanningImpact.Contribution => "Investment contribution",
        TransactionPlanningImpact.AssetMovement => "Asset purchase, sale or movement",
        TransactionPlanningImpact.Extraordinary => "Extraordinary event",
        TransactionPlanningImpact.Ignored => "Ignore in planning",
        _ => planningImpact.ToString()
      };
    }

    private static bool TryParseCategoryFilter(string value, out Category category)
    {
      if (Enum.TryParse(value, true, out category))
      {
        return true;
      }

      string normalized = value
        .Replace("_", string.Empty, StringComparison.Ordinal)
        .Replace("-", string.Empty, StringComparison.Ordinal)
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .ToUpperInvariant();

      category = normalized switch
      {
        "SALARY" => Category.Salary,
        "FREELANCE" or "OTHERINCOME" or "OTHERSINCOME" => Category.OthersIncome,
        "HOME" => Category.Home,
        "LIGHT" or "ELECTRICITY" => Category.Light,
        "FOOD" => Category.Food,
        "COMPANYCOSTS" or "COMPANYCOST" => Category.CompanyCosts,
        "INTERNET" => Category.Internet,
        "HEALTH" => Category.Health,
        "VEHICLES" or "TRANSPORT" => Category.Vehicles,
        "LEISURE" or "ENTERTAINMENT" => Category.Leisure,
        "EXTRAEXPENSES" => Category.ExtraExpenses,
        "SCHOOL" or "EDUCATION" => Category.School,
        "INVEST" or "INVESTMENT" or "INVESTIMENT" => Category.Investiment,
        _ => default
      };

      return category != default;
    }

    #endregion

    #region Modal Operations

    private void ShowCreateModal()
    {
      currentTransaction = new TransactionEntity
      {
        Date = DateTime.Today,
        Category = Category.OthersIncome,
        Income = false,
        SourceKind = TransactionSourceKind.Manual,
      };
      isDirty = false;
      isEditing = false;
      showModal = true;
    }

    private void ShowEditModal(TransactionEntity transaction)
    {
      currentTransaction = new TransactionEntity
      {
        Id = transaction.Id,
        Date = transaction.Date,
        Description = transaction.Description,
        Category = transaction.Category,
        Value = transaction.Value,
        Income = transaction.Income,
        AccountId = transaction.AccountId,
        SourceKind = transaction.SourceKind,
        TransactionTime = transaction.TransactionTime,
        SourceDetails = transaction.SourceDetails,
        PlanningImpact = transaction.PlanningImpact,
        PlanningClassificationSource = transaction.PlanningClassificationSource,
        PlanningClassificationConfirmed = transaction.PlanningClassificationConfirmed,
      };
      isDirty = false;
      isEditing = true;
      showModal = true;
    }

    private void HideModal()
    {
      showModal = false;
      showConfirmDiscard = false;
      isDirty = false;
      currentTransaction = new TransactionEntity();
    }

    private void MarkDirty()
    {
      isDirty = true;
    }

    private void RequestCloseModal()
    {
      if (isDirty)
      {
        showConfirmDiscard = true;
      }
      else
      {
        HideModal();
      }
    }

    private void OnBackdropClick()
    {
      RequestCloseModal();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
      if (string.Compare(e.Key, "Escape", StringComparison.Ordinal) == 0)
      {
        RequestCloseModal();
      }
    }

    private void CancelDiscard()
    {
      showConfirmDiscard = false;
    }

    private void ConfirmDiscard()
    {
      HideModal();
    }

    #endregion

    #region CRUD OPERATIONS

    private async Task SaveTransaction(CancellationToken cancellationToken = default)
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      isSaving = true;
      StateHasChanged();
      await Task.Delay(1);
      try
      {
        if (isEditing)
        {
          TransactionEntity? existing = await transactionRepository.SelectAsync(currentTransaction.Id);
          if (existing != null)
          {
            int? previousAccountId = existing.AccountId;
            existing.Date = currentTransaction.Date;
            existing.Description = currentTransaction.Description;
            existing.Category = currentTransaction.Category;
            existing.Value = currentTransaction.Value;
            existing.Income = currentTransaction.Income;
            existing.AccountId = currentTransaction.AccountId;
            existing.PlanningImpact = currentTransaction.PlanningImpact;
            existing.PlanningClassificationConfirmed = currentTransaction.PlanningImpact != TransactionPlanningImpact.Unclassified;
            existing.PlanningClassificationSource = existing.PlanningClassificationConfirmed
              ? PlanningClassificationSource.Manual
              : PlanningClassificationSource.None;
            existing.ExternalId = BankStatementCsvParser.GenerateManualExternalId(existing);

            await transactionRepository.UpdateAsync(existing);

            if (previousAccountId != currentTransaction.AccountId)
            {
              await financialSyncService.SyncAccountAsync(previousAccountId, cancellationToken);
            }
          }
        }
        else
        {
          currentTransaction.ExternalId = BankStatementCsvParser.GenerateManualExternalId(currentTransaction);
          currentTransaction.SourceKind = TransactionSourceKind.Manual;
          currentTransaction.PlanningClassificationConfirmed = currentTransaction.PlanningImpact != TransactionPlanningImpact.Unclassified;
          currentTransaction.PlanningClassificationSource = currentTransaction.PlanningClassificationConfirmed
            ? PlanningClassificationSource.Manual
            : PlanningClassificationSource.None;
          await transactionRepository.InsertAsync(currentTransaction);
        }

        await financialSyncService.SyncAccountAsync(currentTransaction.AccountId, cancellationToken);
        await LoadTransactions(cancellationToken);
        HideModal();

        // Messages fixed to English (UI term: Transactions)
        ShowMessage(isEditing ? "Transaction updated successfully!" : "Transaction created successfully!", false);
      }
      catch (Exception ex)
      {
        ShowMessage($"Error saving: {ex.Message}", true);
      }
      finally
      {
        isSaving = false;
      }
    }

    // parameterless wrapper used by EditForm OnValidSubmit and bindings
    private Task SaveTransaction()
    {
      return SaveTransaction(CancellationToken.None);
    }

    private void ConfirmDelete(TransactionEntity transaction)
    {
      transactionToDelete = transaction;
      showDeleteConfirm = true;
    }

    private async Task DeleteTransaction(CancellationToken cancellationToken = default)
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      if (transactionToDelete == null)
      {
        return;
      }

      isSaving = true;
      StateHasChanged();
      await Task.Delay(1);
      try
      {
        TransactionEntity? apt = await transactionRepository.SelectOneAsync(a => a.Id == transactionToDelete.Id, cancellationToken);
        if (apt != null)
        {
          await transactionRepository.DeleteAsync(apt.Id);
          await financialSyncService.SyncAccountAsync(apt.AccountId, cancellationToken);
          await LoadTransactions(cancellationToken);
          ShowMessage("Transaction deleted successfully!", false);
        }
      }
      catch (Exception ex)
      {
        ShowMessage($"Error deleting: {ex.Message}", true);
      }
      finally
      {
        isSaving = false;
        showDeleteConfirm = false;
        transactionToDelete = null;
      }
    }

    #endregion

    #region UI HELPERS

    private void ShowMessage(string msg, bool error)
    {
      message = msg;
      isError = error;

      _ = ClearMessageAfterDelayAsync(CancellationToken.None);
    }

    private async Task<bool> EnsureWriteAllowedAsync()
    {
      AuthenticationState state = await AuthStateProvider.GetAuthenticationStateAsync();
      if (state.User.Identity?.IsAuthenticated == true)
      {
        return true;
      }

      ShowMessage(Loc[DemoReadOnlyException.MessageKey], true);
      return false;
    }

    private async Task ClearMessageAfterDelayAsync(CancellationToken cancellationToken = default)
    {
      await Task.Delay(3000, cancellationToken);
      message = string.Empty;
      await InvokeAsync(() => StateHasChanged());
    }

    #endregion

    #region IMPORT METHODS

    private async Task OpenImportFlow()
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      if (importFlow != null)
      {
        await importFlow.OpenAsync(CancellationToken.None);
      }
    }

    private async Task OnImportDataChanged()
    {
      await LoadTransactions(CancellationToken.None);
      ShowMessage("Import completed successfully!", false);
    }

    #endregion

    #region CATEGORY METHODS

    private async Task OpenCategoriesModal()
    {
      if (categoryModal != null)
      {
        await categoryModal.ShowAsync();
      }
    }

    private void OnCategoriesChanged()
    {
      _ = RefreshCategoriesAsync();
      StateHasChanged();
    }

    private async Task RefreshCategoriesAsync()
    {
      categories = await CategoryService.GetAllAsync(CancellationToken.None);
      await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region SORTING METHODS

    private void ToggleSort(string column)
    {
      if (sortColumn == column)
      {
        sortAscending = !sortAscending;
      }
      else
      {
        sortColumn = column;
        sortAscending = true;
      }
    }

    private MarkupString GetSortIndicator(string column)
    {
      if (sortColumn != column)
      {
        return new MarkupString(string.Empty);
      }

      string icon = sortAscending ? "bi-sort-up" : "bi-sort-down";
      return new MarkupString($"<i class=\"bi {icon} ms-1\"></i>");
    }

    /// <summary>
    /// Returns Transaction sorted by current sort settings.
    /// </summary>
    private IEnumerable<TransactionEntity> GetSortedTransactions()
    {
      IEnumerable<TransactionEntity> sorted = sortColumn switch
      {
        "Date" => sortAscending
              ? transactions.OrderBy(a => a.Date)
              : transactions.OrderByDescending(a => a.Date),
        "Category" => sortAscending
                ? transactions.OrderBy(a => a.Category)
                : transactions.OrderByDescending(a => a.Category),
        "Type" => sortAscending
              ? transactions.OrderBy(a => a.Income)
              : transactions.OrderByDescending(a => a.Income),
        "Amount" => sortAscending
                ? transactions.OrderBy(a => a.Value)
                : transactions.OrderByDescending(a => a.Value),
        _ => transactions
      };

      return sorted;
    }

    #endregion

    #region SELECTION METHODS

    private void ToggleSelection(int id)
    {
      if (selectedIds.Contains(id))
      {
        selectedIds.Remove(id);
      }
      else
      {
        selectedIds.Add(id);
      }
    }

    private void ToggleSelectAll()
    {
      if (IsAllSelected)
      {
        selectedIds.Clear();
      }
      else
      {
        selectedIds = transactions.Select(a => a.Id).ToHashSet();
      }
    }

    private void ConfirmDeleteSelected()
    {
      if (selectedIds.Count > 0)
      {
        showBulkDeleteConfirm = true;
      }
    }

    private async Task DeleteSelectedTransactions()
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      if (selectedIds.Count == 0)
      {
        return;
      }

      isSaving = true;
      StateHasChanged();
      await Task.Delay(1);
      try
      {
        List<int> idsToDelete = selectedIds.ToList();

        // Load current Transaction via repository and filter
        List<TransactionEntity>? all = await transactionRepository.SelectAsync(CancellationToken.None);
        List<TransactionEntity> toDelete = (all ?? new List<TransactionEntity>()).Where(a => idsToDelete.Contains(a.Id) && !a.Deleted).ToList();

        int deletedCount = 0;
        foreach (TransactionEntity apt in toDelete)
        {
          await transactionRepository.DeleteAsync(apt.Id);
          deletedCount++;
        }
        selectedIds.Clear();
        showBulkDeleteConfirm = false;
        await LoadTransactions(CancellationToken.None);
        ShowMessage($"Deleted {deletedCount} transactions successfully!", false);
      }
      catch (Exception ex)
      {
        ShowMessage($"Error deleting Transactions: {ex.Message}", true);
      }
      finally
      {
        isSaving = false;
      }
    }

    #endregion

    #region ICON HELPERS

    private static string GetDescriptionIconCss(TransactionEntity item)
    {
      string desc = (item.Description ?? string.Empty).ToUpperInvariant();

      if (desc.Contains("PIX"))
      {
        return "bi bi-lightning-charge";
      }

      if (desc.Contains("TRANSFER") || desc.Contains("TED") || desc.Contains("DOC"))
      {
        return "bi bi-arrow-left-right";
      }

      if (desc.Contains("CARTAO") || desc.Contains("CARD") || desc.Contains("PURCHASE"))
      {
        return "bi bi-credit-card";
      }

      if (desc.Contains("DEBIT"))
      {
        return "bi bi-credit-card-2-front";
      }

      if (desc.Contains("ESTORNO") || desc.Contains("REFUND"))
      {
        return "bi bi-arrow-counterclockwise";
      }

      if (item.Income)
      {
        return "bi bi-arrow-up-circle";
      }

      return "bi bi-arrow-down-circle";
    }

    private static string GetCategoryIconCss(Category category)
    {
      return category switch
      {
        Category.Food => "bi bi-egg-fried",
        Category.Vehicles => "bi bi-car-front",
        Category.CompanyCosts => "bi bi-briefcase",
        Category.Health => "bi bi-heart-pulse",
        Category.Internet => "bi bi-wifi",
        Category.Light => "bi bi-lightbulb",
        Category.Leisure => "bi bi-controller",
        Category.Home => "bi bi-house",
        Category.ExtraExpenses => "bi bi-tags",
        Category.OthersIncome => "bi bi-cash-coin",
        Category.Salary => "bi bi-wallet2",
        Category.School => "bi bi-mortarboard",
        _ => "bi bi-grid"
      };
    }

    #endregion
  }
}
