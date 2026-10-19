//-----------------------------------------------------------------------
// <copyright file="LifePlannerContext.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data.Auth;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Represents the Entity Framework Core database context for the LifePlanner application, providing access to
/// application entities and configuring database behavior.
/// </summary>
/// <remarks>This context manages the application's data model, including entities such as accounts, allocations,
/// Transactions, categories, credit cards, and application settings. It configures entity relationships, value
/// conversions, encryption for sensitive data, and compatibility with Postgres. The context is typically
/// registered and managed by dependency injection in the application startup. Thread safety is not guaranteed; use a
/// separate instance per operation or request as recommended by Entity Framework Core.</remarks>
public class LifePlannerContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
	/// <summary>Sentinel value used as OwnerUserId for unauthenticated (demo) sessions.</summary>
	public const string DemoUserId = "__DEMO__";

	private const string OwnerUserIdShadowProperty = "OwnerUserId";

  /// <summary>Ambient flag authorizing writes to the demonstration tenant; see <see cref="AllowDemoWrites"/>.</summary>
  private static readonly AsyncLocal<bool> demoWriteScope = new AsyncLocal<bool>();

	private readonly string? effectiveUserId;

	/// <summary>
	/// Initializes a new instance of the LifePlannerContext class with Identity and multi-tenant support.
	/// </summary>
	/// <param name="options">The options to be used by the DbContext.</param>
	/// <param name="httpContextAccessor">Accessor for the current HTTP context; null HttpContext disables tenant filtering.</param>
	public LifePlannerContext(DbContextOptions<LifePlannerContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
	{
		HttpContext? httpContext = httpContextAccessor?.HttpContext;
		if (httpContext is null)
		{
			effectiveUserId = null;
		}
		else if (httpContext.User.Identity?.IsAuthenticated == true)
		{
			effectiveUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		}
		else
		{
			effectiveUserId = DemoUserId;
		}
	}

	/// <summary>
	/// Static encryption service set during app startup. Used by ValueConverter for encryption at database level.
	/// </summary>
	public static IEncryptionService? EncryptionService { get; set; }

	/// <summary>
	/// Gets or sets the collection of account entities in the database context.
	/// </summary>
	public DbSet<AccountEntity> Accounts { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of allocation entities for the database context.
	/// </summary>
	public DbSet<AllocationEntity> Allocations { get; set; } = null!;

  /// <summary>
  /// Gets or sets the collection of allocation invested-capital history records.
  /// </summary>
  public DbSet<AllocationHistoryEntity> AllocationHistories { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of Transaction entities in the database context.
	/// </summary>
	public DbSet<TransactionEntity> Transactions { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of category entities in the database context.
	/// </summary>
	public DbSet<CategoryEntity> Categories { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of credit card entities in the database context.
	/// </summary>
	public DbSet<CreditCardEntity> CreditCards { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of application settings stored in the database.
	/// </summary>
	public DbSet<AppSetting> AppSettings { get; set; } = null!;

	/// <summary>
	/// Gets or sets the collection of market quote history records.
	/// </summary>
	public DbSet<MarketQuoteEntity> MarketQuotes { get; set; } = null!;

	// ── Planning DbSets ──────────────────────────────────────────────────────

	/// <summary>Gets or sets the collection of OKR pillar records.</summary>
	public DbSet<PillarEntity> Pillars { get; set; } = null!;

	/// <summary>Gets or sets the collection of OKR objective records.</summary>
	public DbSet<ObjectiveEntity> Objectives { get; set; } = null!;

	/// <summary>Gets or sets the collection of OKR key result records.</summary>
	public DbSet<KeyResultEntity> KeyResults { get; set; } = null!;

	/// <summary>Gets or sets the collection of quarterly plan records.</summary>
	public DbSet<QuarterPlanEntity> QuarterPlans { get; set; } = null!;

	/// <summary>Gets or sets the collection of quarterly task checklist records.</summary>
	public DbSet<QuarterTaskEntity> QuarterTasks { get; set; } = null!;

	/// <summary>Gets or sets the collection of review template records.</summary>
	public DbSet<ReviewTemplateEntity> ReviewTemplates { get; set; } = null!;

	/// <summary>Gets or sets the collection of review template item records.</summary>
	public DbSet<ReviewTemplateItemEntity> ReviewTemplateItems { get; set; } = null!;

	/// <summary>Gets or sets the collection of review run records.</summary>
	public DbSet<ReviewRunEntity> ReviewRuns { get; set; } = null!;

	/// <summary>Gets or sets the collection of review run item records.</summary>
	public DbSet<ReviewRunItemEntity> ReviewRunItems { get; set; } = null!;

	/// <summary>Gets or sets the collection of pre-mortem risk records.</summary>
	public DbSet<RiskEntity> Risks { get; set; } = null!;

	/// <summary>Gets or sets the collection of habit records.</summary>
	public DbSet<HabitEntity> Habits { get; set; } = null!;

	/// <summary>Gets or sets the collection of daily habit log records.</summary>
	public DbSet<HabitLogEntity> HabitLogs { get; set; } = null!;

  /// <summary>Gets or sets annual planning decisions and assumptions.</summary>
  public DbSet<AnnualPlanEntity> AnnualPlans { get; set; } = null!;

  /// <summary>Gets or sets weekly routine templates.</summary>
  public DbSet<RoutineTemplateEntity> RoutineTemplates { get; set; } = null!;

  /// <summary>Gets or sets weekly routine blocks.</summary>
  public DbSet<RoutineBlockEntity> RoutineBlocks { get; set; } = null!;

  /// <summary>Gets or sets weekly planning focuses.</summary>
  public DbSet<WeeklyFocusEntity> WeeklyFocuses { get; set; } = null!;

  /// <summary>Gets or sets aggregate financial history snapshots.</summary>
  public DbSet<FinancialSnapshotEntity> FinancialSnapshots { get; set; } = null!;

  /// <summary>Gets or sets compact weekly and monthly reviews.</summary>
  public DbSet<PlanningReviewEntity> PlanningReviews { get; set; } = null!;

  /// <summary>Gets or sets generated operational planning actions.</summary>
  public DbSet<PlanningActionEntity> PlanningActions { get; set; } = null!;

  /// <summary>Gets or sets deterministic planning action generator runs.</summary>
  public DbSet<PlanningActionRunEntity> PlanningActionRuns { get; set; } = null!;

  /// <summary>Gets or sets personal vehicle records.</summary>
  public DbSet<VehicleEntity> Vehicles { get; set; } = null!;

  /// <summary>Gets or sets unified vehicle history entries.</summary>
  public DbSet<VehicleRecordEntity> VehicleRecords { get; set; } = null!;

  /// <summary>Gets or sets vehicle reminders.</summary>
  public DbSet<VehicleReminderEntity> VehicleReminders { get; set; } = null!;

	// ── IMAP Email Sync DbSets ───────────────────────────────────────────────

	/// <summary>Gets or sets the per-user IMAP credential records used for bank email monitoring.</summary>
	public DbSet<ImapSettingEntity> ImapSettings { get; set; } = null!;

	/// <summary>Gets or sets the mappings from email sender identifiers to financial accounts.</summary>
	public DbSet<ImapAccountMappingEntity> ImapAccountMappings { get; set; } = null!;

	// ── AI Provider DbSets ───────────────────────────────────────────────────

	/// <summary>Gets or sets the per-user AI provider configuration records.</summary>
	public DbSet<AiProviderSettingEntity> AiProviderSettings { get; set; } = null!;

  // ── Data Protection DbSets ───────────────────────────────────────────────

  /// <summary>Gets or sets the ASP.NET Core Data Protection keys persisted for this application, so the key
  /// ring survives process restarts and redeploys instead of being regenerated in memory.</summary>
  public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

  // ── Access Analytics DbSets ──────────────────────────────────────────────

  /// <summary>Gets or sets bounded-retention access analytics events (HTTP requests, page views, feature usage).</summary>
  public DbSet<AccessEventEntity> AccessEvents { get; set; } = null!;

  /// <summary>Gets or sets permanent per-day access analytics rollups used for long-range trend charts.</summary>
  public DbSet<AccessDailySummaryEntity> AccessDailySummaries { get; set; } = null!;

	/// <summary>
	/// Configures the entity model for the context using the specified model builder. This method is called when the model
	/// for a derived context has been initialized, but before the model has been locked down and used to initialize the
	/// context.
	/// </summary>
	/// <remarks>Override this method to customize the model by configuring entity properties, relationships, table
	/// mappings, value converters, query filters, and indexes. Call the base implementation to ensure that any
	/// configuration in the base class is applied. This method is typically called by the Entity Framework infrastructure
	/// and is not intended to be called directly from application code.</remarks>
	/// <param name="modelBuilder">The builder used to construct the model for the context. Provides a fluent API for configuring entity types,
	/// relationships, and database mappings.</param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		ConfigureAppSettings(modelBuilder);
		ConfigureIdsAndTables(modelBuilder);
		ConfigureOwnerShadowProperties(modelBuilder);
		ConfigureQueryFilters(modelBuilder);
		ConfigureRelationships(modelBuilder);
		ConfigurePropertyTypes(modelBuilder);
		ConfigureJsonConverters(modelBuilder);
		ConfigureIndexes(modelBuilder);

		// Sensitive data encryption via ValueConverter
		ConfigureEncryption(modelBuilder);

		// Postgres compatibility type converters for Guid/DateTime stored as TEXT
		ConfigurePostgresTypeConverters(modelBuilder);
	}

	private static void ConfigureAppSettings(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AppSetting>().ToTable("AppSettings");
		modelBuilder.Entity<AppSetting>().HasKey(s => s.Key);
		modelBuilder.Entity<AppSetting>().Property(s => s.Key).HasMaxLength(256);
	}

	private static void ConfigureIdsAndTables(ModelBuilder modelBuilder)
	{
		// Table mappings
		modelBuilder.Entity<AccountEntity>().ToTable("Accounts");
		modelBuilder.Entity<AllocationEntity>().ToTable("Allocations");
    modelBuilder.Entity<AllocationHistoryEntity>().ToTable("AllocationHistories");
		modelBuilder.Entity<TransactionEntity>().ToTable("Transactions");
		modelBuilder.Entity<CategoryEntity>().ToTable("Categories");
		modelBuilder.Entity<CreditCardEntity>().ToTable("CreditCards");
		modelBuilder.Entity<MarketQuoteEntity>().ToTable("MarketQuotes");

		// Planning tables
		modelBuilder.Entity<PillarEntity>().ToTable("Pillars");
		modelBuilder.Entity<ObjectiveEntity>().ToTable("Objectives");
		modelBuilder.Entity<KeyResultEntity>().ToTable("KeyResults");
		modelBuilder.Entity<QuarterPlanEntity>().ToTable("QuarterPlans");
		modelBuilder.Entity<QuarterTaskEntity>().ToTable("QuarterTasks");
		modelBuilder.Entity<ReviewTemplateEntity>().ToTable("ReviewTemplates");
		modelBuilder.Entity<ReviewTemplateItemEntity>().ToTable("ReviewTemplateItems");
		modelBuilder.Entity<ReviewRunEntity>().ToTable("ReviewRuns");
		modelBuilder.Entity<ReviewRunItemEntity>().ToTable("ReviewRunItems");
		modelBuilder.Entity<RiskEntity>().ToTable("Risks");
		modelBuilder.Entity<HabitEntity>().ToTable("Habits");
		modelBuilder.Entity<HabitLogEntity>().ToTable("HabitLogs");
    modelBuilder.Entity<AnnualPlanEntity>().ToTable("AnnualPlans");
    modelBuilder.Entity<RoutineTemplateEntity>().ToTable("RoutineTemplates");
    modelBuilder.Entity<RoutineBlockEntity>().ToTable("RoutineBlocks");
    modelBuilder.Entity<WeeklyFocusEntity>().ToTable("WeeklyFocuses");
    modelBuilder.Entity<FinancialSnapshotEntity>().ToTable("FinancialSnapshots");
    modelBuilder.Entity<PlanningReviewEntity>().ToTable("PlanningReviews");
    modelBuilder.Entity<PlanningActionEntity>().ToTable("PlanningActions");
    modelBuilder.Entity<PlanningActionRunEntity>().ToTable("PlanningActionRuns");
    modelBuilder.Entity<VehicleEntity>().ToTable("Vehicles");
    modelBuilder.Entity<VehicleRecordEntity>().ToTable("VehicleRecords");
    modelBuilder.Entity<VehicleReminderEntity>().ToTable("VehicleReminders");

		// IMAP Email Sync tables
		modelBuilder.Entity<ImapSettingEntity>().ToTable("ImapSettings");
		modelBuilder.Entity<ImapAccountMappingEntity>().ToTable("ImapAccountMappings");

		// AI Provider tables
		modelBuilder.Entity<AiProviderSettingEntity>().ToTable("AiProviderSettings");
	}

	private static void ConfigureOwnerShadowProperties(ModelBuilder modelBuilder)
	{
		Type[] ownedTypes = { typeof(AccountEntity), typeof(AllocationEntity), typeof(TransactionEntity), typeof(CreditCardEntity) };
		foreach (Type entityType in ownedTypes)
		{
			modelBuilder.Entity(entityType).Property<string>(OwnerUserIdShadowProperty).HasMaxLength(450).IsRequired(false);
			modelBuilder.Entity(entityType).HasIndex(OwnerUserIdShadowProperty);
		}
	}

	private void ConfigureQueryFilters(ModelBuilder modelBuilder)
	{
		// Soft delete + ownership query filters for tenant-scoped entities
		modelBuilder.Entity<AccountEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || EF.Property<string>(x, OwnerUserIdShadowProperty) == effectiveUserId));
		modelBuilder.Entity<AllocationEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || EF.Property<string>(x, OwnerUserIdShadowProperty) == effectiveUserId));
		modelBuilder.Entity<TransactionEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || EF.Property<string>(x, OwnerUserIdShadowProperty) == effectiveUserId));
		modelBuilder.Entity<CreditCardEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || EF.Property<string>(x, OwnerUserIdShadowProperty) == effectiveUserId));

		// Category: soft delete only (shared across users)
		modelBuilder.Entity<CategoryEntity>().HasQueryFilter(x => !x.Deleted);

		// IMAP Email Sync: ownership filter with soft-delete
		modelBuilder.Entity<ImapSettingEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
		modelBuilder.Entity<ImapAccountMappingEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));

		// AI Provider: ownership filter with soft-delete
		modelBuilder.Entity<AiProviderSettingEntity>().HasQueryFilter(x =>
		!x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<AnnualPlanEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<RoutineTemplateEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<RoutineBlockEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<WeeklyFocusEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<FinancialSnapshotEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<AllocationHistoryEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<PlanningReviewEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<PlanningActionEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<PlanningActionRunEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<VehicleEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<VehicleRecordEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<VehicleReminderEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));

    // OKR, quarter, review, risk and habit trees: these were previously filtered only by the calling
    // services. The filter is repeated here so a single forgotten Where() cannot leak across tenants.
    modelBuilder.Entity<PillarEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<ObjectiveEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<KeyResultEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<QuarterPlanEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<QuarterTaskEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<ReviewTemplateEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<ReviewRunEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<RiskEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<HabitEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
    modelBuilder.Entity<HabitLogEntity>().HasQueryFilter(x =>
    !x.Deleted && (effectiveUserId == null || x.OwnerUserId == effectiveUserId));
	}

	private static void ConfigureRelationships(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AllocationEntity>()
		.HasOne(a => a.Account)
		.WithMany(acc => acc.Allocations)
		.HasForeignKey(a => a.AccountId)
		.OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<AllocationHistoryEntity>()
    .HasOne(a => a.Allocation)
    .WithMany()
    .HasForeignKey(a => a.AllocationId)
    .OnDelete(DeleteBehavior.Restrict);

		modelBuilder.Entity<CreditCardEntity>()
		.HasOne(c => c.Account)
		.WithMany(acc => acc.Cards)
		.HasForeignKey(c => c.AccountId)
		.OnDelete(DeleteBehavior.Restrict);

		// Transaction -> Category relationship (optional FK for backward compatibility)
		modelBuilder.Entity<TransactionEntity>()
		.HasOne(a => a.CategoryEntity)
		.WithMany()
		.HasForeignKey(a => a.CategoryId)
		.OnDelete(DeleteBehavior.Restrict)
		.IsRequired(false);

		// Transaction -> Account relationship (optional FK for balance sync)
		modelBuilder.Entity<TransactionEntity>()
		.HasOne(t => t.Account)
		.WithMany()
		.HasForeignKey(t => t.AccountId)
		.OnDelete(DeleteBehavior.SetNull)
		.IsRequired(false);

    modelBuilder.Entity<TransactionEntity>()
    .HasOne<CreditCardEntity>()
    .WithMany()
    .HasForeignKey(t => t.CreditCardId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

		// OKR relationships
		modelBuilder.Entity<ObjectiveEntity>()
		.HasOne(o => o.Pillar)
		.WithMany(p => p.Objectives)
		.HasForeignKey(o => o.PillarId)
		.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<KeyResultEntity>()
		.HasOne(kr => kr.Objective)
		.WithMany(o => o.KeyResults)
		.HasForeignKey(kr => kr.ObjectiveId)
		.OnDelete(DeleteBehavior.Cascade);

		// Quarter plan relationships
		modelBuilder.Entity<QuarterTaskEntity>()
		.HasOne(t => t.QuarterPlan)
		.WithMany(q => q.Tasks)
		.HasForeignKey(t => t.QuarterPlanId)
		.OnDelete(DeleteBehavior.Cascade);

		// Review relationships
		modelBuilder.Entity<ReviewTemplateItemEntity>()
		.HasOne(i => i.Template)
		.WithMany(t => t.Items)
		.HasForeignKey(i => i.TemplateId)
		.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<ReviewRunEntity>()
		.HasOne(r => r.Template)
		.WithMany()
		.HasForeignKey(r => r.TemplateId)
		.OnDelete(DeleteBehavior.Restrict);

		modelBuilder.Entity<ReviewRunItemEntity>()
		.HasOne(ri => ri.ReviewRun)
		.WithMany(r => r.Items)
		.HasForeignKey(ri => ri.ReviewRunId)
		.OnDelete(DeleteBehavior.Cascade);

		// Risk -> Pillar (optional)
		modelBuilder.Entity<RiskEntity>()
		.HasOne(r => r.Pillar)
		.WithMany()
		.HasForeignKey(r => r.PillarId)
		.OnDelete(DeleteBehavior.SetNull)
		.IsRequired(false);

		// Habit -> Pillar (optional)
		modelBuilder.Entity<HabitEntity>()
		.HasOne(h => h.Pillar)
		.WithMany()
		.HasForeignKey(h => h.PillarId)
		.OnDelete(DeleteBehavior.SetNull)
		.IsRequired(false);

		// HabitLog -> Habit
		modelBuilder.Entity<HabitLogEntity>()
		.HasOne(hl => hl.Habit)
		.WithMany(h => h.Logs)
		.HasForeignKey(hl => hl.HabitId)
		.OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<RoutineBlockEntity>()
    .HasOne(b => b.RoutineTemplate)
    .WithMany(t => t.Blocks)
    .HasForeignKey(b => b.RoutineTemplateId)
    .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<VehicleRecordEntity>()
    .HasOne(r => r.Vehicle)
    .WithMany(v => v.Records)
    .HasForeignKey(r => r.VehicleId)
    .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<VehicleReminderEntity>()
    .HasOne(r => r.Vehicle)
    .WithMany(v => v.Reminders)
    .HasForeignKey(r => r.VehicleId)
    .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<VehicleReminderEntity>()
    .HasOne(r => r.CompletedByRecord)
    .WithMany()
    .HasForeignKey(r => r.CompletedByRecordId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

    modelBuilder.Entity<ObjectiveEntity>()
    .HasOne<AnnualPlanEntity>()
    .WithMany()
    .HasForeignKey(o => o.AnnualPlanId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

    modelBuilder.Entity<QuarterPlanEntity>()
    .HasOne<AnnualPlanEntity>()
    .WithMany()
    .HasForeignKey(q => q.AnnualPlanId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

    modelBuilder.Entity<QuarterPlanEntity>()
    .HasOne<ObjectiveEntity>()
    .WithMany()
    .HasForeignKey(q => q.ObjectiveId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

    modelBuilder.Entity<PlanningReviewEntity>()
    .HasOne<FinancialSnapshotEntity>()
    .WithMany()
    .HasForeignKey(r => r.FinancialSnapshotId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

		// ImapAccountMapping -> Account (restrict: mapping must be removed before account can be deleted)
		modelBuilder.Entity<ImapAccountMappingEntity>()
		.HasOne(m => m.Account)
		.WithMany()
		.HasForeignKey(m => m.AccountId)
		.OnDelete(DeleteBehavior.Restrict)
		.IsRequired(true);
	}

	private static void ConfigurePropertyTypes(ModelBuilder modelBuilder)
	{
		// Decimal and text column type overrides for Postgres
		modelBuilder.Entity<AccountEntity>().Property(a => a.FeesOrChargesValue).HasColumnType("decimal(18,2)");

		modelBuilder.Entity<AllocationEntity>().Property(a => a.BuyUnitValue).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.AllocationType).HasColumnType("INTEGER");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.Quantity).HasColumnType("decimal(18,8)");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.ProfitPercentage).HasColumnType("TEXT");

		modelBuilder.Entity<TransactionEntity>().Property(a => a.Value).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.PreviousNativeAmount).HasColumnType("decimal(18,8)");
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.NewNativeAmount).HasColumnType("decimal(18,8)");
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.DeltaNativeAmount).HasColumnType("decimal(18,8)");
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.AmountBrl).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AllocationHistoryEntity>().Property(a => a.AllocationName).HasMaxLength(240);
		modelBuilder.Entity<AccountEntity>().Property(a => a.Balance).HasColumnType("decimal(18,8)");
		modelBuilder.Entity<AccountEntity>().Property(a => a.TotalInvested).HasColumnType("decimal(18,8)");

		modelBuilder.Entity<CreditCardEntity>().Property(c => c.LimitValue).HasColumnType("TEXT");
		modelBuilder.Entity<CreditCardEntity>().Property(c => c.AnnualFee).HasColumnType("decimal(18,2)");

		// MarketQuoteEntity property types
		modelBuilder.Entity<MarketQuoteEntity>().Property(m => m.Price).HasColumnType("decimal(18,8)");
		modelBuilder.Entity<MarketQuoteEntity>().Property(m => m.Source).HasMaxLength(64);
		modelBuilder.Entity<MarketQuoteEntity>().Property(m => m.Error).HasMaxLength(2000);
		modelBuilder.Entity<MarketQuoteEntity>().Property(m => m.Warning).HasMaxLength(2000);
		modelBuilder.Entity<MarketQuoteEntity>().Property(m => m.Symbol).HasMaxLength(32);

		// Planning entity OwnerUserId lengths
		modelBuilder.Entity<PillarEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<ObjectiveEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<QuarterPlanEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<QuarterTaskEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<ReviewTemplateEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<ReviewRunEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<RiskEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<HabitEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<HabitEntity>().Property(e => e.FrequencyDays).HasMaxLength(20).IsRequired(false);
		modelBuilder.Entity<HabitLogEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<RoutineTemplateEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<RoutineBlockEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<WeeklyFocusEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<PlanningReviewEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.SourceCode).HasMaxLength(120).IsRequired();
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.IconCss).HasMaxLength(80);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.TitleKey).HasMaxLength(200).IsRequired();
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.DetailKey).HasMaxLength(500);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.EvidenceText).HasMaxLength(500);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.DestinationHref).HasMaxLength(300);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.PrimaryActionKey).HasMaxLength(120);
    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.ErrorMessage).HasMaxLength(1000);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<VehicleEntity>().Property(e => e.Name).HasMaxLength(120).IsRequired();
    modelBuilder.Entity<VehicleEntity>().Property(e => e.Brand).HasMaxLength(80);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.Model).HasMaxLength(120);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.Plate).HasMaxLength(40);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.OdometerUnit).HasMaxLength(12).IsRequired();
    modelBuilder.Entity<VehicleEntity>().Property(e => e.IconCss).HasMaxLength(80).IsRequired();
    modelBuilder.Entity<VehicleEntity>().Property(e => e.ColorHex).HasMaxLength(16).IsRequired();
    modelBuilder.Entity<VehicleEntity>().Property(e => e.ImageDataUrl).HasColumnType("TEXT");
    modelBuilder.Entity<VehicleEntity>().Property(e => e.Notes).HasMaxLength(1000);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.CurrentOdometer).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Odometer).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Title).HasMaxLength(160).IsRequired();
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Category).HasMaxLength(80);
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Quantity).HasColumnType("decimal(18,3)");
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.ServiceItems).HasMaxLength(1000);
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Location).HasMaxLength(160);
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.Notes).HasMaxLength(1000);
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.Title).HasMaxLength(160).IsRequired();
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.Category).HasMaxLength(80);
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.DueOdometer).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.Notes).HasMaxLength(1000);
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.TargetNetWorth).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.PlannedMonthlyIncome).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.MonthlySpendingCeiling).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.PlannedAnnualExpenses).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.PlannedMonthlyContribution).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.PlannedOneOffNetFlows).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.NetWorth).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.ConfirmedValue).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.EstimatedValue).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.OperatingCash).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.EmergencyReserve).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.IdleCash).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.LongTermInvestments).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.MonthlyOperatingIncome).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.MonthlyOperatingExpense).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.MonthlyContributions).HasColumnType("decimal(18,2)");
    modelBuilder.Entity<AccountEntity>().Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");

		// Decimal precision for KR values
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.Baseline).HasColumnType("decimal(18,4)");
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.Target).HasColumnType("decimal(18,4)");
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.Current).HasColumnType("decimal(18,4)");

		// HabitLog unique constraint: one entry per (HabitId, LogDate)
		modelBuilder.Entity<HabitLogEntity>()
		.HasIndex(hl => new { hl.HabitId, hl.LogDate })
		.IsUnique();

		// AI Provider property types
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.OwnerUserId).HasMaxLength(450).IsRequired();
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.DisplayName).HasMaxLength(200);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.Model).HasMaxLength(200);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.PrivacyMode).HasMaxLength(100);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.LastTestMessage).HasMaxLength(1000);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.Temperature).HasColumnType("decimal(4,2)");
	}

	private static void ConfigureJsonConverters(ModelBuilder modelBuilder)
	{
		// CreditCard TransactionCurrencies JSON storage
		ValueConverter<List<Currency>, string> currencyListConverter = new ValueConverter<List<Currency>, string>(
		v => JsonSerializer.Serialize(v ?? new List<Currency>(), (JsonSerializerOptions?)null),
		v => string.IsNullOrWhiteSpace(v)
		? new List<Currency>()
		: JsonSerializer.Deserialize<List<Currency>>(v, (JsonSerializerOptions?)null)
		?? new List<Currency>());

		ValueComparer<List<Currency>> currencyListComparer = new ValueComparer<List<Currency>>(
		(c1, c2) => (c1 ?? new List<Currency>()).SequenceEqual(c2 ?? new List<Currency>()),
		c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
		c => c.ToList());

		modelBuilder.Entity<CreditCardEntity>().Property(c => c.TransactionCurrencies).HasConversion(currencyListConverter)
		.Metadata
		.SetValueComparer(currencyListComparer);

		// Account SupportedCurrencies JSON storage (reuse same converter/comparer)
		modelBuilder.Entity<AccountEntity>().Property(a => a.SupportedCurrencies).HasConversion(currencyListConverter)
		.Metadata
		.SetValueComparer(currencyListComparer);
	}

	private static void ConfigureIndexes(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AccountEntity>().HasIndex(a => a.Name);

		modelBuilder.Entity<TransactionEntity>().HasIndex(a => a.Date);
		modelBuilder.Entity<TransactionEntity>().HasIndex(a => a.Category);
		modelBuilder.Entity<TransactionEntity>().HasIndex(a => a.CategoryId);
		modelBuilder.Entity<TransactionEntity>().HasIndex(a => a.AccountId);

		modelBuilder.Entity<CategoryEntity>().HasIndex(c => c.Code).IsUnique();
		modelBuilder.Entity<CategoryEntity>().HasIndex(c => c.Name);

		// MarketQuoteEntity indexes — composite key is (AllocationType, Symbol)
		modelBuilder.Entity<MarketQuoteEntity>()
		.HasIndex(m => new { m.AllocationType, m.Symbol, m.RetrievedAtUtc })
		.IsDescending(false, false, true);
		modelBuilder.Entity<MarketQuoteEntity>()
		.HasIndex(m => new { m.Status, m.RetrievedAtUtc })
		.IsDescending(false, true);

    // AccessEventEntity indexes — pruning/rollup scan by kind + timestamp, unique summary per day
    modelBuilder.Entity<AccessEventEntity>()
    .HasIndex(e => new { e.Kind, e.CreationDate });
    modelBuilder.Entity<AccessDailySummaryEntity>()
    .HasIndex(s => s.SummaryDate)
    .IsUnique();

		// AI Provider index
		modelBuilder.Entity<AiProviderSettingEntity>().HasIndex(e => e.OwnerUserId);

		// Planning indexes
		modelBuilder.Entity<PillarEntity>().HasIndex(p => p.OwnerUserId);
		modelBuilder.Entity<ObjectiveEntity>().HasIndex(o => new { o.OwnerUserId, o.PillarId });
		modelBuilder.Entity<KeyResultEntity>().HasIndex(kr => new { kr.OwnerUserId, kr.ObjectiveId });
		modelBuilder.Entity<QuarterPlanEntity>().HasIndex(q => new { q.OwnerUserId, q.Year, q.Quarter });
		modelBuilder.Entity<QuarterTaskEntity>().HasIndex(t => t.QuarterPlanId);
		modelBuilder.Entity<ReviewTemplateEntity>().HasIndex(t => new { t.OwnerUserId, t.ReviewType });
		modelBuilder.Entity<ReviewRunEntity>().HasIndex(r => new { r.OwnerUserId, r.RunDate });
		modelBuilder.Entity<ReviewRunEntity>().HasIndex(r => new { r.OwnerUserId, r.ReviewType });
		modelBuilder.Entity<RiskEntity>().HasIndex(r => r.OwnerUserId);
		modelBuilder.Entity<HabitEntity>().HasIndex(h => h.OwnerUserId);
		modelBuilder.Entity<HabitLogEntity>().HasIndex(hl => new { hl.OwnerUserId, hl.LogDate });
    modelBuilder.Entity<AnnualPlanEntity>().HasIndex(p => new { p.OwnerUserId, p.Year }).IsUnique();
    modelBuilder.Entity<RoutineTemplateEntity>().HasIndex(t => new { t.OwnerUserId, t.IsActiveTemplate });
    modelBuilder.Entity<RoutineBlockEntity>().HasIndex(b => new { b.OwnerUserId, b.DayOfWeek, b.StartTime });
    modelBuilder.Entity<WeeklyFocusEntity>().HasIndex(f => new { f.OwnerUserId, f.WeekStartDate, f.SortOrder }).IsUnique();
    modelBuilder.Entity<FinancialSnapshotEntity>().HasIndex(s => new { s.OwnerUserId, s.SnapshotDate }).IsUnique();
    modelBuilder.Entity<AllocationHistoryEntity>().HasIndex(a => new { a.OwnerUserId, a.OccurredAt });
    modelBuilder.Entity<AllocationHistoryEntity>().HasIndex(a => a.AllocationId);
    modelBuilder.Entity<PlanningReviewEntity>().HasIndex(r => new { r.OwnerUserId, r.ReviewType, r.ReviewDate });
    modelBuilder.Entity<PlanningActionEntity>().HasIndex(a => new { a.OwnerUserId, a.Status, a.Priority });
    modelBuilder.Entity<PlanningActionEntity>().HasIndex(a => new { a.OwnerUserId, a.SourceCode, a.ReferenceDate });
    modelBuilder.Entity<PlanningActionRunEntity>().HasIndex(r => new { r.OwnerUserId, r.ReferenceDate, r.Manual });
    modelBuilder.Entity<VehicleEntity>().HasIndex(v => new { v.OwnerUserId, v.Active });
    modelBuilder.Entity<VehicleRecordEntity>().HasIndex(r => new { r.OwnerUserId, r.VehicleId, r.RecordDate });
    modelBuilder.Entity<VehicleRecordEntity>().HasIndex(r => new { r.OwnerUserId, r.VehicleId, r.RecordType });
    modelBuilder.Entity<VehicleReminderEntity>().HasIndex(r => new { r.OwnerUserId, r.VehicleId, r.Status });
    modelBuilder.Entity<VehicleReminderEntity>().HasIndex(r => new { r.OwnerUserId, r.DueDate });
    modelBuilder.Entity<VehicleReminderEntity>().HasIndex(r => new { r.OwnerUserId, r.DueOdometer });
	}

	/// <summary>
	/// Configures type converters for Guid and DateTime for Postgres compatibility.
	/// </summary>
	private static void ConfigurePostgresTypeConverters(ModelBuilder modelBuilder)
	{
		// Apply to all entities inheriting BaseEntity
		modelBuilder.Entity<AccountEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<AccountEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<AllocationEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<AllocationEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<AllocationEntity>().Property(e => e.ProfitDate).HasConversion(Helper.nullableDateTimeConverter);
		modelBuilder.Entity<AllocationEntity>().Property(e => e.ExpirationDate).HasConversion(Helper.nullableDateTimeConverter);

    modelBuilder.Entity<AllocationHistoryEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<AllocationHistoryEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<AllocationHistoryEntity>().Property(e => e.OccurredAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<TransactionEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<TransactionEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<TransactionEntity>().Property(e => e.Date).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<CategoryEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<CategoryEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<CreditCardEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<CreditCardEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<CreditCardEntity>().Property(e => e.Validity).HasConversion(Helper.nullableDateTimeConverter);

		// MarketQuoteEntity Postgres converters
		modelBuilder.Entity<MarketQuoteEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<MarketQuoteEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<MarketQuoteEntity>().Property(e => e.RetrievedAtUtc).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<MarketQuoteEntity>().Property(e => e.QuoteTimestampUtc).HasConversion(Helper.nullableDateTimeConverter);

		// Planning entity Postgres converters
		modelBuilder.Entity<PillarEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<PillarEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<ObjectiveEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<ObjectiveEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<ObjectiveEntity>().Property(e => e.Deadline).HasConversion(Helper.nullableDateTimeConverter);

		modelBuilder.Entity<KeyResultEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.Deadline).HasConversion(Helper.nullableDateTimeConverter);
		modelBuilder.Entity<KeyResultEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<QuarterPlanEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<QuarterPlanEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<QuarterPlanEntity>().Property(e => e.TargetDate).HasConversion(Helper.nullableDateTimeConverter);
		modelBuilder.Entity<QuarterPlanEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<QuarterTaskEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<QuarterTaskEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<ReviewTemplateEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<ReviewTemplateEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<ReviewTemplateEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<ReviewTemplateItemEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<ReviewTemplateItemEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<ReviewRunEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<ReviewRunEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<ReviewRunEntity>().Property(e => e.RunDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<ReviewRunItemEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<ReviewRunItemEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<RiskEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<RiskEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<RiskEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<HabitEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<HabitEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<HabitEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);

		modelBuilder.Entity<HabitLogEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<HabitLogEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<HabitLogEntity>().Property(e => e.LogDate).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<AnnualPlanEntity>().Property(e => e.UpdatedAtUtc).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<RoutineTemplateEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<RoutineTemplateEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<RoutineBlockEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<RoutineBlockEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<WeeklyFocusEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<WeeklyFocusEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<FinancialSnapshotEntity>().Property(e => e.CalculatedAtUtc).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<PlanningReviewEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<PlanningReviewEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<PlanningReviewEntity>().Property(e => e.CompletedAtUtc).HasConversion(Helper.nullableDateTimeConverter);

    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.GeneratedAtUtc).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<PlanningActionEntity>().Property(e => e.ResolvedAtUtc).HasConversion(Helper.nullableDateTimeConverter);

    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.StartedAtUtc).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<PlanningActionRunEntity>().Property(e => e.CompletedAtUtc).HasConversion(Helper.nullableDateTimeConverter);

    modelBuilder.Entity<VehicleEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<VehicleEntity>().Property(e => e.UpdatedAtUtc).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<VehicleRecordEntity>().Property(e => e.UpdatedAtUtc).HasConversion(Helper.dateTimeConverter);

    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.ResolvedAtUtc).HasConversion(Helper.nullableDateTimeConverter);
    modelBuilder.Entity<VehicleReminderEntity>().Property(e => e.UpdatedAtUtc).HasConversion(Helper.dateTimeConverter);

		// AI Provider Postgres converters
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.RefId).HasConversion(Helper.guidConverter);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.CreationDate).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.UpdatedAt).HasConversion(Helper.dateTimeConverter);
		modelBuilder.Entity<AiProviderSettingEntity>().Property(e => e.LastTestedAtUtc).HasConversion(Helper.nullableDateTimeConverter);
	}

	/// <summary>
	/// Configures encryption for sensitive credit card properties in the data model.
	/// </summary>
	/// <remarks>This method applies encryption conversions to the Number and SecurityCode properties of the
	/// CreditCardEntity. No configuration is performed if the EncryptionService is not initialized.</remarks>
	/// <param name="modelBuilder">The builder used to configure entity properties for the model.</param>
	private static void ConfigureEncryption(ModelBuilder modelBuilder)
	{
		if (EncryptionService == null)
		{
			return;
		}

    modelBuilder.Entity<CreditCardEntity>().Property(c => c.Number).HasConversion(nullableEncryptionConverter);
    modelBuilder.Entity<CreditCardEntity>().Property(c => c.SecurityCode).HasConversion(nullableEncryptionConverter);
		modelBuilder.Entity<CreditCardEntity>().Property(c => c.LimitValue).HasConversion(decimalEncryptionConverter);
    modelBuilder.Entity<AllocationEntity>().Property(a => a.Name).HasConversion(requiredEncryptionConverter).HasColumnType("TEXT");
    modelBuilder.Entity<AllocationEntity>().Property(a => a.Initials).HasConversion(nullableEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.AllocationType).HasConversion(allocationTypeEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.Quantity).HasConversion(decimalNonNullEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.ProfitPercentage).HasConversion(decimalEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.CurrentUnitValue).HasConversion(decimalEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AllocationEntity>().Property(a => a.BuyUnitValue).HasConversion(decimalNonNullEncryptionConverter).HasColumnType("TEXT");

		// Encrypt sensitive bank account fields at rest.
		modelBuilder.Entity<AccountEntity>()
    .Property(a => a.NumberAccount).HasConversion(nullableEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AccountEntity>()
    .Property(a => a.Agency).HasConversion(nullableEncryptionConverter).HasColumnType("TEXT");
		modelBuilder.Entity<AccountEntity>()
    .Property(a => a.BankCode).HasConversion(nullableEncryptionConverter).HasColumnType("TEXT");

		// Encrypt IMAP application password at rest.
		modelBuilder.Entity<ImapSettingEntity>()
    .Property(s => s.ImapPassword).HasConversion(requiredEncryptionConverter).HasColumnType("TEXT");

		// Encrypt AI provider API key at rest.
		modelBuilder.Entity<AiProviderSettingEntity>()
    .Property(s => s.ApiKey).HasConversion(nullableEncryptionConverter).HasColumnType("TEXT");
	}

	/// <summary>
	/// Converts a nullable decimal value to its encrypted string representation.
	/// </summary>
	/// <remarks>This method uses the EncryptionService to encrypt the string representation of the decimal value.
	/// Ensure that the EncryptionService is properly initialized before calling this method.</remarks>
	/// <param name="v">The nullable decimal value to convert. If the value is null, the method returns null.</param>
	/// <returns>An encrypted string representation of the decimal value, or null if the input is null.</returns>
	private static string? DecimalToEncryptedString(decimal? v)
	{
		if (!v.HasValue)
		{
			return null;
		}

		string text = v.Value.ToString(CultureInfo.InvariantCulture);
		return EncryptionService!.Encrypt(text);
	}

	/// <summary>
	/// Converts an encrypted string representation of a decimal value to its decimal equivalent.
	/// </summary>
	/// <remarks>This method uses the EncryptionService to decrypt the input string before attempting to parse it as
	/// a decimal. The decrypted string must be a valid decimal in invariant culture format to be converted
	/// successfully.</remarks>
	/// <param name="v">The encrypted string to decrypt and convert. This parameter can be null or empty.</param>
	/// <returns>The decimal value represented by the decrypted string if the conversion is successful; otherwise, null.</returns>
	private static decimal? EncryptedStringToDecimal(string? v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return null;
		}

    string? decrypted = EncryptionService!.Decrypt(v);
		if (decimal.TryParse(decrypted, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d))
		{
			return d;
		}
		return null;
	}

	/// <summary>
	/// Converts the specified allocation type to its encrypted string representation.
	/// </summary>
	/// <remarks>This method uses the EncryptionService to encrypt the string representation of the allocation
	/// type's integer value. Ensure that the EncryptionService is properly initialized before calling this
	/// method.</remarks>
	/// <param name="v">The allocation type to convert. Must be a valid member of the AllocationType enumeration.</param>
	/// <returns>An encrypted string representation of the integer value of the specified allocation type, or null if the encryption
	/// fails.</returns>
	private static string? AllocationTypeToEncryptedString(AllocationType v)
	{
		string text = ((int)v).ToString(CultureInfo.InvariantCulture);
		return EncryptionService!.Encrypt(text);
	}

	/// <summary>
	/// Converts an encrypted string representation of an allocation type to its corresponding AllocationType enumeration
	/// value.
	/// </summary>
	/// <remarks>If the input string is null, empty, or does not represent a valid AllocationType after decryption,
	/// the method returns the default value. The method expects the decrypted string to be a valid integer corresponding
	/// to an AllocationType enumeration value.</remarks>
	/// <param name="v">The encrypted string that represents an AllocationType value. This parameter can be null or empty.</param>
	/// <returns>The corresponding AllocationType value if the decryption and conversion succeed; otherwise, the default value of
	/// AllocationType.</returns>
	private static AllocationType AllocationTypeFromEncryptedString(string? v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return default;
		}
    string? decrypted = EncryptionService!.Decrypt(v);
		if (int.TryParse(decrypted, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && Enum.IsDefined(typeof(AllocationType), i))
		{
			return (AllocationType)i;
		}
		return default;
	}

	/// <summary>Stamps OwnerUserId on new tenant-scoped entities before persisting.</summary>
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		StampOwnerUserId();
		GuardDemoTenantWrites();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	/// <summary>Stamps OwnerUserId on new tenant-scoped entities before persisting.</summary>
	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		StampOwnerUserId();
		GuardDemoTenantWrites();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

  /// <summary>
  /// Gets a value indicating whether the caller runs inside an authorized demonstration-write scope.
  /// System paths use it to tell a maintenance run apart from an anonymous request.
  /// </summary>
  public static bool IsDemoWriteScopeActive => demoWriteScope.Value;

  /// <summary>
  /// Opens a scope in which writes to the demonstration tenant are authorized.
  /// Reserved for the demonstration seeder and its refresh service; never for request-driven code.
  /// </summary>
  /// <returns>A handle that revokes the authorization when disposed.</returns>
  public static IDisposable AllowDemoWrites()
  {
    return new DemoWriteScope();
  }

  /// <summary>
  /// Rejects any insert, update or delete that targets the demonstration tenant.
  /// Anonymous visitors browse a read-only showcase; persisting requires a real profile.
  /// </summary>
  /// <remarks>
  /// Both the ambient tenant and the per-entity owner are inspected: contexts created through
  /// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> inside an interactive circuit have no
  /// ambient HTTP context, so the owner stamped on the tracked entities is the only reliable signal there.
  /// </remarks>
  private void GuardDemoTenantWrites()
  {
    if (demoWriteScope.Value)
    {
      return;
    }

    foreach (EntityEntry entry in ChangeTracker.Entries()
      .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
    {
      if (IsIdentityEntry(entry))
      {
        continue;
      }

      if (effectiveUserId == DemoUserId || ResolveOwnerUserId(entry) == DemoUserId)
      {
        throw new DemoReadOnlyException();
      }
    }
  }

  /// <summary>
  /// Determines whether a tracked entry belongs to the ASP.NET Core Identity store.
  /// Sign-in bookkeeping such as failed-attempt counters and lockout runs on anonymous requests
  /// and must persist, otherwise a wrong password surfaces as a server error instead of a login failure.
  /// </summary>
  /// <param name="entry">Tracked entry being persisted.</param>
  /// <returns><see langword="true"/> when the entity is the application user or a stock Identity entity.</returns>
  private static bool IsIdentityEntry(EntityEntry entry)
  {
    return entry.Entity is ApplicationUser
      || entry.Entity.GetType().Namespace == "Microsoft.AspNetCore.Identity";
  }

  /// <summary>Reads the owner of a tracked entity from its shadow or declared OwnerUserId property.</summary>
  /// <param name="entry">Tracked entry being persisted.</param>
  /// <returns>The owner identifier, or <see langword="null"/> when the entity is not tenant-scoped.</returns>
  private static string? ResolveOwnerUserId(EntityEntry entry)
  {
    Microsoft.EntityFrameworkCore.Metadata.IProperty? ownerProperty =
      entry.Metadata.FindProperty(OwnerUserIdShadowProperty)
      ?? entry.Metadata.FindProperty(nameof(PlanningActionEntity.OwnerUserId));
    if (ownerProperty == null)
    {
      return null;
    }

    PropertyEntry property = entry.Property(ownerProperty.Name);
    string? current = property.CurrentValue as string;
    if (!string.IsNullOrEmpty(current))
    {
      return current;
    }

    return entry.State == EntityState.Added ? null : property.OriginalValue as string;
  }

  /// <summary>Ambient authorization used by the demonstration seeder while it populates the showcase tenant.</summary>
  private sealed class DemoWriteScope : IDisposable
  {
    private readonly bool previous;

    internal DemoWriteScope()
    {
      this.previous = demoWriteScope.Value;
      demoWriteScope.Value = true;
    }

    public void Dispose()
    {
      demoWriteScope.Value = this.previous;
    }
  }

	private void StampOwnerUserId()
	{
		if (effectiveUserId is null)
		{
			return;
		}

		foreach (EntityEntry entry in ChangeTracker.Entries()
		.Where(e => e.State == EntityState.Added &&
		(e.Entity is AccountEntity || e.Entity is AllocationEntity ||
		e.Entity is TransactionEntity || e.Entity is CreditCardEntity)))
		{
			string? current = entry.Property(OwnerUserIdShadowProperty).CurrentValue as string;
			if (string.IsNullOrEmpty(current))
			{
				entry.Property(OwnerUserIdShadowProperty).CurrentValue = effectiveUserId;
			}
		}
	}

  // Nullable string encryption converter for optional sensitive data.
  private static readonly ValueConverter<string?, string?> nullableEncryptionConverter = new ValueConverter<string?, string?>(
  static v => EncryptionService!.Encrypt(v),
  static v => EncryptionService!.Decrypt(v));

  // Required string encryption converter for non-null sensitive data.
  private static readonly ValueConverter<string, string?> requiredEncryptionConverter = new ValueConverter<string, string?>(
  static v => EncryptionService!.Encrypt(v) ?? string.Empty,
  static v => EncryptionService!.Decrypt(v) ?? string.Empty);

  // Nullable decimal converter for properties that can be null (e.g. ProfitPercentage)
  private static readonly ValueConverter<decimal?, string?> decimalEncryptionConverter = new ValueConverter<decimal?, string?>(
  v => DecimalToEncryptedString(v),
  v => EncryptedStringToDecimal(v));

  // Non-nullable decimal converter for properties that are not nullable (e.g. Quantity)
  private static readonly ValueConverter<decimal, string?> decimalNonNullEncryptionConverter = new ValueConverter<decimal, string?>(
  v => DecimalToEncryptedString(v),
  v => EncryptedStringToDecimal(v) ?? 0m);

  // AllocationType (enum) encrypted as string containing the numeric value
  private static readonly ValueConverter<AllocationType, string?> allocationTypeEncryptionConverter = new ValueConverter<AllocationType, string?>(
  v => AllocationTypeToEncryptedString(v),
  v => AllocationTypeFromEncryptedString(v));
}
