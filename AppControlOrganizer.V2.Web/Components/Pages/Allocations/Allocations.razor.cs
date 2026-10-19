//-----------------------------------------------------------------------
// <copyright file="Allocations.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Allocations
{
  using System.Globalization;
  using AppControlOrganizer.V2.Shared.Entities;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Shared.Models;
  using AppControlOrganizer.V2.Web.Components.Shared.Common;
  using AppControlOrganizer.V2.Web.Data;
  using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
  using AppControlOrganizer.V2.Web.Helpers;
  using AppControlOrganizer.V2.Web.Services;
  using AppControlOrganizer.V2.Web.Services.Financial;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.AspNetCore.WebUtilities;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using System.Security.Claims;

  /// <summary>
  /// Code-behind for Allocations.razor (Investments page). Manages financial allocations/investments with CRUD
  /// operations and multiple portfolio views.
  /// </summary>
  public partial class Allocations : ComponentBase, IDisposable
  {
    /// <summary>
    /// Gets or sets the panel requested by contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "panel")]
    public string? RequestedPanel { get; set; }

    /// <summary>
    /// Gets or sets the requested view mode from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "view")]
    public string? RequestedView { get; set; }

    /// <summary>
    /// Gets or sets the requested account filter from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "accountId")]
    public int? RequestedAccountId { get; set; }

    /// <summary>
    /// Gets or sets the requested allocation type filter from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "type")]
    public string? RequestedType { get; set; }

    /// <summary>
    /// Gets or sets the requested currency filter from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "currency")]
    public string? RequestedCurrency { get; set; }

    /// <summary>
    /// Gets or sets the requested search text from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "search")]
    public string? RequestedSearch { get; set; }

    /// <summary>
    /// Gets or sets the requested quote focus from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "quote")]
    public string? RequestedQuote { get; set; }

    /// <summary>
    /// Gets or sets the requested expiration filter from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "expires")]
    public string? RequestedExpires { get; set; }

    /// <summary>
    /// Gets or sets the requested allocation to open from contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "allocationId")]
    public int? RequestedAllocationId { get; set; }

    [Inject]
    private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;

    /// <summary>
    /// Flag to show/hide filter section.
    /// </summary>
    private bool showFilters = false;

    /// <summary>
    /// Indicates whether quotes are displayed.
    /// </summary>
    private bool showQuotes = false;

    /// <summary>
    /// Indicates whether the fixed-income audit panel is displayed.
    /// </summary>
    private bool showFixedIncomeAudit = false;

    /// <summary>
    /// View modes available for the allocations page.
    /// </summary>
    internal enum AllocationViewMode
    {
      /// <summary>List view of allocations.</summary>
      List = 1,
      /// <summary>Grouped view by type/account.</summary>
      Grouped = 2
    }

        /// <summary>
    /// Injects the account repository used to load account lists and resolve account details.
    /// </summary>
    [Inject]
    public IAccountRepository accountRepository { get; set; } = null!;

    /// <summary>
    /// Injects the allocation repository for CRUD operations on allocations.
    /// </summary>
    [Inject]
    public IAllocationRepository allocationRepository { get; set; } = null!;

    /// <summary>
    /// Injects the market quote cache to obtain latest market quotes used for pricing.
    /// </summary>
    [Inject]
    public MarketQuoteCache marketQuoteCache { get; set; } = null!;

    /// <summary>
    /// Injects the pricing service responsible for allocation price calculations and conversions.
    /// </summary>
    [Inject]
    public AllocationQuotePricingService pricingService { get; set; } = null!;

    /// <summary>
    /// Injects the database context factory used for allocation history writes.
    /// </summary>
    [Inject]
    public IDbContextFactory<LifePlannerContext> ContextFactory { get; set; } = null!;

    /// <summary>
    /// Injects the authentication state provider used to stamp owner-scoped history.
    /// </summary>
    [Inject]
    public AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    /// <summary>
    /// Injects the fixed-income audit service used to diagnose metadata divergences.
    /// </summary>
    [Inject]
    public FixedIncomeAuditService fixedIncomeAuditService { get; set; } = null!;

    /// <summary>
    /// Injects the HTTP client factory used by fixed-income BACEN calculations.
    /// </summary>
    [Inject]
    public IHttpClientFactory httpClientFactory { get; set; } = null!;

    /// <summary>
    /// Injects the logger for non-fatal allocation calculations.
    /// </summary>
    [Inject]
    public ILogger<Allocations> logger { get; set; } = null!;

    /// <summary>
    /// Financial service.
    /// </summary>
    [Inject]
    public FinancialSyncService financialSyncService { get; set;} = null!;

    /// <summary>
    /// Injects the scoped state container that persists allocation data across page navigations
    /// within the same Blazor circuit, eliminating redundant DB round-trips on re-visits.
    /// </summary>
    [Inject]
    public AllocationsStateContainer allocationsState { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// Cached latest successful market quotes by allocation type.
    /// </summary>
    private IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> latestQuotes = new Dictionary<QuoteKey, MarketQuoteSnapshot>();

    /// <summary>
    /// True if USD/BRL quote is available.
    /// </summary>
    private bool hasUsdBrlQuote;

    /// <summary>
    /// The USD/BRL quote snapshot if available.
    /// </summary>
    private MarketQuoteSnapshot? usdBrlQuote;

        // -----------------------------------------------------------------------------
    // Dropdown items
    // -----------------------------------------------------------------------------

    private IEnumerable<DropdownItem<int>> FilterAccountItems =>
      new[] { new DropdownItem<int>(0, "All Accounts") }
        .Concat(accounts.Select(a => new DropdownItem<int>(a.Id, a.Name)));

    private static IEnumerable<DropdownItem<string>> FilterAllocationTypeItems =>
      new[] { new DropdownItem<string>(string.Empty, "All Types") }
        .Concat(Enum.GetValues<AllocationType>()
              .Select(t => new DropdownItem<string>(t.ToString(), t.GetDisplayName())));

    private static IEnumerable<DropdownItem<string>> FilterCurrencyItems =>
      new[] { new DropdownItem<string>(string.Empty, "All Currencies") }
        .Concat(Enum.GetValues<Currency>()
              .Select(c => new DropdownItem<string>(c.ToString(), c.GetDisplayName())));

    private IEnumerable<DropdownItem<string>> FilterExpirationItems =>
      new[]
      {
        new DropdownItem<string>(string.Empty, Loc["All Maturities"]),
        new DropdownItem<string>("upcoming", Loc["Upcoming maturities"])
      };

    // -----------------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------------

    private List<AllocationEntity> allocations = new List<AllocationEntity>();
    private List<AllocationEntity> allocationsBase = new List<AllocationEntity>();
    private List<AccountEntity> accounts = new List<AccountEntity>();

    private AllocationEntity currentAllocation = new AllocationEntity();
    private AllocationEntity? allocationToDelete;

    private int selectedAccountId = 0;
    private string selectedType = string.Empty;
    private string selectedCurrency = string.Empty;
    private string selectedExpiration = string.Empty;

    private decimal totalInvested;
    private decimal totalCurrent;
    private decimal profit;
    private decimal profitPercentage;

    private bool hasMultipleCurrencies;
    private bool hasUnconvertedCurrencies;
    private decimal totalCurrentApproxBrl;

    private bool isInitialAllocationsLoading = true;
    private bool isLoadingSummary = false;
    private bool isLoadingList = false;
    private bool isAuditingFixedIncome = false;

    private List<FixedIncomeAuditItem> fixedIncomeAuditItems = new List<FixedIncomeAuditItem>();
    private string fixedIncomeAuditError = string.Empty;

    /// <summary>
    /// Cache of BRL current values per allocation ID, computed in RecalculatePortfolio.
    /// </summary>
    private readonly Dictionary<int, decimal> currentBrlByAllocationId = new Dictionary<int, decimal>();

    private bool showModal = false;
    private bool showDeleteConfirm = false;
    private bool showConfirmDiscard = false;

    private bool isEditing = false;
    private bool isSaving = false;
    private bool isDirty = false;

    private string message = string.Empty;
    private bool isError = false;

    private AllocationViewMode selectedViewMode = AllocationViewMode.List;

    private string quickSearch = string.Empty;
    private string? focusedQuote;
    private string? lastAppliedNavigationUri;
    private int? lastOpenedAllocationId;

    // -----------------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------------

    private bool initialized = false;

    /// <summary>
    /// Lifecycle hook executed after render. Loads accounts and allocations on first render.
    /// Uses the in-memory state container for an instant render when the data is still fresh,
    /// then silently refreshes in the background to keep the snapshot current.
    /// </summary>
    /// <inheritdoc />
    public void Dispose() => ValuesVis.OnChange -= OnValuesVisibilityChanged;

    private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
      ValuesVis.OnChange += OnValuesVisibilityChanged;
      ApplyNavigationState();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
      string currentUri = Navigation.Uri;
      if (string.Equals(lastAppliedNavigationUri, currentUri, StringComparison.Ordinal))
      {
        return;
      }

      lastAppliedNavigationUri = currentUri;
      ApplyNavigationState();

      if (initialized)
      {
        await LoadAllocations(CancellationToken.None, onlyRefreshSections: true);
        OpenRequestedAllocationIfAvailable();
      }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (!initialized)
      {
        initialized = true;

        if (allocationsState.HasFreshData)
        {
          // --- Cache hit: restore snapshot and render immediately, no spinner ---
          accounts = allocationsState.Accounts!;
          List<AllocationEntity> cachedAllocations = allocationsState.Allocations!.ToList();
          allocationsBase = ApplyCurrentFilters(cachedAllocations).ToList();
          allocations = allocationsBase.ToList();
          latestQuotes = allocationsState.LatestQuotes!;
          isInitialAllocationsLoading = false;

          if (!string.IsNullOrWhiteSpace(quickSearch))
          {
            ApplyQuickSearch();
          }
          else
          {
            RecalculatePortfolio();
          }

          OpenRequestedAllocationIfAvailable();
          await InvokeAsync(StateHasChanged);

          // Silently refresh in background so data stays current.
          _ = BackgroundRefreshAllocationsAsync();
        }
        else
        {
          // --- Cache miss: full load ---
          List<AccountEntity>? acctResult = await accountRepository.SelectAsync(CancellationToken.None);
          accounts = acctResult != null ? acctResult.OrderBy(a => a.Name).ToList() : new List<AccountEntity>();

          await LoadAllocations(CancellationToken.None, onlyRefreshSections: false);
          await InvokeAsync(StateHasChanged);
        }
      }
    }

    /// <summary>
    /// Refreshes allocations and accounts from the database in the background after a cache-hit render.
    /// Updates the state container so subsequent navigations also get a fresh snapshot.
    /// ObjectDisposedException is swallowed in case the user navigates away before completion.
    /// </summary>
    private async Task BackgroundRefreshAllocationsAsync()
    {
      try
      {
        List<AccountEntity>? acctResult = await accountRepository.SelectAsync(CancellationToken.None);
        accounts = acctResult != null ? acctResult.OrderBy(a => a.Name).ToList() : new List<AccountEntity>();

        await LoadAllocations(CancellationToken.None, onlyRefreshSections: false);
        await InvokeAsync(StateHasChanged);
      }
      catch (ObjectDisposedException)
      {
        // Component was disposed (user navigated away) before the refresh completed — safe to ignore.
      }
    }

    private Task LoadAllocations()
    {
      isLoadingSummary = true;
      isLoadingList = true;
      _ = InvokeAsync(StateHasChanged);
      return LoadAllocations(CancellationToken.None, onlyRefreshSections: true);
    }

    // -----------------------------------------------------------------------------
    // View Helpers
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Sets the selected view mode.
    /// </summary>
    private void SetViewMode(AllocationViewMode mode)
    {
      selectedViewMode = mode;
      Navigation.NavigateTo(GetAllocationsHref(view: mode));
    }

    private void ApplyNavigationState()
    {
      showQuotes = string.Equals(RequestedPanel, "quotes", StringComparison.OrdinalIgnoreCase);
      focusedQuote = RequestedQuote;

      if (string.Equals(RequestedView, "dashboard", StringComparison.OrdinalIgnoreCase))
      {
        selectedViewMode = AllocationViewMode.List;
      }
      else if (Enum.TryParse<AllocationViewMode>(RequestedView, true, out AllocationViewMode requestedMode))
      {
        selectedViewMode = requestedMode;
      }

      selectedAccountId = RequestedAccountId.GetValueOrDefault() > 0 ? RequestedAccountId.GetValueOrDefault() : 0;
      selectedType = Enum.TryParse<AllocationType>(RequestedType, true, out AllocationType requestedType)
        ? requestedType.ToString()
        : string.Empty;
      selectedCurrency = Enum.TryParse<Currency>(RequestedCurrency, true, out Currency requestedCurrency)
        ? requestedCurrency.ToString()
        : string.Empty;
      selectedExpiration = string.Equals(RequestedExpires, "upcoming", StringComparison.OrdinalIgnoreCase)
        ? "upcoming"
        : string.Empty;
      quickSearch = RequestedSearch ?? string.Empty;
    }

    private Task ApplyFiltersAndNavigate()
    {
      fixedIncomeAuditItems.Clear();
      Navigation.NavigateTo(GetAllocationsHref());
      return LoadAllocations();
    }

    private string GetAllocationsHref(
      AllocationViewMode? view = null,
      int? accountId = null,
      AllocationType? type = null,
      Currency? currency = null,
      string? search = null,
      string? panel = null,
      string? quote = null,
      string? expires = null,
      int? allocationId = null,
      bool clearFilters = false)
    {
      Dictionary<string, string?> query = new Dictionary<string, string?>();

      AllocationViewMode effectiveView = view ?? selectedViewMode;
      if (effectiveView != AllocationViewMode.List)
      {
        query["view"] = effectiveView.ToString().ToLowerInvariant();
      }

      int effectiveAccountId = clearFilters ? 0 : accountId ?? selectedAccountId;
      if (effectiveAccountId > 0)
      {
        query["accountId"] = effectiveAccountId.ToString(CultureInfo.InvariantCulture);
      }

      string effectiveType = clearFilters ? string.Empty : type?.ToString() ?? selectedType;
      if (!string.IsNullOrWhiteSpace(effectiveType))
      {
        query["type"] = effectiveType;
      }

      string effectiveCurrency = clearFilters ? string.Empty : currency?.ToString() ?? selectedCurrency;
      if (!string.IsNullOrWhiteSpace(effectiveCurrency))
      {
        query["currency"] = effectiveCurrency;
      }

      string effectiveSearch = clearFilters ? string.Empty : search ?? quickSearch;
      if (!string.IsNullOrWhiteSpace(effectiveSearch))
      {
        query["search"] = effectiveSearch;
      }

      string effectiveExpires = clearFilters ? string.Empty : expires ?? selectedExpiration;
      if (!string.IsNullOrWhiteSpace(effectiveExpires))
      {
        query["expires"] = effectiveExpires;
      }

      string effectivePanel = panel ?? (showQuotes ? "quotes" : string.Empty);
      if (!string.IsNullOrWhiteSpace(effectivePanel))
      {
        query["panel"] = effectivePanel;
      }

      string effectiveQuote = quote ?? focusedQuote ?? string.Empty;
      if (!string.IsNullOrWhiteSpace(effectiveQuote))
      {
        query["quote"] = effectiveQuote;
      }

      if (allocationId.HasValue && allocationId.Value > 0)
      {
        query["allocationId"] = allocationId.Value.ToString(CultureInfo.InvariantCulture);
      }

      return QueryHelpers.AddQueryString("allocations", query);
    }

    private void ToggleQuotes()
    {
      showQuotes = !showQuotes;
      Navigation.NavigateTo(GetAllocationsHref(panel: showQuotes ? "quotes" : string.Empty, quote: showQuotes ? focusedQuote : string.Empty));
    }

    private async Task ToggleFixedIncomeAudit()
    {
      showFixedIncomeAudit = !showFixedIncomeAudit;

      if (showFixedIncomeAudit && fixedIncomeAuditItems.Count == 0)
      {
        await RunFixedIncomeAudit();
      }
    }

    private async Task RunFixedIncomeAudit()
    {
      fixedIncomeAuditError = string.Empty;
      isAuditingFixedIncome = true;
      await InvokeAsync(StateHasChanged);

      try
      {
        IReadOnlyList<FixedIncomeAuditItem> audit = await fixedIncomeAuditService.AuditAsync(
          allocations,
          CancellationToken.None);

        fixedIncomeAuditItems = audit.ToList();
      }
      catch
      {
        fixedIncomeAuditError = Loc["Could not run fixed-income audit now."];
      }
      finally
      {
        isAuditingFixedIncome = false;
        await InvokeAsync(StateHasChanged);
      }
    }

    private void ClearAllocationFilters()
    {
      selectedAccountId = 0;
      selectedType = string.Empty;
      selectedCurrency = string.Empty;
      selectedExpiration = string.Empty;
      quickSearch = string.Empty;
      fixedIncomeAuditItems.Clear();
      Navigation.NavigateTo(GetAllocationsHref(clearFilters: true));
      _ = LoadAllocations();
    }

    private void OpenAllocationById(int allocationId)
    {
      AllocationEntity? allocation = allocationsBase.FirstOrDefault(a => a.Id == allocationId)
        ?? allocations.FirstOrDefault(a => a.Id == allocationId);
      if (allocation != null)
      {
        ShowEditModal(allocation);
      }
    }

    private void OpenRequestedAllocationIfAvailable()
    {
      if (!RequestedAllocationId.HasValue || RequestedAllocationId.Value <= 0)
      {
        return;
      }

      if (lastOpenedAllocationId == RequestedAllocationId.Value && showModal)
      {
        return;
      }

      OpenAllocationById(RequestedAllocationId.Value);
      lastOpenedAllocationId = RequestedAllocationId.Value;
    }

    private static decimal GetInvested(AllocationEntity a)
    {
      return FinancialPositionCalculator.CalculateAppliedNativeValue(a);
    }

    /// <summary>
    /// Calculates current value using market quotes when available, falling back to CurrentUnitValue.
    /// </summary>
    private decimal GetCurrent(AllocationEntity a)
    {
      return FinancialPositionCalculator.CalculateAllocationPosition(
        a,
        latestQuotes,
        pricingService,
        FinancialFactsService.ResolvePurpose).CurrentNativeAmount;
    }

    internal static string FormatUsd(decimal v) => $"${v:N2}";

    internal static string FormatMoneySafe(Currency currency, decimal amount)
    {
      return currency switch
      {
        Currency.BRL => Helper.FormatBrl(amount),
        Currency.USD => FormatUsd(amount),
        _ => $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}"
      };
    }

    private static string FormatSignedMoney(Currency currency, decimal amount)
    {
      string sign = amount > 0m ? "+" : string.Empty;
      return $"{sign}{FormatMoneySafe(currency, amount)}";
    }

    private static string FormatSignedPercent(decimal amount)
    {
      string sign = amount > 0m ? "+" : string.Empty;
      return $"{sign}{amount:N2}%";
    }

    private static string GetAuditBadgeCss(FixedIncomeAuditItem item)
    {
      if (!item.CanCalculate)
      {
        return "badge bg-secondary";
      }

      return item.HasDivergence ? "badge bg-danger" : "badge bg-success";
    }

    private static string GetAuditDifferenceCss(FixedIncomeAuditItem item)
    {
      if (!item.Difference.HasValue)
      {
        return string.Empty;
      }

      return item.HasDivergence ? "text-danger" : "text-success";
    }

    private static string GetAuditStatusKey(FixedIncomeAuditItem item)
    {
      if (!item.CanCalculate)
      {
        return "Incomplete";
      }

      return item.HasDivergence ? "Review" : "OK";
    }

    private void ApplyQuickSearch()
    {
      if (string.IsNullOrWhiteSpace(quickSearch))
      {
        allocations = allocationsBase.ToList();
        RecalculatePortfolio();
        return;
      }

      string q = quickSearch.Trim();

      allocations = allocationsBase
        .Where(a =>
          (!string.IsNullOrWhiteSpace(a.Name) && a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
          || (!string.IsNullOrWhiteSpace(a.Initials) && a.Initials.Contains(q, StringComparison.OrdinalIgnoreCase))
          || (!string.IsNullOrWhiteSpace(a.Annotations) && a.Annotations.Contains(q, StringComparison.OrdinalIgnoreCase)))
        .ToList();

      RecalculatePortfolio();
    }

    private IEnumerable<AllocationEntity> ApplyCurrentFilters(IEnumerable<AllocationEntity> source)
    {
      IEnumerable<AllocationEntity> query = source;

      if (selectedAccountId > 0)
      {
        query = query.Where(a => a.AccountId == selectedAccountId);
      }

      if (!string.IsNullOrEmpty(selectedType)
        && Enum.TryParse<AllocationType>(selectedType, out AllocationType type))
      {
        query = query.Where(a => a.AllocationType == type);
      }

      if (!string.IsNullOrEmpty(selectedCurrency)
        && Enum.TryParse<Currency>(selectedCurrency, out Currency currency))
      {
        query = query.Where(a => a.Currency == currency);
      }

      if (string.Equals(selectedExpiration, "upcoming", StringComparison.OrdinalIgnoreCase))
      {
        DateTime today = DateTime.Today;
        DateTime limit = today.AddDays(90);
        query = query.Where(a => a.ExpirationDate.HasValue && a.ExpirationDate.Value.Date >= today && a.ExpirationDate.Value.Date <= limit);
      }

      return query;
    }

    // -----------------------------------------------------------------------------
    // Derived view models (grouped/dashboard)
    // -----------------------------------------------------------------------------

    internal IReadOnlyList<CurrencySummaryVm> CurrencySummaries =>
      allocations
        .GroupBy(a => a.Currency)
        .Select(g => new CurrencySummaryVm(g.Key, g.Sum(a => GetCurrentBrlCached(a))))
        .OrderByDescending(x => x.CurrentTotal)
        .ToList();

    internal IReadOnlyList<TypeSummaryVm> TypeSummaries
    {
      get
      {
        decimal totalBrl = allocations.Sum(a => GetCurrentBrlCached(a));
        if (totalBrl <= 0m)
        {
          totalBrl = 1m;
        }

        return allocations
          .GroupBy(a => a.AllocationType)
          .Select(g =>
          {
            bool mixed = g.Select(x => x.Currency).Distinct().Count() > 1;
            decimal currentTotalBrl = g.Sum(a => GetCurrentBrlCached(a));

            return new TypeSummaryVm(
              Type: g.Key,
              TypeName: g.Key.GetDisplayName(),
              Currency: Currency.BRL,
              CurrentTotal: currentTotalBrl,
              Count: g.Count(),
              Weight: (currentTotalBrl / totalBrl) * 100m,
              HasMixedCurrencies: mixed);
          })
          .OrderByDescending(x => x.CurrentTotal)
          .ToList();
      }
    }

    internal IReadOnlyList<AccountSummaryVm> AccountSummaries
    {
      get
      {
        decimal totalBrl = allocations.Sum(a => GetCurrentBrlCached(a));
        if (totalBrl <= 0m)
        {
          totalBrl = 1m;
        }

        return allocations
          .GroupBy(a => a.AccountId)
          .Select(g =>
          {
            AccountEntity? acc = accounts.FirstOrDefault(x => x.Id == g.Key);
            bool mixed = g.Select(x => x.Currency).Distinct().Count() > 1;
            decimal currentTotalBrl = g.Sum(a => GetCurrentBrlCached(a));

            return new AccountSummaryVm(
              AccountId: g.Key,
              AccountName: acc?.Name ?? "Unknown",
              AccountLogo: acc?.Logo,
              Currency: Currency.BRL,
              CurrentTotal: currentTotalBrl,
              Count: g.Count(),
              Weight: (currentTotalBrl / totalBrl) * 100m,
              HasMixedCurrencies: mixed);
          })
          .OrderByDescending(x => x.CurrentTotal)
          .ToList();
      }
    }

    internal IReadOnlyList<GroupedTypeVm> GroupedByType =>
      allocations
        .GroupBy(a => a.AllocationType)
        .Select(g =>
        {
          bool mixed = g.Select(x => x.Currency).Distinct().Count() > 1;
          decimal typeTotalBrl = g.Sum(a => GetCurrentBrlCached(a));

          List<GroupedAccountVm> byAccount = g
            .GroupBy(x => x.AccountId)
            .Select(ag =>
            {
              AccountEntity? acc = accounts.FirstOrDefault(a => a.Id == ag.Key);
              bool accMixed = ag.Select(x => x.Currency).Distinct().Count() > 1;
              decimal accTotalBrl = ag.Sum(a => GetCurrentBrlCached(a));

              return new GroupedAccountVm(
                AccountId: ag.Key,
                AccountName: acc?.Name ?? "Unknown",
                AccountLogo: acc?.Logo,
                Currency: Currency.BRL,
                CurrentTotal: accTotalBrl,
                Count: ag.Count(),
                HasMixedCurrencies: accMixed,
                Items: ag.OrderBy(x => x.Name).ToList());
            })
            .OrderByDescending(x => x.CurrentTotal)
            .ToList();

          return new GroupedTypeVm(
            Type: g.Key,
            TypeName: g.Key.GetDisplayName(),
            Currency: Currency.BRL,
            CurrentTotal: typeTotalBrl,
            Count: g.Count(),
            HasMixedCurrencies: mixed,
            ByAccount: byAccount);
        })
        .OrderByDescending(x => x.CurrentTotal)
        .ToList();

    internal IReadOnlyList<TopPositionVm> TopPositions =>
      allocations
        .Select(a =>
        {
          AccountEntity? acc = accounts.FirstOrDefault(x => x.Id == a.AccountId);
          return new TopPositionVm(
            AllocationId: a.Id,
            Name: a.Name,
            Initials: a.Initials,
            AccountId: a.AccountId,
            AccountName: acc?.Name ?? "Unknown",
            Type: a.AllocationType,
            TypeName: a.AllocationType.GetDisplayName(),
            Currency: Currency.BRL,
            Current: GetCurrentBrlCached(a));
        })
        .OrderByDescending(x => x.Current)
        .Take(6)
        .ToList();

    internal IReadOnlyList<UpcomingExpirationVm> UpcomingExpirations =>
      allocations
        .Where(a => a.ExpirationDate.HasValue)
        .OrderBy(a => a.ExpirationDate)
        .Take(6)
        .Select(a =>
        {
          AccountEntity? acc = accounts.FirstOrDefault(x => x.Id == a.AccountId);
          return new UpcomingExpirationVm(
            AllocationId: a.Id,
            Name: a.Name,
            Initials: a.Initials,
            AccountId: a.AccountId,
            AccountName: acc?.Name ?? "Unknown",
            Type: a.AllocationType,
            TypeName: a.AllocationType.GetDisplayName(),
            Currency: Currency.BRL,
            Expiration: a.ExpirationDate,
            Current: GetCurrentBrlCached(a));
        })
        .ToList();


    // -----------------------------------------------------------------------------
    // Data loading
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Loads allocations based on current filters.
    /// </summary>
    private async Task LoadAllocations(CancellationToken cancellationToken = default, bool onlyRefreshSections = false)
    {
      isInitialAllocationsLoading = false;

      if (onlyRefreshSections)
      {
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
      }

      try
      {
        List<AllocationEntity>? result = await allocationRepository.SelectAsync(cancellationToken);

        // Capture the full unfiltered list (sorted) before applying page filters.
        // This is what gets stored in the state container so the next navigation can
        // render the complete dataset regardless of whatever filter was active here.
        List<AllocationEntity> allRaw = (result ?? new List<AllocationEntity>())
          .OrderBy(a => a.Name)
          .ToList();

        IQueryable<AllocationEntity> query = allRaw.AsQueryable();

        if (selectedAccountId > 0)
        {
          query = query.Where(a => a.AccountId == selectedAccountId);
        }

        if (!string.IsNullOrEmpty(selectedType)
          && Enum.TryParse<AllocationType>(selectedType, out AllocationType type))
        {
          query = query.Where(a => a.AllocationType == type);
        }

        if (!string.IsNullOrEmpty(selectedCurrency)
          && Enum.TryParse<Currency>(selectedCurrency, out Currency currency))
        {
          query = query.Where(a => a.Currency == currency);
        }

        if (string.Equals(selectedExpiration, "upcoming", StringComparison.OrdinalIgnoreCase))
        {
          DateTime today = DateTime.Today;
          DateTime limit = today.AddDays(90);
          query = query.Where(a => a.ExpirationDate.HasValue && a.ExpirationDate.Value.Date >= today && a.ExpirationDate.Value.Date <= limit);
        }

        allocationsBase = query.ToList();
        allocations = allocationsBase.ToList();

        // Load latest market quotes before recalculating
        latestQuotes = await marketQuoteCache.GetLatestSuccessQuotesAsync(CancellationToken.None);

        // Persist unfiltered snapshot — next page visit restores this and renders instantly.
        allocationsState.Set(allRaw, accounts, latestQuotes);

        if (!string.IsNullOrWhiteSpace(quickSearch))
        {
          ApplyQuickSearch();
        }
        else
        {
          RecalculatePortfolio();
        }

        OpenRequestedAllocationIfAvailable();
        await InvokeAsync(StateHasChanged);
      }
      finally
      {
        if (onlyRefreshSections)
        {
          isLoadingSummary = false;
          isLoadingList = false;
          await InvokeAsync(StateHasChanged);
        }
      }
    }

    /// <summary>
    /// Recalculates portfolio totals, conversions and BRL cache for current allocations.
    /// </summary>
    private void RecalculatePortfolio()
    {
      // Set USD/BRL quote properties first
      hasUsdBrlQuote = MarketQuoteCache.TryGetByType(latestQuotes, AllocationType.UsdBrl, out MarketQuoteSnapshot? usdBrlSnapshot);
      usdBrlQuote = usdBrlSnapshot;

      hasMultipleCurrencies = allocations.Select(a => a.Currency).Distinct().Count() > 1;

      // Clear and rebuild BRL cache
      currentBrlByAllocationId.Clear();

      // Calculate totals in BRL using conversion service
      decimal investedBrlTotal = 0m;
      decimal currentBrlTotal = 0m;
      hasUnconvertedCurrencies = false;

      foreach (AllocationEntity alloc in allocations)
      {
        FinancialAllocationPosition position = FinancialPositionCalculator.CalculateAllocationPosition(
          alloc,
          latestQuotes,
          pricingService,
          FinancialFactsService.ResolvePurpose);
        if (!position.AppliedIsConvertible || !position.CurrentIsConvertible)
        {
          hasUnconvertedCurrencies = true;
          currentBrlByAllocationId[alloc.Id] = 0m;
          continue;
        }

        currentBrlByAllocationId[alloc.Id] = position.CurrentAmountBrl;
        investedBrlTotal += position.AppliedAmountBrl;
        currentBrlTotal += position.CurrentAmountBrl;
      }

      totalInvested = investedBrlTotal;
      totalCurrent = currentBrlTotal;
      profit = totalCurrent - totalInvested;
      profitPercentage = totalInvested > 0m ? Math.Abs((profit / totalInvested) * 100m) : 0m;

      // Keep totalCurrentApproxBrl for backwards compatibility
      totalCurrentApproxBrl = totalCurrent;
    }

    /// <summary>
    /// Returns the cached BRL current value for an allocation.
    /// </summary>
    private decimal GetCurrentBrlCached(AllocationEntity allocation)
    {
      if (allocation == null)
      {
        return 0m;
      }

      if (currentBrlByAllocationId.TryGetValue(allocation.Id, out decimal value))
      {
        return value;
      }

      return 0m;
    }

    // -----------------------------------------------------------------------------
    // Modal operations
    // -----------------------------------------------------------------------------

    /// <summary>Handles "New Allocation" click: redirects to Accounts if none exist, otherwise opens the modal.</summary>
    private void OnNewAllocationClick()
    {
      if (!accounts.Any())
      {
        Navigation.NavigateTo("/accounts");
        return;
      }

      ShowCreateModal();
    }

    private void ShowCreateModal()
    {
      currentAllocation = new AllocationEntity
      {
        Currency = Currency.BRL,
        AllocationType = AllocationType.Stocks,
        Quantity = 1m
      };
      isDirty = false;
      isEditing = false;
      showModal = true;
    }

    private void ShowEditModal(AllocationEntity allocation)
    {
      currentAllocation = new AllocationEntity
      {
        Id = allocation.Id,
        Name = allocation.Name,
        Initials = allocation.Initials,
        AllocationType = allocation.AllocationType,
        PlanningPurpose = allocation.PlanningPurpose,
        BuyUnitValue = allocation.BuyUnitValue,
        Quantity = allocation.Quantity,
        CurrentUnitValue = allocation.CurrentUnitValue,
        Currency = allocation.Currency,
        SecondCurrency = allocation.SecondCurrency,
        ProfitType = allocation.ProfitType,
        ProfitPercentage = allocation.ProfitPercentage,
        ProfitDate = allocation.ProfitDate,
        ExpirationDate = allocation.ExpirationDate,
        Annotations = allocation.Annotations,
        AccountId = allocation.AccountId
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
      currentAllocation = new AllocationEntity();
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

    private void CancelDiscard()
    {
      showConfirmDiscard = false;
    }

    private void ConfirmDiscard()
    {
      HideModal();
    }


    // -----------------------------------------------------------------------------
    // CRUD operations
    // -----------------------------------------------------------------------------

    private async Task SaveAllocation(CancellationToken cancellationToken = default)
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      if (currentAllocation.AccountId == 0)
      {
        ShowMessage("Select an account", true);
        return;
      }

      isSaving = true;
      StateHasChanged();
      await Task.Delay(1);

      int? previousAccountId = null;

      try
      {
        await RefreshAutomaticFixedIncomeValueAsync(currentAllocation, cancellationToken);

        if (isEditing)
        {
          AllocationEntity? existing = await allocationRepository.SelectAsync(currentAllocation.Id);
          if (existing != null)
          {
            AllocationEntity previousAllocation = CloneAllocation(existing);
            previousAccountId = existing.AccountId;
            existing.Name = currentAllocation.Name;
            existing.Initials = currentAllocation.Initials;
            existing.AllocationType = currentAllocation.AllocationType;
            existing.PlanningPurpose = currentAllocation.PlanningPurpose;
            existing.BuyUnitValue = currentAllocation.BuyUnitValue;
            existing.Quantity = currentAllocation.Quantity;
            existing.CurrentUnitValue = currentAllocation.CurrentUnitValue;
            existing.Currency = currentAllocation.Currency;
            existing.SecondCurrency = currentAllocation.SecondCurrency;
            existing.ProfitType = currentAllocation.ProfitType;
            existing.ProfitPercentage = currentAllocation.ProfitPercentage;
            existing.ProfitDate = currentAllocation.ProfitDate;
            existing.ExpirationDate = currentAllocation.ExpirationDate;
            existing.Annotations = currentAllocation.Annotations;
            existing.AccountId = currentAllocation.AccountId;

            await allocationRepository.UpdateAsync(existing);
            await RecordAllocationHistoryAsync(previousAllocation, existing, cancellationToken);
          }
        }
        else
        {
          await allocationRepository.InsertAsync(currentAllocation);
          await RecordAllocationHistoryAsync(null, currentAllocation, cancellationToken);
        }

        await financialSyncService.SyncAccountAsync(currentAllocation.AccountId, cancellationToken);

        // Moving an allocation removes its value from the previous account, so that account must be recomputed too.
        if (previousAccountId.HasValue && previousAccountId != currentAllocation.AccountId)
        {
          await financialSyncService.SyncAccountAsync(previousAccountId, cancellationToken);
        }

        await LoadAllocations(cancellationToken);
        HideModal();
        ShowMessage(isEditing ? "Allocation updated successfully!" : "Allocation created successfully!", false);
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

    private async Task RefreshAutomaticFixedIncomeValueAsync(
      AllocationEntity allocation,
      CancellationToken cancellationToken)
    {
      if (!FixedIncomeYieldCalculator.CanCalculateAllocation(allocation))
      {
        return;
      }

      FixedIncomeCalculationResult? calculation = await FixedIncomeYieldCalculator.CalculateAsync(
        allocation,
        httpClientFactory,
        logger,
        cancellationToken);

      if (calculation == null || calculation.CurrentUnitValue <= 0m)
      {
        return;
      }

      allocation.CurrentUnitValue = Math.Round(calculation.CurrentUnitValue, 2);
    }

    private void ConfirmDelete(AllocationEntity allocation)
    {
      allocationToDelete = allocation;
      showDeleteConfirm = true;
    }

    private async Task DeleteAllocation(CancellationToken cancellationToken = default)
    {
      if (!await EnsureWriteAllowedAsync())
      {
        return;
      }

      if (allocationToDelete == null)
      {
        return;
      }

      isSaving = true;
      StateHasChanged();
      await Task.Delay(1);

      try
      {
        AllocationEntity? alloc = await allocationRepository.SelectOneAsync(a => a.Id == allocationToDelete.Id, cancellationToken);
        if (alloc != null)
        {
          await RecordAllocationRemovalAsync(alloc, cancellationToken);
          await allocationRepository.DeleteAsync(alloc.Id);
          await financialSyncService.SyncAccountAsync(alloc.AccountId, cancellationToken);
          await LoadAllocations(cancellationToken);
          ShowMessage("Allocation deleted successfully!", false);
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
        allocationToDelete = null;
      }
    }

    private async Task RecordAllocationHistoryAsync(
      AllocationEntity? previous,
      AllocationEntity current,
      CancellationToken cancellationToken)
    {
      decimal previousNative = previous == null ? 0m : GetInvestedNative(previous);
      decimal currentNative = GetInvestedNative(current);
      decimal deltaNative = currentNative - previousNative;
      if (deltaNative == 0m)
      {
        return;
      }

      AllocationHistoryKind kind = previous == null
        ? AllocationHistoryKind.Created
        : deltaNative > 0m
          ? AllocationHistoryKind.Increased
          : AllocationHistoryKind.Reduced;
      await InsertAllocationHistoryAsync(
        current,
        kind,
        previousNative,
        currentNative,
        deltaNative,
        cancellationToken);
    }

    private async Task RecordAllocationRemovalAsync(
      AllocationEntity allocation,
      CancellationToken cancellationToken)
    {
      decimal currentNative = GetInvestedNative(allocation);
      if (currentNative == 0m)
      {
        return;
      }

      await InsertAllocationHistoryAsync(
        allocation,
        AllocationHistoryKind.Removed,
        currentNative,
        0m,
        -currentNative,
        cancellationToken);
    }

    private async Task InsertAllocationHistoryAsync(
      AllocationEntity allocation,
      AllocationHistoryKind kind,
      decimal previousNative,
      decimal currentNative,
      decimal deltaNative,
      CancellationToken cancellationToken)
    {
      string userId = await GetCurrentUserIdAsync();
      AllocationHistoryEntity history = new AllocationHistoryEntity
      {
        OwnerUserId = userId,
        AllocationId = allocation.Id,
        AccountId = allocation.AccountId,
        Kind = kind,
        PlanningPurpose = FinancialFactsService.ResolvePurpose(allocation),
        OccurredAt = DateTime.UtcNow,
        PreviousNativeAmount = previousNative,
        NewNativeAmount = currentNative,
        DeltaNativeAmount = deltaNative,
        AmountBrl = ConvertInvestedDeltaToBrl(allocation, deltaNative),
        Currency = allocation.Currency,
        AllocationName = allocation.Name,
      };

      await using LifePlannerContext context = await ContextFactory.CreateDbContextAsync(cancellationToken);
      context.AllocationHistories.Add(history);
      await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
      AuthenticationState state = await AuthStateProvider.GetAuthenticationStateAsync();
      return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? LifePlannerContext.DemoUserId;
    }

    private decimal ConvertInvestedDeltaToBrl(AllocationEntity allocation, decimal deltaNative)
    {
      AllocationBrlConversionResult conversion = pricingService.ConvertAmountToBrl(
        allocation,
        deltaNative,
        latestQuotes);
      return conversion.IsConvertible ? conversion.AmountBrl : deltaNative;
    }

    private static decimal GetInvestedNative(AllocationEntity allocation)
    {
      return FinancialPositionCalculator.CalculateAppliedNativeValue(allocation);
    }

    private static AllocationEntity CloneAllocation(AllocationEntity allocation)
    {
      return new AllocationEntity
      {
        Id = allocation.Id,
        Name = allocation.Name,
        Initials = allocation.Initials,
        AllocationType = allocation.AllocationType,
        PlanningPurpose = allocation.PlanningPurpose,
        BuyUnitValue = allocation.BuyUnitValue,
        Quantity = allocation.Quantity,
        CurrentUnitValue = allocation.CurrentUnitValue,
        Currency = allocation.Currency,
        SecondCurrency = allocation.SecondCurrency,
        ProfitType = allocation.ProfitType,
        ProfitPercentage = allocation.ProfitPercentage,
        ProfitDate = allocation.ProfitDate,
        ExpirationDate = allocation.ExpirationDate,
        Annotations = allocation.Annotations,
        AccountId = allocation.AccountId,
      };
    }


    // -----------------------------------------------------------------------------
    // UI helpers
    // -----------------------------------------------------------------------------

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
      await InvokeAsync(StateHasChanged);
    }
  }
}
