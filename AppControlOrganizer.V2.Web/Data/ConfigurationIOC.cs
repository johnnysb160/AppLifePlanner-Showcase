//-----------------------------------------------------------------------
// <copyright file="ConfigurationIOC.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data;

using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Repositories;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Analytics;
using AppControlOrganizer.V2.Web.Services.Localization;
using AppControlOrganizer.V2.Web.Services.Ai;
using AppControlOrganizer.V2.Web.Services.Imap;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Planning;
using AppControlOrganizer.V2.Web.Services.Vehicles;
using AppControlOrganizer.V2.Web.Services.Widgets;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Provides methods to configure Inversion of Control (IOC) services and application settings.
/// </summary>
public static class ConfigurationIOC
{
  /// <summary>
  /// Gets whether the current application process selected the secondary Neon database at startup.
  /// </summary>
  public static bool IsRuntimeUsingStandby { get; private set; }

	/// <summary>
	/// Configures application services and dependencies for dependency injection, including configuration values, caching,
	/// database context, and repositories.
	/// </summary>
	/// <remarks>This method registers core infrastructure components such as configuration value providers, memory
	/// caching, database context, and repositories. It is typically called during application startup to prepare the
	/// dependency injection container.</remarks>
	/// <param name="services">The service collection to which application services and dependencies will be added.</param>
	/// <param name="configuration">The application configuration used to initialize configuration values and service settings.</param>
	/// <returns>The same <see cref="IServiceCollection"/> instance with the configured services.</returns>
	public static IServiceCollection ConfigureIOC(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton(x => new ConfigurationsValues(configuration));

		ConfigurationsValues configurations = new ConfigurationsValues(configuration);
		int timeout = Convert.ToInt32(configurations.ConnectionTimeout);

		services.AddMemoryCache();
		services.AddHttpContextAccessor();
		services.InitializePostgresContext(configurations, timeout);
		services.InitializeRepositories();
		services.ServicesConfigure(configuration);

		return services;
	}

	/// <summary>
	/// Configures the application settings by adding JSON files and environment variables.
	/// </summary>
	/// <param name="configurationManager">The configuration builder.</param>
	/// <returns>The configuration builder with the application settings configured.</returns>
  public static IConfigurationBuilder ConfigureAppSettings(this IConfigurationBuilder configurationManager, string? environmentName = null, string? basePath = null)
  {
    string? resolvedEnvironment = environmentName
      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(basePath))
    {
      configurationManager.SetBasePath(basePath);
    }

    configurationManager.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    if (!string.IsNullOrWhiteSpace(resolvedEnvironment))
    {
      configurationManager.AddJsonFile($"appsettings.{resolvedEnvironment}.json", optional: true, reloadOnChange: true);
    }

    configurationManager.AddEnvironmentVariables();

