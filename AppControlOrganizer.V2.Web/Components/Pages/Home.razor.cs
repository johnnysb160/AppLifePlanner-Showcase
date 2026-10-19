//-----------------------------------------------------------------------
// <copyright file="Home.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using AppControlOrganizer.V2.Shared.Entities;
	using AppControlOrganizer.V2.Shared.Enums;
	using AppControlOrganizer.V2.Shared.Models;
  using AppControlOrganizer.V2.Web.Data;
	using AppControlOrganizer.V2.Web.Data.Auth;
	using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
	using AppControlOrganizer.V2.Web.Helpers;
  using AppControlOrganizer.V2.Web.Components.Shared.Common;
	using AppControlOrganizer.V2.Web.Services;
  using AppControlOrganizer.V2.Web.Services.Financial;
	using Microsoft.AspNetCore.Components;
	using Microsoft.AspNetCore.Components.Authorization;
	using Microsoft.AspNetCore.Http;
	using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.JSInterop;

	/// <summary>
	/// Code-behind for Home.razor (Dashboard page). Displays financial overview with month/year selector for filtering data.
	/// </summary>
	public partial class Home : ComponentBase, IDisposable
	{
		/// <summary>
		/// Gets or sets the repository used to manage Transaction data.
		/// </summary>
		[Inject]
		public ITransactionRepository TransactionRepository { get; set; } = null!;

		/// <summary>
		/// Gets or sets the account repository used to access account data.
		/// </summary>
		[Inject]
		public IAccountRepository AccountRepository { get; set; } = null!;

		/// <summary>
		/// Gets or sets the repository used to manage allocation data.
		/// </summary>
		[Inject]
		public IAllocationRepository AllocationRepository { get; set; } = null!;

    /// <summary>Gets or sets the canonical financial-facts service.</summary>
    [Inject]
    public IFinancialFactsService FinancialFactsService { get; set; } = null!;

    /// <summary>Gets or sets the context factory used for page-specific read models.</summary>
    [Inject]
    public IDbContextFactory<LifePlannerContext> DbContextFactory { get; set; } = null!;

		/// <summary>Gets or sets the scope factory used to resolve scoped services.</summary>
		[Inject]
		private IServiceScopeFactory ScopeFactory { get; set; } = null!;

		/// <summary>Gets or sets the authentication state provider.</summary>
		[Inject]
		private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

		/// <summary>Gets or sets the JS runtime used to obtain the client's local hour.</summary>
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		/// <summary>Gets or sets the HTTP context accessor used to read the timezone cookie during SSR.</summary>
		[Inject]
		private IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

		#region State Fields

		private bool isInitialDashboardLoading = true;
		private int selectedMonth = DateTime.Now.Month;
		private int selectedYear = DateTime.Now.Year;
		private int totalAccounts;
    private int totalCards;
		private int totalAllocations;
		private decimal monthlyIncome;
		private decimal monthlyExpense;
		private decimal monthlyBalance;
		private decimal totalBalance;
		private decimal totalCurrentValue;
    private decimal canonicalNetWorth;
    private decimal monthlyContributions;
    private decimal emergencyReserve;
    private decimal plannedYearEndNetWorth;
    private FinancialFacts financialFacts = new FinancialFacts();
    private List<ContributionDetail> contributionDetails = new();
    private string currentUserId = LifePlannerContext.DemoUserId;
		private int monthlyIncomeCount;
		private int monthlyExpenseCount;
		private bool isLoadingIncomeExpense = false;
		private bool isLoadingExpensesByCategory = false;
		private List<CategorySummary> expensesByCategory = new();
		private List<AccountSummary> recentAccounts = new();
		private bool initialized = false;
		private string navDisplayName = string.Empty;

    private decimal CategorizedExpenseTotal
    {
      get
      {
        return this.expensesByCategory.Sum(category => category.Total);
      }
    }

		// -1 = not yet resolved; updated after first render via JS interop (client local time).
		private int clientHour = -1;

		/// <summary>Returns a time-based greeting key resolved through the current locale.</summary>
		private string Greeting => this.clientHour < 0
		  ? string.Empty
		  : Loc[this.clientHour < 12 ? "Good morning" : this.clientHour < 18 ? "Good afternoon" : "Good evening"];

		#endregion

		#region Lifecycle Methods

		/// <inheritdoc />
		public void Dispose()
		{
			ValuesVis.OnChange -= OnValuesVisibilityChanged;
		}

		private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

		/// <inheritdoc />
		protected override Task OnInitializedAsync()
		{
			ValuesVis.OnChange += OnValuesVisibilityChanged;
			// During SSR prerender the HttpContext is available — read the timezone cookie
			// set by the client-side script on the previous request so the greeting is
			// already correct in the server-rendered HTML (no flash for returning visitors).
			HttpContext? ctx = HttpContextAccessor.HttpContext;
			if (ctx is not null
				&& ctx.Request.Cookies.TryGetValue("tz_hour", out string? hourStr)
				&& int.TryParse(hourStr, out int cookieHour)
				&& cookieHour is >= 0 and <= 23)
			{
				clientHour = cookieHour;
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Lifecycle: initialize once after first render to avoid prerender/interactive double-load flicker.
		/// </summary>
		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!this.initialized)
			{
				this.initialized = true;

				// window.__clientHour is set synchronously by the inline script in App.razor
				// before Blazor.start(), so it is always available here — no retry needed.
				// Call StateHasChanged immediately after resolving the hour so the greeting
				// updates at the same time as MainLayout (avoids showing stale cookie value
				// while data is still loading).
				try
				{
					this.clientHour = await this.JSRuntime.InvokeAsync<int>(
					  "lpGetClientHour", TimeSpan.FromSeconds(5));
					await this.InvokeAsync(this.StateHasChanged);
				}
				catch
				{
					// Keeps whatever the cookie provided, or -1 if first ever visit.
				}

				// Resolve the authenticated user's display name for the greeting banner.
				AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();
        this.currentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
          ?? LifePlannerContext.DemoUserId;
				if (authState.User.Identity?.IsAuthenticated == true)
				{
					using IServiceScope scope = this.ScopeFactory.CreateScope();
					UserManager<ApplicationUser> userManager =
					  scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
					ApplicationUser? appUser = await userManager.GetUserAsync(authState.User);
					if (appUser is not null)
					{
						this.navDisplayName = !string.IsNullOrWhiteSpace(appUser.DisplayName)
						  ? appUser.DisplayName
						  : appUser.UserName ?? string.Empty;
					}
				}
				else
				{
					// Unauthenticated visitor: show localized "Visitor" label.
					this.navDisplayName = Loc["Visitor"];
				}

				await this.SetDefaultMonthForDemoUserAsync();

				bool hadAccounts = this.recentAccounts != null && this.recentAccounts.Any();
				await this.LoadDashboardData(CancellationToken.None, showSpinner: !hadAccounts);
				await this.InvokeAsync(this.StateHasChanged);
			}
		}

		#endregion

		#region Data Loading

		/// <summary>
		/// When no user is authenticated, automatically selects the most recent month that
		/// has demo transactions, so the dashboard never opens empty for unauthenticated visitors.
		/// </summary>
		private async Task SetDefaultMonthForDemoUserAsync()
		{
			AuthenticationState auth = await this.AuthStateProvider.GetAuthenticationStateAsync();
			if (auth.User.Identity?.IsAuthenticated == true)
			{
				return;
			}

      DateTime? mostRecent = await this.TransactionRepository.SelectMostRecentDateAsync(CancellationToken.None);
      if (!mostRecent.HasValue)
			{
				return;
			}

      this.selectedMonth = mostRecent.Value.Month;
      this.selectedYear = mostRecent.Value.Year;
		}

		/// <summary>
		/// Loads dashboard data for the selected month/year. Queries accounts, allocations, and transactions from database.
		/// </summary>
		private async Task LoadDashboardData(CancellationToken cancellationToken = default, bool showSpinner = false, bool onlyRefreshSections = false)
		{
			this.isInitialDashboardLoading = false;

			if (onlyRefreshSections)
			{
				this.isLoadingIncomeExpense = true;
				this.isLoadingExpensesByCategory = true;
				await this.InvokeAsync(this.StateHasChanged);
				await Task.Yield();
			}

			try
			{
				DateTime startOfMonth = new DateTime(this.selectedYear, this.selectedMonth, 1);
				DateTime endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        DateOnly referenceDate = this.GetFinancialFactsReferenceDate();
        Task<List<TransactionEntity>?> monthlyTransactionsTask =
          this.TransactionRepository.SelectByDateRangeAsync(startOfMonth, endOfMonth, cancellationToken);
        Task<FinancialFacts> canonicalFactsTask = this.FinancialFactsService.GetWithAnnualPlanAsync(
          this.currentUserId,
          referenceDate,
          cancellationToken);
        Task<List<AllocationHistoryEntity>> allocationHistoriesTask =
          this.LoadMonthlyAllocationHistoriesAsync(startOfMonth, endOfMonth.AddDays(1), cancellationToken);

        if (onlyRefreshSections)
        {
          await Task.WhenAll(monthlyTransactionsTask, canonicalFactsTask, allocationHistoriesTask);
          this.ApplyMonthlyData(
            await monthlyTransactionsTask ?? [],
            await canonicalFactsTask,
            await allocationHistoriesTask,
            referenceDate);
          return;
        }

        Task<List<AccountEntity>?> accountsTask = this.AccountRepository.SelectAsync(cancellationToken);
        Task<List<AllocationEntity>?> allocationsTask = this.AllocationRepository.SelectAsync(cancellationToken);
        await Task.WhenAll(
          monthlyTransactionsTask,
          canonicalFactsTask,
          allocationHistoriesTask,
          accountsTask,
          allocationsTask);

        List<TransactionEntity> monthlyTransactions = await monthlyTransactionsTask ?? [];
        FinancialFacts canonical = await canonicalFactsTask;
        List<AllocationHistoryEntity> allocationHistories = await allocationHistoriesTask;
        List<AccountEntity> accounts = await accountsTask ?? [];
        List<AllocationEntity> allAllocations = await allocationsTask ?? [];
        this.ApplyMonthlyData(monthlyTransactions, canonical, allocationHistories, referenceDate);
        this.totalAccounts = accounts.Count(account => !account.Deleted);
        this.totalCards = accounts
          .Where(account => !account.Deleted)
          .Sum(account => account.Cards.Count(card => !card.Deleted));
        this.totalAllocations = allAllocations.Count(allocation => !allocation.Deleted);
        this.totalBalance = canonical.AccountCash;
        this.totalCurrentValue = canonical.AllocationCurrentValue;
        this.canonicalNetWorth = canonical.NetWorth;
        Dictionary<int, FinancialAccountPosition> accountPositions = canonical.AccountPositions
          .ToDictionary(position => position.AccountId);

        this.recentAccounts = accounts
          .Where(a => !a.Deleted)
          .Select(a =>
          {
            accountPositions.TryGetValue(a.Id, out FinancialAccountPosition? position);
            return new AccountSummary
            {
              Id = a.Id,
              Name = a.Name,
              Logo = a.Logo,
              InstitutionType = a.InstitutionType,
              Currency = a.Currency,
              AllocationCount = a.Allocations.Count(al => !al.Deleted),
              CardCount = a.Cards.Count(c => !c.Deleted),
              Balance = position?.IncludedCash ?? 0m,
              TotalInvested = position?.AllocationCurrentValue ?? 0m,
            };
          })
          .OrderByDescending(a => a.Balance + a.TotalInvested)
          .Take(5)
          .ToList();
			}
			finally
			{
				if (onlyRefreshSections)
				{
					this.isLoadingIncomeExpense = false;
					this.isLoadingExpensesByCategory = false;
					await this.InvokeAsync(this.StateHasChanged);
				}
			}
		}

    private void ApplyMonthlyData(
      List<TransactionEntity> monthlyTransactions,
      FinancialFacts canonical,
      List<AllocationHistoryEntity> allocationHistories,
      DateOnly referenceDate)
    {
      FinancialMonthlyCashFlow monthlyCashFlow = FinancialMonthlyCashFlowCalculator.Calculate(
        monthlyTransactions,
        allocationHistories,
        referenceDate);
      decimal rawExpense = monthlyTransactions.Where(transaction => !transaction.Income).Sum(transaction => transaction.Value);
      decimal categoryTotal = rawExpense > 0m ? rawExpense : 1m;
      this.monthlyIncomeCount = monthlyCashFlow.OperatingIncomeCount;
      this.monthlyExpenseCount = monthlyCashFlow.OperatingExpenseCount;
      this.expensesByCategory = monthlyTransactions
        .Where(transaction => !transaction.Income)
        .GroupBy(transaction => transaction.Category)
        .Select(group => new CategorySummary
        {
          Category = group.Key,
          CategoryName = Helper.GetDisplayName(group.Key),
          Total = group.Sum(transaction => transaction.Value),
          Percentage = (group.Sum(transaction => transaction.Value) / categoryTotal) * 100m,
        })
        .OrderByDescending(category => category.Total)
        .ToList();
      this.canonicalNetWorth = canonical.NetWorth;
      this.monthlyIncome = canonical.MonthlyOperatingIncome;
      this.monthlyExpense = canonical.MonthlyOperatingExpense;
      this.monthlyBalance = monthlyCashFlow.ResultWithContributions;
      this.monthlyContributions = canonical.MonthlyContributions;
      this.emergencyReserve = canonical.EmergencyReserve;
      this.plannedYearEndNetWorth = canonical.PlannedYearEndNetWorth;
      this.financialFacts = canonical;
      this.contributionDetails = this.BuildContributionDetails(monthlyCashFlow);
    }

    private async Task<List<AllocationHistoryEntity>> LoadMonthlyAllocationHistoriesAsync(
      DateTime monthStart,
      DateTime monthEndExclusive,
      CancellationToken cancellationToken)
    {
      await using LifePlannerContext context = await this.DbContextFactory.CreateDbContextAsync(cancellationToken);
      return await context.AllocationHistories
        .AsNoTracking()
        .Where(history => history.OwnerUserId == this.currentUserId
          && history.OccurredAt >= monthStart
          && history.OccurredAt < monthEndExclusive)
        .OrderByDescending(history => history.OccurredAt)
        .ToListAsync(cancellationToken);
    }

    private DateOnly GetFinancialFactsReferenceDate()
    {
      DateOnly today = DateOnly.FromDateTime(DateTime.Now);
      if (this.selectedYear == today.Year && this.selectedMonth == today.Month)
      {
        return today;
      }

      return new DateOnly(
        this.selectedYear,
        this.selectedMonth,
        DateTime.DaysInMonth(this.selectedYear, this.selectedMonth));
    }

    private List<ContributionDetail> BuildContributionDetails(FinancialMonthlyCashFlow monthlyCashFlow)
    {
      List<ContributionDetail> details = monthlyCashFlow.ContributionTransactions
        .Select(transaction => new ContributionDetail
        {
          OccurredAt = transaction.Date,
          Title = string.IsNullOrWhiteSpace(transaction.Description)
            ? Loc["Transaction contribution"]
            : transaction.Description,
          DetailKey = "Transaction contribution",
          AmountBrl = transaction.Value,
          IsTransaction = true,
        })
        .ToList();

      foreach (AllocationHistoryEntity history in monthlyCashFlow.PositiveContributionHistories)
      {
        details.Add(new ContributionDetail
        {
          OccurredAt = history.OccurredAt,
          Title = string.IsNullOrWhiteSpace(history.AllocationName)
            ? Loc["Allocation"]
            : history.AllocationName,
          DetailKey = GetAllocationHistoryLabelKey(history.Kind),
          AmountBrl = history.AmountBrl,
          HistoryKind = history.Kind,
        });
      }

      return details
        .OrderByDescending(detail => detail.OccurredAt)
        .ThenBy(detail => detail.Title)
        .ToList();
    }

		/// <summary>
		/// Handles month selection change - reloads dashboard data.
		/// </summary>
		private async Task OnMonthChanged(ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out int month))
			{
				this.selectedMonth = month;
				this.isLoadingIncomeExpense = true;
				this.isLoadingExpensesByCategory = true;
				await this.InvokeAsync(this.StateHasChanged);
				await Task.Yield();
				await this.LoadDashboardData(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
			}
		}

		/// <summary>
		/// Handles year selection change - reloads dashboard data.
		/// </summary>
		private async Task OnYearChanged(ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out int year))
			{
				this.selectedYear = year;
				this.isLoadingIncomeExpense = true;
				this.isLoadingExpensesByCategory = true;
				await this.InvokeAsync(this.StateHasChanged);
				await Task.Yield();
				await this.LoadDashboardData(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
			}
		}

		/// <summary>
		/// Resets selection to current month and reloads data.
		/// </summary>
		private async Task GoToCurrentMonth(CancellationToken cancellationToken = default)
		{
			this.selectedMonth = DateTime.Now.Month;
			this.selectedYear = DateTime.Now.Year;
			this.isLoadingIncomeExpense = true;
			this.isLoadingExpensesByCategory = true;
			await this.InvokeAsync(this.StateHasChanged);
			await Task.Yield();
			await this.LoadDashboardData(cancellationToken, showSpinner: false, onlyRefreshSections: true);
		}

		/// <summary>
		/// Parameterless wrapper for UI bindings.
		/// </summary>
		private Task GoToCurrentMonth()
		{
			return this.GoToCurrentMonth(CancellationToken.None);
		}

		/// <summary>
		/// Direct month selection for custom dropdown UI (no ChangeEventArgs needed).
		/// </summary>
		private async Task SelectMonth(int month)
		{
			this.selectedMonth = month;
			this.isLoadingIncomeExpense = true;
			this.isLoadingExpensesByCategory = true;
			await this.InvokeAsync(this.StateHasChanged);
			await Task.Yield();
			await this.LoadDashboardData(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
		}

		/// <summary>
		/// Direct year selection for custom dropdown UI (no ChangeEventArgs needed).
		/// </summary>
		private async Task SelectYear(int year)
		{
			this.selectedYear = year;
			this.isLoadingIncomeExpense = true;
			this.isLoadingExpensesByCategory = true;
			await this.InvokeAsync(this.StateHasChanged);
			await Task.Yield();
			await this.LoadDashboardData(CancellationToken.None, showSpinner: false, onlyRefreshSections: true);
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Gets localized month name for display in dropdown.
		/// </summary>
    private string GetMonthName(int month)
		{
      return Loc[CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)];
		}

    private string GetTransactionsHref(bool? income = null, Category? category = null)
    {
      string href = $"transactions?month={this.selectedMonth}&year={this.selectedYear}";
      if (income.HasValue)
      {
        href += income.Value ? "&type=income" : "&type=expense";
      }

      if (category.HasValue)
      {
        href += $"&category={category.Value}";
      }

      return href;
    }

    private string GetReserveTileValue()
    {
      return ValuesVis.Mask(Helper.FormatBrl(this.emergencyReserve));
    }

    private string GetContributionDetailAmount(ContributionDetail detail)
    {
      string amount = ValuesVis.Mask(Helper.FormatBrl(Math.Abs(detail.AmountBrl)));
      return detail.AmountBrl < 0m ? $"-{amount}" : amount;
    }

    private string GetContributionDetailDate(ContributionDetail detail)
    {
      return detail.OccurredAt.ToString(Loc.CompactDayMonthPattern, Loc.Culture);
    }

    private string GetContributionDetailIcon(ContributionDetail detail)
    {
      if (detail.IsTransaction)
      {
        return "bi-receipt";
      }

      return detail.HistoryKind switch
      {
        AllocationHistoryKind.Created => "bi-plus-lg",
        AllocationHistoryKind.Increased => "bi-arrow-up-right",
        AllocationHistoryKind.Reduced => "bi-arrow-down-left",
        AllocationHistoryKind.Removed => "bi-trash3",
        _ => "bi-dot",
      };
    }

    private string GetContributionDetailTone(ContributionDetail detail)
    {
      if (detail.AmountBrl < 0m)
      {
        return "negative";
      }

      return detail.IsTransaction ? "transaction" : "positive";
    }

    private IReadOnlyList<FinancialDetailsPopoverRow> GetContributionPopoverRows()
    {
      return this.contributionDetails
        .Select(detail => new FinancialDetailsPopoverRow
        {
          IconClass = this.GetContributionDetailIcon(detail),
          Title = detail.Title,
          Detail = $"{this.GetContributionDetailDate(detail)} · {Loc[detail.DetailKey]}",
          Amount = this.GetContributionDetailAmount(detail),
          Tone = this.GetContributionDetailTone(detail),
        })
        .ToList();
    }

    private static string GetAllocationHistoryLabelKey(AllocationHistoryKind kind)
    {
      return kind switch
      {
        AllocationHistoryKind.Created => "Created allocation",
        AllocationHistoryKind.Increased => "Increased allocation",
        AllocationHistoryKind.Reduced => "Reduced allocation",
        AllocationHistoryKind.Removed => "Removed allocation",
        _ => "Allocation movement",
      };
    }

    private sealed class ContributionDetail
    {
      public DateTime OccurredAt { get; set; }

      public string Title { get; set; } = string.Empty;

      public string DetailKey { get; set; } = string.Empty;

      public decimal AmountBrl { get; set; }

      public bool IsTransaction { get; set; }

      public AllocationHistoryKind? HistoryKind { get; set; }
    }

    private static string GetAccountHref(int accountId)
    {
      return $"accounts?accountId={accountId}";
    }

    private static string GetCategoryTone(int index)
    {
      return $"tone-{index % 6}";
    }

		#endregion
	}
}
