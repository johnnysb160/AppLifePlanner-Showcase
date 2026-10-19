//-----------------------------------------------------------------------
// <copyright file="DemoDataSeeder.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Demo;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Populates the showcase tenant read by anonymous visitors so every screen the product offers
/// renders with believable content.
/// </summary>
/// <remarks>
/// Two rules govern this seeder. Every group checks itself independently, so a group added later —
/// or one wiped by hand — is restored without touching the groups that already exist. And every
/// date is derived from the run date instead of being written as a literal, so the showcase never
/// drifts into the past between deployments.
/// </remarks>
public static class DemoDataSeeder
{
  /// <summary>Owner assigned to every showcase row.</summary>
  internal const string Owner = LifePlannerContext.DemoUserId;

  /// <summary>Current revision of the showcase content produced by this seeder.</summary>
  /// <remarks>Bump this whenever seeded text changes, so deployed showcase rows are rebuilt.</remarks>
  private const int ShowcaseContentVersion = 2;

  /// <summary>Application-setting key holding the showcase revision already applied.</summary>
  private const string ShowcaseVersionKey = "DemoShowcaseVersion";

  /// <summary>Months of transaction history kept behind the reference month.</summary>
  private const int TransactionHistoryMonths = 6;

  /// <summary>
  /// Ensures the showcase tenant is complete and anchored to the current date.
  /// </summary>
  /// <param name="rootServices">Root service provider used to open a scope.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A task representing the seeding run.</returns>
  public static async Task SeedAsync(IServiceProvider rootServices, CancellationToken cancellationToken)
  {
    using IServiceScope scope = rootServices.CreateScope();
    LifePlannerContext db = scope.ServiceProvider.GetRequiredService<LifePlannerContext>();
    ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder).FullName!);

    await SeedAsync(db, logger, cancellationToken);
  }

  /// <summary>
  /// Ensures the showcase tenant is complete and anchored to the current date.
  /// </summary>
  /// <param name="db">Database context scoped to the caller.</param>
  /// <param name="logger">Logger used to report each group.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A task representing the seeding run.</returns>
  public static async Task SeedAsync(LifePlannerContext db, ILogger logger, CancellationToken cancellationToken)
  {
    // The showcase tenant rejects request-driven writes; this is the one authorized maintenance path.
    using IDisposable demoScope = LifePlannerContext.AllowDemoWrites();

    DateTime today = DateTime.UtcNow.Date;
    logger.LogInformation("Demo data seeder running for reference date {Date:yyyy-MM-dd}.", today);

    try
    {
      await ResetOutdatedShowcaseAsync(db, logger, cancellationToken);
      await ClearCodeSeededPlanValuesAsync(db, logger, cancellationToken);

      List<CategoryEntity> categories = await ReadCategoriesAsync(db, cancellationToken);
      List<AccountEntity> accounts = await EnsureAccountsAsync(db, logger, cancellationToken);
      await EnsureCreditCardsAsync(db, logger, accounts, cancellationToken);
      List<AllocationEntity> allocations = await EnsureAllocationsAsync(db, logger, accounts, cancellationToken);
      await EnsureAllocationHistoriesAsync(db, logger, allocations, today, cancellationToken);
      await EnsureTransactionsAsync(db, logger, accounts, categories, today, cancellationToken);
      await DemoPlanningDataSeeder.SeedAsync(db, logger, today, cancellationToken);

      logger.LogInformation("Demo data seeder finished.");
    }
    catch (Exception exception)
    {
      // A broken showcase must never take the application down on startup.
      logger.LogError(exception, "Demo data seeding failed. The application continues without a complete showcase.");
    }
  }

  /// <summary>
  /// Clears the planning values that used to be injected from code into every annual plan.
  /// </summary>
  /// <remarks>
  /// A baseline used to be written into each profile from a hardcoded table, and the projection
  /// calculator carried a matching special case to ignore the contribution it wrote. Both are gone;
  /// this clears the leftover rows so the projection resumes deriving the contribution from real
  /// history. Resetting the marker makes the cleanup run exactly once per row.
  /// </remarks>
  /// <param name="db">Database context.</param>
  /// <param name="logger">Logger used to report the cleanup.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>A task representing the cleanup.</returns>
  private static async Task ClearCodeSeededPlanValuesAsync(LifePlannerContext db, ILogger logger, CancellationToken ct)
  {
    const decimal LegacySeededContribution = 1684m;

    List<AnnualPlanEntity> plans = await db.AnnualPlans
      .IgnoreQueryFilters()
      .Where(item => item.InitialDataVersion > 0)
      .ToListAsync(ct);
    if (plans.Count == 0)
    {
      return;
    }

    foreach (AnnualPlanEntity plan in plans)
    {
      if (plan.PlannedMonthlyContribution == LegacySeededContribution)
      {
        plan.PlannedMonthlyContribution = null;
      }

      // The showcase inherited the same personal baseline; replace it with neutral demo figures.
      if (plan.OwnerUserId == Owner)
      {
        DemoPlanningDataSeeder.ApplyShowcaseAnnualPlan(plan);
      }

      plan.InitialDataVersion = 0;
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Cleared code-seeded planning values from {Count} annual plan(s).", plans.Count);
  }

  /// <summary>Reads the showcase rows of a tenant-scoped set, bypassing the ambient tenant filter.</summary>
  /// <typeparam name="TEntity">Tenant-scoped entity type.</typeparam>
  /// <param name="set">Set to read.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The rows owned by the showcase tenant.</returns>
  internal static Task<List<TEntity>> ReadOwnedAsync<TEntity>(DbSet<TEntity> set, CancellationToken ct)
    where TEntity : BaseEntity
  {
    return set
      .IgnoreQueryFilters()
      .Where(item => !item.Deleted && EF.Property<string>(item, "OwnerUserId") == Owner)
      .ToListAsync(ct);
  }

  // ── Financial groups ────────────────────────────────────────────────────────

  private static async Task<List<CategoryEntity>> ReadCategoriesAsync(LifePlannerContext db, CancellationToken ct)
  {
    // Categories are shared by every tenant: create the predefined set only when the table is empty.
    bool categoriesExist = await db.Categories.IgnoreQueryFilters().AnyAsync(ct);
    if (!categoriesExist)
    {
      db.Categories.AddRange(
        new CategoryEntity { Name = "Salary", Code = "SALARY", IconCss = "bi bi-wallet2", IsIncome = true, IsPredefined = true },
        new CategoryEntity { Name = "Freelance", Code = "FREELANCE", IconCss = "bi bi-briefcase", IsIncome = true, IsPredefined = true },
        new CategoryEntity { Name = "Food", Code = "FOOD", IconCss = "bi bi-cart3", IsIncome = false, IsPredefined = true },
        new CategoryEntity { Name = "Transport", Code = "TRANSPORT", IconCss = "bi bi-bus-front", IsIncome = false, IsPredefined = true },
        new CategoryEntity { Name = "Health", Code = "HEALTH", IconCss = "bi bi-heart-pulse", IsIncome = false, IsPredefined = true },
        new CategoryEntity { Name = "Entertainment", Code = "LEISURE", IconCss = "bi bi-controller", IsIncome = false, IsPredefined = true },
        new CategoryEntity { Name = "Home", Code = "HOME", IconCss = "bi bi-house-door", IsIncome = false, IsPredefined = true },
        new CategoryEntity { Name = "Investment", Code = "INVEST", IconCss = "bi bi-graph-up", IsIncome = false, IsPredefined = true });
      await db.SaveChangesAsync(ct);
    }

    return await db.Categories.IgnoreQueryFilters().Where(item => !item.Deleted).ToListAsync(ct);
  }

  private static async Task<List<AccountEntity>> EnsureAccountsAsync(
    LifePlannerContext db,
    ILogger logger,
    CancellationToken ct)
  {
    List<AccountEntity> existing = await ReadOwnedAsync(db.Accounts, ct);
    if (existing.Count > 0)
    {
      return existing;
    }

    logger.LogInformation("Seeding showcase accounts...");
    AccountEntity[] accounts =
    {
      new() { Name = "Nubank", InstitutionType = InstitutionType.Fintech, AccountType = AccountType.CheckingAccount, Currency = Currency.BRL, AccountIsActive = true, HasCard = true, DisplayOrder = 1 },
      new() { Name = "Itau", InstitutionType = InstitutionType.Bank, AccountType = AccountType.CheckingAccount, Currency = Currency.BRL, AccountIsActive = true, DisplayOrder = 2 },
      new() { Name = "XP Investimentos", InstitutionType = InstitutionType.Broker, AccountType = AccountType.InvestmentAccount, Currency = Currency.BRL, AccountIsActive = true, HasInvestments = true, DisplayOrder = 3 },
      new() { Name = "Inter Global", InstitutionType = InstitutionType.Fintech, AccountType = AccountType.InvestmentAccount, Currency = Currency.USD, AccountIsActive = true, HasInvestments = true, DisplayOrder = 4 },
      new() { Name = "Binance", InstitutionType = InstitutionType.Exchange, AccountType = AccountType.InvestmentAccount, Currency = Currency.USD, AccountIsActive = true, HasInvestments = true, DisplayOrder = 5 },
      new() { Name = "C6 Bank", InstitutionType = InstitutionType.Fintech, AccountType = AccountType.CheckingAccount, Currency = Currency.BRL, AccountIsActive = true, HasCard = true, DisplayOrder = 6 },
    };

    foreach (AccountEntity account in accounts)
    {
      db.Accounts.Add(account);
      db.Entry(account).Property("OwnerUserId").CurrentValue = Owner;
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase accounts seeded: {Count}.", accounts.Length);
    return accounts.ToList();
  }

  private static async Task EnsureCreditCardsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<AccountEntity> accounts,
    CancellationToken ct)
  {
    List<CreditCardEntity> existing = await ReadOwnedAsync(db.CreditCards, ct);
    if (existing.Count >= 4)
    {
      return;
    }

    // Accounts are matched by role, never by display name: a renamed or localized showcase account
    // would silently skip the whole group otherwise.
    List<AccountEntity> cardAccounts = SelectCardAccounts(accounts);
    if (cardAccounts.Count < 2)
    {
      logger.LogWarning("Showcase credit cards skipped: no card-capable accounts found.");
      return;
    }

    AccountEntity nubank = cardAccounts[0];
    AccountEntity c6 = cardAccounts[1];

    logger.LogInformation("Seeding showcase credit cards...");
    CreditCardEntity[] cards =
    {
      new() { HolderName = "Demo User", CardType = CardType.Credit, Flag = CardFlag.MasterCard, HasLimit = true, LimitValue = 8000m, HasInvoice = true, InvoiceClosingDay = 3, InvoicePaymentDay = 10, AccountId = nubank.Id, Currency = Currency.BRL, DisplayOrder = 1 },
      new() { HolderName = "Demo User", CardType = CardType.Credit, Flag = CardFlag.Visa, HasLimit = true, LimitValue = 12000m, HasInvoice = true, InvoiceClosingDay = 15, InvoicePaymentDay = 22, AccountId = nubank.Id, Currency = Currency.BRL, DisplayOrder = 2 },
      new() { HolderName = "Demo User", CardType = CardType.Credit, Flag = CardFlag.Elo, HasLimit = true, LimitValue = 5000m, HasInvoice = true, InvoiceClosingDay = 10, InvoicePaymentDay = 17, AccountId = c6.Id, Currency = Currency.BRL, DisplayOrder = 1 },
      new() { HolderName = "Demo User", CardType = CardType.Debit, Flag = CardFlag.Visa, HasLimit = false, AccountId = c6.Id, Currency = Currency.BRL, DisplayOrder = 2 },
    };

    // Only the missing slots are added, so a partially wiped group is repaired without duplicates.
    foreach (CreditCardEntity card in cards)
    {
      bool alreadyPresent = existing.Exists(item =>
        item.AccountId == card.AccountId && item.CardType == card.CardType && item.Flag == card.Flag);
      if (alreadyPresent)
      {
        continue;
      }

      db.CreditCards.Add(card);
      db.Entry(card).Property("OwnerUserId").CurrentValue = Owner;
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase credit cards completed to {Count}.", cards.Length);
  }

  private static async Task<List<AllocationEntity>> EnsureAllocationsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<AccountEntity> accounts,
    CancellationToken ct)
  {
    List<AllocationEntity> existing = await ReadOwnedAsync(db.Allocations, ct);
    if (existing.Count > 0)
    {
      return existing;
    }

    List<AccountEntity> investmentAccounts = accounts
      .Where(item => item.AccountType == AccountType.InvestmentAccount)
      .OrderBy(item => item.DisplayOrder)
      .ToList();
    if (investmentAccounts.Count == 0)
    {
      logger.LogWarning("Showcase allocations skipped: no investment account found.");
      return existing;
    }

    AccountEntity xp = investmentAccounts[0];
    AccountEntity inter = investmentAccounts.Count > 1 ? investmentAccounts[1] : xp;
    AccountEntity binance = investmentAccounts.Count > 2 ? investmentAccounts[2] : inter;

    logger.LogInformation("Seeding showcase allocations...");
    AllocationEntity[] allocations =
    {
      new() { Name = "Tesouro Selic 2029", Initials = "SELIC29", AllocationType = AllocationType.DirectTreasure, BuyUnitValue = 14200m, Quantity = 2m, Currency = Currency.BRL, ProfitType = "CDI", ProfitPercentage = 100m, AccountId = xp.Id, PlanningPurpose = FinancialPurpose.EmergencyReserve },
      new() { Name = "CDB Banco Inter 120%", Initials = "CDB120", AllocationType = AllocationType.CDB, BuyUnitValue = 5000m, Quantity = 1m, Currency = Currency.BRL, ProfitType = "CDI", ProfitPercentage = 120m, AccountId = xp.Id, PlanningPurpose = FinancialPurpose.EmergencyReserve },
      new() { Name = "LCI 95% CDI", Initials = "LCI95", AllocationType = AllocationType.LCI, BuyUnitValue = 10000m, Quantity = 1m, Currency = Currency.BRL, ProfitType = "CDI", ProfitPercentage = 95m, AccountId = xp.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
      new() { Name = "PETR4", Initials = "PETR4", AllocationType = AllocationType.Stocks, BuyUnitValue = 32.50m, Quantity = 100m, Currency = Currency.BRL, AccountId = xp.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
      new() { Name = "HGLG11", Initials = "HGLG11", AllocationType = AllocationType.FII, BuyUnitValue = 160.00m, Quantity = 20m, Currency = Currency.BRL, AccountId = xp.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
      new() { Name = "SPY", Initials = "SPY", AllocationType = AllocationType.SPY, BuyUnitValue = 520.00m, Quantity = 3m, Currency = Currency.USD, AccountId = inter.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
      new() { Name = "Bitcoin", Initials = "BTC", AllocationType = AllocationType.BTC, BuyUnitValue = 62000m, Quantity = 0.05m, Currency = Currency.USD, AccountId = binance.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
      new() { Name = "Ethereum", Initials = "ETH", AllocationType = AllocationType.ETH, BuyUnitValue = 3400m, Quantity = 1.5m, Currency = Currency.USD, AccountId = binance.Id, PlanningPurpose = FinancialPurpose.LongTermInvestment },
    };

    foreach (AllocationEntity allocation in allocations)
    {
      db.Allocations.Add(allocation);
      db.Entry(allocation).Property("OwnerUserId").CurrentValue = Owner;
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase allocations seeded: {Count}.", allocations.Length);
    return allocations.ToList();
  }

  private static async Task EnsureAllocationHistoriesAsync(
    LifePlannerContext db,
    ILogger logger,
    List<AllocationEntity> allocations,
    DateTime today,
    CancellationToken ct)
  {
    bool exists = await db.AllocationHistories
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && !item.Deleted, ct);
    if (exists || allocations.Count == 0)
    {
      return;
    }

    logger.LogInformation("Seeding showcase allocation history...");
    List<AllocationHistoryEntity> history = new();

    foreach (AllocationEntity allocation in allocations)
    {
      decimal invested = allocation.BuyUnitValue * allocation.Quantity;
      decimal opening = Math.Round(invested * 0.6m, 2);
      decimal firstTopUp = Math.Round(invested * 0.25m, 2);
      decimal secondTopUp = Math.Round(invested - opening - firstTopUp, 2);

      history.Add(BuildHistory(allocation, AllocationHistoryKind.Created, today.AddMonths(-12), 0m, opening));
      history.Add(BuildHistory(allocation, AllocationHistoryKind.Increased, today.AddMonths(-7), opening, opening + firstTopUp));
      history.Add(BuildHistory(allocation, AllocationHistoryKind.Increased, today.AddMonths(-2), opening + firstTopUp, invested));
    }

    db.AllocationHistories.AddRange(history);
    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase allocation history seeded: {Count} movements.", history.Count);
  }

  /// <summary>Picks the accounts that can legitimately hold a card, ordered for stable assignment.</summary>
  /// <param name="accounts">Showcase accounts.</param>
  /// <returns>Card-capable accounts, falling back to checking accounts when no flag is set.</returns>
  private static List<AccountEntity> SelectCardAccounts(List<AccountEntity> accounts)
  {
    List<AccountEntity> flagged = accounts
      .Where(item => item.HasCard)
      .OrderBy(item => item.DisplayOrder)
      .ToList();
    if (flagged.Count >= 2)
    {
      return flagged;
    }

    return accounts
      .Where(item => item.AccountType == AccountType.CheckingAccount)
      .OrderBy(item => item.DisplayOrder)
      .ToList();
  }

  private static AllocationHistoryEntity BuildHistory(
    AllocationEntity allocation,
    AllocationHistoryKind kind,
    DateTime occurredAt,
    decimal previous,
    decimal current)
  {
    // USD positions are converted with an illustrative rate so the consolidated chart stays readable.
    decimal rate = allocation.Currency == Currency.USD ? 5.4m : 1m;
    return new AllocationHistoryEntity
    {
      OwnerUserId = Owner,
      AllocationId = allocation.Id,
      AccountId = allocation.AccountId,
      Kind = kind,
      PlanningPurpose = allocation.PlanningPurpose,
      OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
      PreviousNativeAmount = previous,
      NewNativeAmount = current,
      DeltaNativeAmount = current - previous,
      AmountBrl = Math.Round((current - previous) * rate, 2),
      Currency = allocation.Currency,
      AllocationName = allocation.Name,
    };
  }

  private static async Task EnsureTransactionsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<AccountEntity> accounts,
    List<CategoryEntity> categories,
    DateTime today,
    CancellationToken ct)
  {
    DateTime currentMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    DateTime windowStart = currentMonth.AddMonths(-TransactionHistoryMonths);

    // The series is extended, never rebuilt: months that already carry showcase entries are left
    // untouched, so a run that repeats — or overlaps another instance — cannot duplicate or destroy.
    List<TransactionEntity> existing = await ReadOwnedAsync(db.Transactions, ct);
    HashSet<DateTime> seededMonths = existing
      .Select(item => new DateTime(item.Date.Year, item.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
      .ToHashSet();
    if (seededMonths.Contains(currentMonth))
    {
      return;
    }

    logger.LogInformation("Extending showcase transactions up to {Month:yyyy-MM}.", currentMonth);

    List<AccountEntity> cashAccounts = accounts
      .Where(item => item.AccountType == AccountType.CheckingAccount)
      .OrderBy(item => item.DisplayOrder)
      .ToList();
    AccountEntity? nubank = cashAccounts.FirstOrDefault();
    AccountEntity? itau = cashAccounts.Skip(1).FirstOrDefault() ?? nubank;
    AccountEntity? xp = accounts
      .Where(item => item.AccountType == AccountType.InvestmentAccount)
      .OrderBy(item => item.DisplayOrder)
      .FirstOrDefault() ?? nubank;

    List<TransactionEntity> transactions = new();
    for (int monthOffset = TransactionHistoryMonths; monthOffset >= 0; monthOffset--)
    {
      DateTime month = currentMonth.AddMonths(-monthOffset);
      if (seededMonths.Contains(month))
      {
        continue;
      }

      // A small deterministic drift keeps the month-over-month charts from looking flat.
      decimal drift = 1m + (0.012m * (TransactionHistoryMonths - monthOffset));

      Add(month, 4, Category.Salary, "SALARY", 8500m * drift, "Monthly salary", true, nubank, TransactionPlanningImpact.Operating);
      Add(month, 12, Category.OthersIncome, "FREELANCE", 2200m * drift, "Freelance project payment", true, itau, TransactionPlanningImpact.Operating);
      Add(month, 3, Category.Home, "HOME", 1800m, "Monthly rent", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 2, Category.Light, "HOME", 180m, "Electricity bill", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 5, Category.Food, "FOOD", 450m, "Weekly groceries", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 18, Category.Food, "FOOD", 420m, "Weekly groceries", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 6, Category.Food, "FOOD", 85m, "Restaurant dinner", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 7, Category.Vehicles, "TRANSPORT", 250m, "Fuel and parking", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 8, Category.Health, "HEALTH", 320m, "Health insurance", false, itau, TransactionPlanningImpact.Operating);
      Add(month, 10, Category.Leisure, "LEISURE", 120m, "Streaming subscriptions", false, nubank, TransactionPlanningImpact.Operating);
      Add(month, 15, Category.Investiment, "INVEST", 1000m, "Monthly investment contribution", false, xp, TransactionPlanningImpact.Contribution);
    }

    // Entries dated after today would feed projections as if they had already happened.
    transactions.RemoveAll(item => item.Date > today || item.Date < windowStart);

    foreach (TransactionEntity transaction in transactions)
    {
      db.Transactions.Add(transaction);
      db.Entry(transaction).Property("OwnerUserId").CurrentValue = Owner;
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase transactions seeded: {Count}.", transactions.Count);

    void Add(
      DateTime month,
      int day,
      Category category,
      string categoryCode,
      decimal value,
      string description,
      bool income,
      AccountEntity? account,
      TransactionPlanningImpact impact)
    {
      transactions.Add(new TransactionEntity
      {
        Date = month.AddDays(day - 1),
        Category = category,
        CategoryId = categories.Find(item => item.Code == categoryCode)?.Id,
        Value = Math.Round(value, 2),
        Description = description,
        Income = income,
        AccountId = account?.Id,
        SourceKind = TransactionSourceKind.Manual,
        PlanningImpact = impact,
        PlanningClassificationSource = PlanningClassificationSource.DefaultRule,
        PlanningClassificationConfirmed = true,
      });
    }
  }

  /// <summary>
  /// Rebuilds the showcase planning groups when they predate the current showcase content version.
  /// </summary>
  /// <remarks>
  /// Every group below is created only when missing, which means text written by an older revision is
  /// never rewritten. The showcase tenant carried Portuguese habit, risk, OKR and review content long
  /// after the seeder itself moved to English. Deleting those rows is safe because nothing here is a
  /// real record: the Ensure groups that run straight after recreate all of it from current code.
  /// </remarks>
  /// <param name="db">Database context scoped to the caller.</param>
  /// <param name="logger">Logger used to report the rebuild.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>A task representing the rebuild.</returns>
  private static async Task ResetOutdatedShowcaseAsync(LifePlannerContext db, ILogger logger, CancellationToken ct)
  {
    AppSetting? marker = await db.AppSettings
      .FirstOrDefaultAsync(item => item.Key == ShowcaseVersionKey, ct);
    int storedVersion = int.TryParse(marker?.Value, out int parsed) ? parsed : 0;
    if (storedVersion >= ShowcaseContentVersion)
    {
      return;
    }

    logger.LogInformation(
      "Rebuilding showcase planning content: stored version {Stored} is behind {Current}.",
      storedVersion,
      ShowcaseContentVersion);

    // Children first: these tables have no owner column of their own, so they are reached through
    // their parent. ExecuteDeleteAsync is used throughout because these rows must disappear outright
    // rather than be flagged as soft-deleted.
    await db.ReviewRunItems
      .IgnoreQueryFilters()
      .Where(item => db.ReviewRuns
        .IgnoreQueryFilters()
        .Any(run => run.Id == item.ReviewRunId && run.OwnerUserId == Owner))
      .ExecuteDeleteAsync(ct);

    await db.ReviewTemplateItems
      .IgnoreQueryFilters()
      .Where(item => db.ReviewTemplates
        .IgnoreQueryFilters()
        .Any(template => template.Id == item.TemplateId && template.OwnerUserId == Owner))
      .ExecuteDeleteAsync(ct);

    await db.HabitLogs.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.Habits.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.ReviewRuns.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.ReviewTemplates.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.QuarterTasks.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.QuarterPlans.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.Risks.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.KeyResults.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.Objectives.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.Pillars.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.RoutineBlocks.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.RoutineTemplates.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.WeeklyFocuses.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.PlanningReviews.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);
    await db.AnnualPlans.IgnoreQueryFilters().Where(item => item.OwnerUserId == Owner).ExecuteDeleteAsync(ct);

    if (marker == null)
    {
      db.AppSettings.Add(new AppSetting
      {
        Key = ShowcaseVersionKey,
        Value = ShowcaseContentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
      });
    }
    else
    {
      marker.Value = ShowcaseContentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase planning content rebuilt at version {Version}.", ShowcaseContentVersion);
  }
}