    return configurationManager;
  }

	/// <summary>
	/// Resolves which database connection string to use.
  /// Uses the configured Supabase connection first, keeping Neon as a standby target.
	/// </summary>
	public static string ResolveConnectionString(ConfigurationsValues cfg)
	{
    if (!string.IsNullOrWhiteSpace(cfg.SupabaseConnectionString)
        && ProbeConnection(cfg.SupabaseConnectionString))
    {
      IsRuntimeUsingStandby = false;
      Console.WriteLine("[Database] Using primary database (Supabase).");
      return cfg.SupabaseConnectionString;
    }

    if (!string.IsNullOrWhiteSpace(cfg.NeonConnectionString)
        && ProbeConnection(cfg.NeonConnectionString))
    {
      IsRuntimeUsingStandby = true;
      Console.WriteLine("[Database] Supabase unavailable. Using secondary database (Neon).");
      return cfg.NeonConnectionString;
    }

    if (!string.IsNullOrWhiteSpace(cfg.SupabaseConnectionString))
    {
      IsRuntimeUsingStandby = false;
      Console.WriteLine("[Database] Supabase probe failed. Using configured Supabase connection despite probe failure.");
      return cfg.SupabaseConnectionString;
    }

    if (!string.IsNullOrWhiteSpace(cfg.NeonConnectionString))
    {
      IsRuntimeUsingStandby = true;
      Console.WriteLine("[Database] Supabase not configured. Using Neon despite probe failure.");
      return cfg.NeonConnectionString;
    }

    throw new InvalidOperationException(
      "No database connection configured. " +
      "Set SUPABASE_CONNECTION and/or NEON_CONNECTION environment variables.");
	}

	private static bool ProbeConnection(string connectionString, int timeoutSeconds = 5)
	{
		try
		{
			NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(connectionString)
			{
				Timeout = timeoutSeconds
			};

			using NpgsqlConnection conn = new NpgsqlConnection(builder.ToString());
			conn.Open();
			using NpgsqlCommand cmd = new NpgsqlCommand("SELECT 1", conn);
			cmd.ExecuteScalar();
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Configures the Postgres-based LifePlannerContext for dependency injection using the specified configuration and timeout.
	/// </summary>
	/// <param name="services">The service collection to which the database context will be added.</param>
	/// <param name="cfg">The configuration values containing the Neon connection string.</param>
	/// <param name="timeoutInSeconds">The maximum time, in seconds, to wait for database operations before timing out.</param>
	/// <returns>The same IServiceCollection instance, enabling method chaining.</returns>
	public static IServiceCollection InitializePostgresContext(this IServiceCollection services, ConfigurationsValues cfg, int timeoutInSeconds)
	{
		string resolvedConnectionString = ResolveConnectionString(cfg);

		string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
		Action<IServiceProvider, DbContextOptionsBuilder> configureOptions = (sp, options) =>
		{
			options.UseNpgsql(resolvedConnectionString, npgsql =>
			{
				npgsql.CommandTimeout(timeoutInSeconds);
				npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
			});

			if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
			{
				options.EnableDetailedErrors()
				.EnableSensitiveDataLogging()
				.LogTo(msg => System.Diagnostics.Debug.WriteLine(msg),
				new[] { DbLoggerCategory.Database.Command.Name },
				LogLevel.Information);
			}
			else
			{
				options.EnableDetailedErrors(false);
			}
		};

		services.AddDbContextFactory<LifePlannerContext>(configureOptions);

		// Identity requires a scoped DbContext alongside the factory used by repositories
		services.AddDbContext<LifePlannerContext>(configureOptions);

		return services;
	}


	/// <summary>
	/// Registers repository services for dependency injection in the specified service collection.
	/// </summary>
	/// <remarks>This method adds transient registrations for common repository interfaces and their
	/// implementations. Call this method during application startup to ensure repositories are available for dependency
	/// injection throughout the application.</remarks>
	/// <param name="services">The service collection to which the repository services will be added.</param>
	/// <returns>The same instance of <see cref="IServiceCollection"/> with repository services registered.</returns>
	public static IServiceCollection InitializeRepositories(this IServiceCollection services)
	{
		services.AddTransient<IAccountRepository, AccountRepository>();
		services.AddTransient<ICreditCardRepository, CreditCardRepository>();
		services.AddTransient<IAllocationRepository, AllocationRepository>();
		services.AddTransient<ITransactionRepository, TransactionRepository>();
		services.AddTransient<ICategoryRepository, CategoryRepository>();
		services.AddTransient<IAppSettingRepository, AppSettingRepository>();
		services.AddTransient<IMarketQuoteRepository, MarketQuoteRepository>();

		return services;
	}

	/// <summary>
	/// Configures application services and registers dependencies required for core functionality.
	/// </summary>
	/// <remarks>This method registers services such as Transaction import, category management, account logo
	/// ensure all required services are available for dependency injection.</remarks>
	/// <param name="services">The service collection to which application services will be added. Cannot be null.</param>
	/// <param name="configuration">The application configuration used to configure certain services. Cannot be null.</param>
	/// <returns>The same <see cref="IServiceCollection"/> instance provided in <paramref name="services"/>, with additional
	/// services registered.</returns>
	public static IServiceCollection ServicesConfigure(this IServiceCollection services, IConfiguration configuration)
	{
    services.AddLocalization(options => options.ResourcesPath = "Resources");

		// Register services
		services.AddScoped<TransactionImportService>();
		services.AddScoped<CategoryService>();

		// Account logo services
		services.AddSingleton<AccountLogoResolver>();
		services.AddHostedService<AccountLogoBackfillHostedService>();

		// Market quote services
		services.AddHttpClient();
		services.AddHostedService<MarketQuoteUpdaterHostedService>();

		services.AddSingleton<IEncryptionService, EncryptionService>();

		// Market quote cache for reading latest quotes
		services.AddSingleton<MarketQuoteCache>();

		// Allocation pricing service (centralized quote-based pricing logic)
		services.AddSingleton<AllocationQuotePricingService>();
    services.AddScoped<FixedIncomeAuditService>();

		services.AddSingleton<DatabaseHealthService>();
		services.AddHostedService<DatabaseHealthCheckHostedService>();

		// Scoped state containers — persist data across page navigations within the same circuit.
		services.AddScoped<AccountsStateContainer>();
		services.AddScoped<AllocationsStateContainer>();

		// Financial sync — recomputes Balance and TotalInvested on account after transaction/allocation changes.
		services.AddScoped<FinancialSyncService>();
    services.AddScoped<FinancialYearReviewService>();
    services.AddSingleton<IFinancialFactsService, FinancialFactsService>();

    // Simplified planning experience is additive; advanced planning remains available.
    services.AddScoped<IPlanningExperienceService, PlanningExperienceService>();
    services.AddScoped<IPlanningActionService, PlanningActionService>();
    services.AddHostedService<PlanningActionGeneratorHostedService>();

    // Personal vehicles are a manual routine-domain feature with no external dependency.
    services.AddScoped<IVehicleService, VehicleService>();

    // Keeps the anonymous showcase complete and anchored to the current date while the app runs.
    services.AddHostedService<AppControlOrganizer.V2.Web.Services.Demo.DemoDataRefreshHostedService>();

    // Local widgets consume compact snapshots and pair through one-time codes.
    services.AddScoped<IWidgetPairingService, WidgetPairingService>();
    services.AddScoped<ITodayWidgetSnapshotService, TodayWidgetSnapshotService>();

		// Planning service (OKRs, Quarters, Reviews, Risks, Habits)
		services.AddScoped<IPlanningService, PlanningService>();

		// IMAP email sync — polls bank notification emails and imports transactions
		services.AddScoped<IImapEmailSyncService, ImapEmailSyncService>();
		services.AddHostedService<ImapEmailSyncHostedService>();

    // Access analytics — bounded HTTP/page-view/feature-usage capture; no-op outside Production.
    services.AddSingleton<AccessAnalyticsRecorder>();
    services.AddSingleton<IAccessAnalyticsRecorder>(sp => sp.GetRequiredService<AccessAnalyticsRecorder>());
    services.AddSingleton<IGeoIpLookupService, GeoIpLookupService>();
    services.AddHostedService<AccessAnalyticsHostedService>();

		// AI provider connection test service (transient: stateless per call)
		services.AddTransient<IAiProviderTestService, AiProviderTestService>();

		// UI localization (scoped: one language preference per SignalR circuit)
		services.AddScoped<LocService>();

		// UI visibility toggle — hides/shows monetary values across all pages
		services.AddScoped<AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService>();

		return services;
	}
}
