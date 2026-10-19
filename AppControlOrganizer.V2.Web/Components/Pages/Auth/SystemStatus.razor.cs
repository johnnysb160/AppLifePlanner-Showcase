//-----------------------------------------------------------------------
// <copyright file="SystemStatus.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Auth
{
  using System.Data;
  using System.Data.Common;
  using System.Diagnostics;
  using System.Globalization;
  using System.Net.Http.Headers;
  using System.Text.Json;
  using System.Text.Json.Serialization;
  using AppControlOrganizer.V2.Shared.Entities;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Web.Data;
  using AppControlOrganizer.V2.Web.Data.Auth;
  using AppControlOrganizer.V2.Web.Services;
  using AppControlOrganizer.V2.Web.Services.Analytics;
  using AppControlOrganizer.V2.Web.Services.Logging;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;

  /// <summary>
  /// Read-only, admin-only operational status page.
  /// </summary>
  public partial class SystemStatus : ComponentBase
  {
    private const string Healthy = "Healthy";
    private const string Attention = "Attention";
    private const string Unavailable = "Unavailable";
    private const string Unknown = "Unknown";
    private const string Configured = "Configured";
    private const string Missing = "Missing";
    private const string Active = "Active";
    private const string Inactive = "Inactive";
    private const string External = "External";
    private const string GitHubApiBaseUrl = "https://api.github.com/repos/johnnysb160/AppLifePlanner-Showcase";

    [Inject]
    private IDbContextFactory<LifePlannerContext> DbFactory { get; set; } = null!;

    [Inject]
    private DatabaseHealthService DatabaseHealth { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private IWebHostEnvironment Environment { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = null!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; } = null!;

    [Inject]
    private IInMemoryLogBuffer InMemoryLogBuffer { get; set; } = null!;

    [Inject]
    private IGeoIpLookupService GeoIpLookup { get; set; } = null!;

    private bool canLoad;
    private string activeTab = "operations";
    private string chartRange = "week";
    private bool isDemoMode;
    private bool isLoading = true;
    private SystemStatusSnapshot? snapshot;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
      this.isDemoMode = this.Navigation.ToBaseRelativePath(this.Navigation.Uri)
        .TrimEnd('/')
        .Equals("demo/system-status", StringComparison.OrdinalIgnoreCase);
      if (this.isDemoMode)
      {
        this.canLoad = true;
        return;
      }

      AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();
      if (authState.User.Identity?.IsAuthenticated != true || !authState.User.IsInRole("Admin"))
      {
        this.Navigation.NavigateTo("/auth/login", forceLoad: true);
        return;
      }

      this.canLoad = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (!firstRender || !this.canLoad)
      {
        return;
      }

      this.snapshot = this.isDemoMode ? this.BuildDemoSnapshot() : await this.BuildSnapshotAsync();
      this.isLoading = false;
      await this.InvokeAsync(this.StateHasChanged);
    }

    private static string GetBadgeClass(string state)
    {
      return state switch
      {
        Healthy or Configured or Active => "badge rounded-pill text-bg-success",
        Attention or Unknown or External => "badge rounded-pill text-bg-warning",
        Unavailable or Missing or Inactive => "badge rounded-pill text-bg-danger",
        _ => "badge rounded-pill text-bg-secondary",
      };
    }

    private async Task<SystemStatusSnapshot> BuildSnapshotAsync()
    {
      DateTime utcNow = DateTime.UtcNow;
      DateTime since = utcNow.AddHours(-24);
      RepositoryWorkflowStatuses workflows = await this.ReadRepositoryWorkflowStatusesAsync();
      DatabaseStatusSummary database = await this.ReadDatabaseStatusAsync(since, workflows);
      RuntimeStatusSummary runtime = this.ReadRuntimeStatus();
      ConfigurationStatusSummary config = this.ReadConfigurationStatus();
      ObservabilitySummary observability = this.ReadObservabilitySummary(utcNow);
      AccessAnalyticsSummary accessAnalytics = await this.ReadAccessAnalyticsSummaryAsync(utcNow);

      List<SystemStatusTile> topTiles =
      [
        new SystemStatusTile(
          "Database",
          database.DatabaseTileValue,
          database.DatabaseCanConnect ? Healthy : Unavailable,
          database.DatabaseTileDetail,
          null),
        new SystemStatusTile(
          "Market quotes",
          database.LatestQuoteLabel,
          database.QuoteTileState,
          database.QuoteDetail,
          database.QuoteFailureTooltip),
        new SystemStatusTile(
          "Backups",
          database.BackupTileValue,
          database.BackupState,
          database.BackupTileDetail,
          null),
        new SystemStatusTile(
          "Runtime",
          runtime.RuntimeDetail,
          Healthy,
          runtime.ProcessDetail,
          null),
      ];

      List<SystemStatusGroup> groups =
      [
        new SystemStatusGroup("Database", "bi-database-check", [
          new SystemStatusLine("Primary database", database.PrimaryValue, database.PrimaryState, database.PrimaryDetail, null, null, null),
          new SystemStatusLine("Standby database", database.StandbyValue, database.StandbyState, database.StandbyDetail, null, null, null),
          new SystemStatusLine("Database size", database.DatabaseSize, database.DatabaseCanConnect ? Healthy : Unavailable, database.DatabaseStorageDetail, null, null, null),
          new SystemStatusLine("Active connections", database.ActiveConnections, database.DatabaseCanConnect ? Healthy : Unavailable, database.ConnectionUsageDetail, null, null, null),
          new SystemStatusLine("PostgreSQL version", database.PostgresVersion, database.DatabaseCanConnect ? Healthy : Unknown, database.PostgresVersionDetail, null, null, null),
          new SystemStatusLine("Supabase quota", database.SupabaseQuotaValue, database.SupabaseQuotaState, database.SupabaseQuotaDetail, null, null, null),
          new SystemStatusLine("Database probe", database.DatabaseProbeValue, database.DatabaseProbeState, database.DatabaseProbeDetail, null, null, null),
          new SystemStatusLine("Pending migrations", database.PendingMigrationsValue, database.PendingMigrationsState, database.LatestMigration, null, null, null),
        ]),
        new SystemStatusGroup("Backups", "bi-cloud-arrow-up", [
          new SystemStatusLine("Daily backup", database.BackupWorkflowValue, database.BackupWorkflowState, database.BackupWorkflowDetail, null, database.BackupWorkflowUrl, database.BackupWorkflowUrlLabel),
          new SystemStatusLine("Standby destination", database.StandbyBackupValue, database.StandbyState, database.StandbyBackupDetail, null, null, null),
          new SystemStatusLine("Recovery workflow", database.RecoveryWorkflowValue, database.RecoveryWorkflowState, database.RecoveryWorkflowDetail, null, database.RecoveryWorkflowUrl, database.RecoveryWorkflowUrlLabel),
          new SystemStatusLine("Restore validation", database.RestoreValidationValue, database.RestoreValidationState, database.RestoreValidationDetail, null, null, null),
          new SystemStatusLine("Retention", "30 days", database.BackupWorkflowState, this.Loc["Encrypted releases keep restore points visible for manual recovery."], null, null, null),
        ]),
        new SystemStatusGroup("Integrations", "bi-plug", [
          new SystemStatusLine("SMTP email", config.SmtpValue, config.SmtpState, config.SmtpDetail, null, null, null),
          new SystemStatusLine("IMAP sync", database.ImapValue, database.ImapState, database.ImapDetail, null, null, null),
          new SystemStatusLine("AI provider", database.AiValue, database.AiState, database.AiDetail, null, null, null),
          new SystemStatusLine("Widget tokens", database.WidgetValue, database.WidgetState, database.WidgetDetail, null, null, null),
          this.BuildGeoIpLine(),
        ]),
        new SystemStatusGroup("Background services", "bi-arrow-repeat", [
          new SystemStatusLine("Quote updater", database.QuoteUpdaterValue, database.QuoteUpdaterState, database.QuoteUpdaterDetail, database.QuoteFailureTooltip, null, null),
          new SystemStatusLine("Planning actions", database.PlanningValue, database.PlanningState, database.PlanningDetail, null, null, null),
          new SystemStatusLine("Demo refresh", Active, Healthy, this.Loc["Runs daily inside the Web service."], null, null, null),
        ]),
        new SystemStatusGroup("Repository automation", "bi-github", [
          new SystemStatusLine("CI workflow", database.CiWorkflowValue, database.CiWorkflowState, database.CiWorkflowDetail, null, database.CiWorkflowUrl, database.CiWorkflowUrlLabel),
          new SystemStatusLine("Application URL", runtime.BaseUri, Healthy, null, null, null, null),
          new SystemStatusLine("Public registration", config.PublicRegistrationValue, config.PublicRegistrationState, null, null, null, null),
        ]),
        new SystemStatusGroup("Quote providers", "bi-key", [
          new SystemStatusLine("Keyed providers", config.KeyedProviderValue, config.KeyedProviderState, config.KeyedProviderDetail, null, null, null),
          new SystemStatusLine("Fallback providers", Active, Healthy, this.Loc["Yahoo, Stooq, CoinGecko, CoinCap, Binance, AwesomeAPI and brapi.dev do not require local paid configuration."], null, null, null),
          new SystemStatusLine("Last quote failure", database.LastQuoteFailureValue, database.LastQuoteFailureState, database.LastQuoteFailureDetail, database.QuoteFailureTooltip, null, null),
        ]),
      ];

      return new SystemStatusSnapshot(topTiles, groups, observability, accessAnalytics);
    }

    private SystemStatusSnapshot BuildDemoSnapshot()
    {
      DateTime utcNow = DateTime.UtcNow;
      string latestRun = this.FormatDate(utcNow.AddMinutes(-18));
      string dailyRun = this.FormatDate(utcNow.Date.AddHours(8));
      string quoteRun = this.FormatDate(utcNow.AddMinutes(-12));
      ObservabilitySummary observability = this.BuildDemoObservabilitySummary(utcNow);
      AccessAnalyticsSummary accessAnalytics = this.BuildDemoAccessAnalyticsSummary(utcNow);

      List<SystemStatusTile> topTiles =
      [
        new SystemStatusTile(
          "Database",
          "28.34 MB · PostgreSQL 17.6",
          Healthy,
          this.Loc["Simulated primary database, standby and live connection usage."],
          null),
        new SystemStatusTile(
          "Market quotes",
          quoteRun,
          Attention,
          this.Loc["Simulated provider mix with successful updates and recent fallback failures."],
          this.Loc["Demo quote failures are simulated for portfolio showcase."]),
        new SystemStatusTile(
          "Backups",
          this.Loc["Daily + standby ready"],
          Healthy,
          this.Loc["Simulated encrypted backup, release retention and standby sync."],
          null),
        new SystemStatusTile(
          "Runtime",
          "Demo · .NET 8",
          Healthy,
          $"{this.Loc["Started"]}: {this.FormatDate(utcNow.AddHours(-2))}",
          null),
      ];

      List<SystemStatusGroup> groups =
      [
        new SystemStatusGroup("Database", "bi-database-check", [
          new SystemStatusLine("Primary database", this.Loc["Primary PostgreSQL"], Healthy, this.Loc["Simulated primary database endpoint is reachable."], null, null, null),
          new SystemStatusLine("Standby database", this.Loc["Standby PostgreSQL"], Healthy, this.Loc["Simulated standby database endpoint is ready for backup sync."], null, null, null),
          new SystemStatusLine("Database size", "28.34 MB", Healthy, this.Loc["Simulated current database footprint."], null, null, null),
          new SystemStatusLine("Active connections", "17/60", Healthy, this.Loc["Simulated live PostgreSQL sessions compared with the server limit."], null, null, null),
          new SystemStatusLine("PostgreSQL version", "17.6", Healthy, this.Loc["Simulated PostgreSQL compatibility check."], null, null, null),
          new SystemStatusLine("Supabase quota", this.Loc["Simulated"], Attention, this.Loc["Demo shows where provider quota alerts would appear without querying live provider accounts."], null, null, null),
          new SystemStatusLine("Database probe", this.FormatDate(utcNow.AddMinutes(-2)), Healthy, this.Loc["Demo probe simulates primary database health."], null, null, null),
          new SystemStatusLine("Pending migrations", "0", Healthy, $"{this.Loc["Latest applied migration"]}: demo-schema-baseline", null, null, null),
        ]),
        new SystemStatusGroup("Backups", "bi-cloud-arrow-up", [
          new SystemStatusLine("Daily backup", "Success", Healthy, $"{this.Loc["Latest successful workflow execution"]}: {dailyRun} · sample-daily-backup", null, null, null),
          new SystemStatusLine("Standby destination", "Ready", Healthy, this.Loc["Simulated standby target accepts the backup/sync path."], null, null, null),
          new SystemStatusLine("Recovery workflow", "Manual disabled", Attention, this.Loc["Manual recovery workflow is intentionally disabled until a real recovery drill is approved."], null, null, null),
          new SystemStatusLine("Restore validation", "Not tested", Attention, this.Loc["No restore drill is simulated or executed in demo mode."], null, null, null),
          new SystemStatusLine("Retention", "30 days", Healthy, this.Loc["Encrypted releases keep restore points visible for manual recovery."], null, null, null),
        ]),
        new SystemStatusGroup("Integrations", "bi-plug", [
          new SystemStatusLine("SMTP email", Missing, Missing, this.Loc["Simulated missing SMTP configuration shows the password-reset dependency."], null, null, null),
          new SystemStatusLine("IMAP sync", Active, Healthy, $"{this.Loc["Last sync"]}: {this.FormatDate(utcNow.AddMinutes(-28))} · 2 {this.Loc["mapping(s)"]}", null, null, null),
          new SystemStatusLine("AI provider", Active, Healthy, $"{this.Loc["Last test"]}: {this.FormatDate(utcNow.AddDays(-1))} · {this.Loc["succeeded"]}", null, null, null),
          new SystemStatusLine("Widget tokens", Active, Healthy, $"8 {this.Loc["token(s)"]} · 16 {this.Loc["pairing code(s)"]}", null, null, null),
          new SystemStatusLine("GeoIP database", Active, Healthy, this.Loc["Country lookups resolve visitor locations."], null, null, null),
        ]),
        new SystemStatusGroup("Background services", "bi-arrow-repeat", [
          new SystemStatusLine("Quote updater", Active, Attention, $"{this.Loc["Sample quote provider"]} · {this.Loc["Latest workflow execution"]}: {quoteRun}", this.Loc["Demo quote failures are simulated for portfolio showcase."], null, null),
          new SystemStatusLine("Planning actions", Healthy, Healthy, $"{this.Loc["Last run"]}: {latestRun} · 14 {this.Loc["open action(s)"]}", null, null, null),
          new SystemStatusLine("Demo refresh", Active, Healthy, this.Loc["Runs daily inside the Web service."], null, null, null),
        ]),
        new SystemStatusGroup("Repository automation", "bi-github", [
          new SystemStatusLine("CI workflow", "Success", Healthy, $"{this.Loc["Latest successful workflow execution"]}: {latestRun} · {this.Loc["Sample CI pipeline"]}", null, null, null),
          new SystemStatusLine("Application URL", "https://demo.example.com", Healthy, this.Loc["Simulated public application URL."], null, null, null),
          new SystemStatusLine("Public registration", Inactive, Healthy, null, null, null, null),
        ]),
        new SystemStatusGroup("Quote providers", "bi-key", [
          new SystemStatusLine("Keyed providers", $"2/2 {this.Loc["Configured"]}", Healthy, $"{this.Loc["Configured"]}: {this.Loc["Sample provider A"]}, {this.Loc["Sample provider B"]} · {this.Loc["Missing"]}: -", null, null, null),
          new SystemStatusLine("Fallback providers", Active, Healthy, this.Loc["Fallback provider checks are simulated in demo mode."], null, null, null),
          new SystemStatusLine("Last quote failure", quoteRun, Attention, "DEMO-ASSET: simulated provider fallback.", this.Loc["Demo quote failures are simulated for portfolio showcase."], null, null),
        ]),
      ];

      return new SystemStatusSnapshot(topTiles, groups, observability, accessAnalytics);
    }

    private string TabButtonClass(string tab)
    {
      return string.Equals(this.activeTab, tab, StringComparison.Ordinal)
        ? "nav-link active"
        : "nav-link";
    }

    private void SetActiveTab(string tab)
    {
      this.activeTab = tab;
    }

    private string ChartRangeButtonClass(string range)
    {
      return string.Equals(this.chartRange, range, StringComparison.Ordinal)
        ? "btn btn-sm btn-primary"
        : "btn btn-sm btn-outline-secondary";
    }

    private void SetChartRange(string range)
    {
      this.chartRange = range;
    }

    private IReadOnlyList<AccessTrendPoint> GetTrendWindow()
    {
      if (this.snapshot is null)
      {
        return [];
      }

      int days = this.chartRange switch
      {
        "year" => 365,
        "month" => 30,
        _ => 7,
      };

      return this.snapshot.AccessAnalytics.Trend.TakeLast(days).ToList();
    }

    private static string BuildTrendPolylinePoints(IReadOnlyList<AccessTrendPoint> points, double width, double height)
    {
      if (points.Count == 0)
      {
        return string.Empty;
      }

      int maxValue = Math.Max(points.Max(point => point.PageViews), 1);
      double stepX = points.Count > 1 ? width / (points.Count - 1) : 0;

      IEnumerable<string> coordinates = points.Select((point, index) =>
      {
        double x = index * stepX;
        double y = height - ((double)point.PageViews / maxValue * height);
        return $"{x.ToString("F1", CultureInfo.InvariantCulture)},{y.ToString("F1", CultureInfo.InvariantCulture)}";
      });

      return string.Join(" ", coordinates);
    }

    private static string WorkflowUrl(string workflowFile)
    {
      return $"https://github.com/johnnysb160/AppLifePlanner-Showcase/actions/workflows/{workflowFile}";
    }

    private RuntimeStatusSummary ReadRuntimeStatus()
    {
      Process process = Process.GetCurrentProcess();
      DateTime startedAtUtc = process.StartTime.ToUniversalTime();
      string detail = $"{this.Loc["Started"]}: {this.FormatDate(startedAtUtc)}";
      return new RuntimeStatusSummary(
        this.Environment.EnvironmentName,
        this.Navigation.BaseUri.TrimEnd('/'),
        detail,
        $"{this.Environment.EnvironmentName} · .NET 8");
    }

    private ConfigurationStatusSummary ReadConfigurationStatus()
    {
      bool publicRegistration = string.Equals(
        this.Configuration["AllowPublicRegistration"],
        bool.TrueString,
        StringComparison.OrdinalIgnoreCase);

      List<string> configuredProviders = [];
      List<string> missingProviders = [];
      this.TrackProviderKey(configuredProviders, missingProviders, "TwelveData", "TWELVEDATA_API_KEY");
      this.TrackProviderKey(configuredProviders, missingProviders, "Alpha Vantage", "ALPHAVANTAGE_API_KEY");
      this.TrackProviderKey(configuredProviders, missingProviders, "Finnhub", "FINNHUB_API_KEY");

      string keyedProviderValue = string.Format(
        this.Loc.Culture,
        "{0}/3 {1}",
        configuredProviders.Count,
        this.Loc["Configured"]);
      string keyedProviderDetail = $"{this.Loc["Configured"]}: {this.JoinOrDash(configuredProviders)} · {this.Loc["Missing"]}: {this.JoinOrDash(missingProviders)}";

      return new ConfigurationStatusSummary(
        this.ConfiguredValue("Smtp:Host"),
        this.ConfiguredState("Smtp:Host"),
        this.SmtpConfigurationDetail(),
        publicRegistration ? Active : Inactive,
        publicRegistration ? Attention : Healthy,
        keyedProviderValue,
        configuredProviders.Count > 0 ? Healthy : Attention,
        keyedProviderDetail);
    }

    private string SmtpConfigurationDetail()
    {
      if (this.HasConfiguration("Smtp:Host"))
      {
        return this.Loc["SMTP is configured for password reset email delivery."];
      }

      return this.Loc["SMTP is missing because Smtp__Host and sender credentials are not configured. Password reset emails cannot be sent until this is configured."];
    }

    private async Task<DatabaseStatusSummary> ReadDatabaseStatusAsync(DateTime sinceUtc, RepositoryWorkflowStatuses workflows)
    {
      await using LifePlannerContext db = await this.DbFactory.CreateDbContextAsync();

      bool canConnect;
      try
      {
        canConnect = await db.Database.CanConnectAsync();
      }
      catch
      {
        canConnect = false;
      }

      if (!canConnect)
      {
        return this.BuildUnavailableDatabaseStatus(workflows);
      }

      PostgresRuntimeMetrics dbMetrics = await this.ReadPostgresMetricsAsync(db);
      IReadOnlyList<string> pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
      string? latestMigration = (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault();

      QuoteStatusSummary quotes = await this.ReadQuoteStatusAsync(db, sinceUtc);

      DateTime? latestImapSync = await db.ImapSettings
        .IgnoreQueryFilters()
        .Where(item => !item.Deleted)
        .MaxAsync(item => item.LastSyncedAt);
      int imapSettings = await db.ImapSettings.IgnoreQueryFilters().CountAsync(item => !item.Deleted);
      int imapMappings = await db.ImapAccountMappings.IgnoreQueryFilters().CountAsync(item => !item.Deleted);

      PlanningActionRunEntity? latestPlanningRun = await db.PlanningActionRuns
        .IgnoreQueryFilters()
        .OrderByDescending(item => item.StartedAtUtc)
        .FirstOrDefaultAsync();
      int openActions = await db.PlanningActions
        .IgnoreQueryFilters()
        .CountAsync(item => !item.Deleted && item.Status == PlanningActionStatus.Open);

      int aiConfigured = await db.AiProviderSettings.IgnoreQueryFilters().CountAsync(item => !item.Deleted);
      int aiEnabled = await db.AiProviderSettings.IgnoreQueryFilters().CountAsync(item => !item.Deleted && item.IsEnabled);
      AiProviderSettingEntity? latestAiTest = await db.AiProviderSettings
        .IgnoreQueryFilters()
        .Where(item => !item.Deleted && item.LastTestedAtUtc != null)
        .OrderByDescending(item => item.LastTestedAtUtc)
        .FirstOrDefaultAsync();

      int widgetTokens = await db.AppSettings.CountAsync(item => item.Key.StartsWith("widget-token:"));
      int widgetPairingCodes = await db.AppSettings.CountAsync(item => item.Key.StartsWith("widget-pair:"));

      string primaryState = this.DatabaseHealth.IsPrimaryReachable ? Healthy : Attention;
      string standbyState = this.DatabaseHealth.IsStandbyReachable ? Healthy : Attention;
      string liveDatabase = this.DatabaseHealth.IsUsingStandby ? "Neon" : "Supabase";
      string backupState = this.MergeStates(this.DatabaseHealth.IsStandbyReachable ? Healthy : Attention, workflows.Backup.State);
      string databaseTileDetail = string.Format(
        this.Loc.Culture,
        "{0}: {1} · {2}: {3} · {4}: {5}",
        this.Loc["Primary"],
        this.ProviderReachability(this.DatabaseHealth.IsPrimaryReachable),
        this.Loc["Standby"],
        this.ProviderReachability(this.DatabaseHealth.IsStandbyReachable),
        this.Loc["Connections"],
        $"{dbMetrics.ActiveConnections.ToString(this.Loc.Culture)}/{dbMetrics.MaxConnections.ToString(this.Loc.Culture)}");

      return new DatabaseStatusSummary
      {
        DatabaseCanConnect = true,
        DatabaseTileValue = $"{this.FormatBytes(dbMetrics.DatabaseSizeBytes)} · PostgreSQL {dbMetrics.PostgresMajorVersion}",
        DatabaseTileDetail = databaseTileDetail,
        LiveDatabase = liveDatabase,
        DatabaseDetail = this.DatabaseHealth.IsUsingStandby ? this.Loc["Running on secondary (Neon)"] : this.Loc["Running on primary (Supabase)"],
        PrimaryValue = "Supabase",
        PrimaryState = primaryState,
        PrimaryDetail = this.DatabaseHealth.IsPrimaryReachable
          ? this.Loc["Reachable and used as the normal production database."]
          : this.Loc["Primary database is not reachable; application should fail over to standby if configured."],
        StandbyValue = "Neon",
        StandbyState = standbyState,
        StandbyDetail = this.DatabaseHealth.IsStandbyReachable
          ? this.Loc["Reachable and ready as the backup/sync destination."]
          : this.Loc["Standby database is not reachable; backups cannot be trusted for recovery."],
        DatabaseProbeValue = this.DatabaseHealth.LastProbeUtc.HasValue ? this.FormatDate(this.DatabaseHealth.LastProbeUtc.Value) : this.Loc[Unknown],
        DatabaseProbeState = this.DatabaseHealth.LastProbeUtc.HasValue ? Healthy : Unknown,
        DatabaseProbeDetail = this.DatabaseHealth.IsUsingStandby ? this.Loc["Running on secondary (Neon)"] : this.Loc["Running on primary (Supabase)"],
        PendingMigrationsValue = pendingMigrations.Count == 0 ? "0" : pendingMigrations.Count.ToString(this.Loc.Culture),
        PendingMigrationsState = pendingMigrations.Count == 0 ? Healthy : Attention,
        LatestMigration = latestMigration is null ? this.Loc[Unknown] : $"{this.Loc["Latest applied migration"]}: {latestMigration}",
        DatabaseSize = this.FormatBytes(dbMetrics.DatabaseSizeBytes),
        DatabaseStorageDetail = $"{this.Loc["Current database"]}: {dbMetrics.DatabaseName}",
        ActiveConnections = $"{dbMetrics.ActiveConnections.ToString(this.Loc.Culture)}/{dbMetrics.MaxConnections.ToString(this.Loc.Culture)}",
        ConnectionUsageDetail = this.Loc["Live PostgreSQL sessions compared with the configured server limit."],
        PostgresVersion = dbMetrics.PostgresMajorVersion,
        PostgresVersionDetail = dbMetrics.Version,
        SupabaseQuotaValue = this.Loc["Check dashboard"],
        SupabaseQuotaState = Attention,
        SupabaseQuotaDetail = this.Loc["Supabase quota is a provider-account signal; validate it in the existing Supabase dashboard before restriction dates."],
        LatestQuoteLabel = quotes.LatestQuoteLabel,
        QuoteTileState = quotes.TileState,
        QuoteDetail = quotes.Detail,
        QuoteFailureTooltip = quotes.FailureTooltip,
        QuoteUpdaterValue = quotes.UpdaterValue,
        QuoteUpdaterState = quotes.UpdaterState,
        QuoteUpdaterDetail = quotes.UpdaterDetail,
        LastQuoteFailureValue = quotes.LastFailureValue,
        LastQuoteFailureState = quotes.LastFailureState,
        LastQuoteFailureDetail = quotes.LastFailureDetail,
        ImapValue = imapSettings > 0 ? Active : Inactive,
        ImapState = imapSettings > 0 ? Healthy : Unknown,
        ImapDetail = latestImapSync.HasValue
          ? $"{this.Loc["Last sync"]}: {this.FormatDate(latestImapSync.Value)} · {imapMappings.ToString(this.Loc.Culture)} {this.Loc["mapping(s)"]}"
          : $"{imapSettings.ToString(this.Loc.Culture)} {this.Loc["setting(s)"]} · {imapMappings.ToString(this.Loc.Culture)} {this.Loc["mapping(s)"]}",
        AiValue = aiEnabled > 0 ? Active : Inactive,
        AiState = aiEnabled > 0 ? Healthy : Unknown,
        AiDetail = latestAiTest is null
          ? $"{aiConfigured.ToString(this.Loc.Culture)} {this.Loc["setting(s)"]}"
          : $"{this.Loc["Last test"]}: {this.FormatDate(latestAiTest.LastTestedAtUtc!.Value)} · {(latestAiTest.LastTestSucceeded == true ? this.Loc["succeeded"] : this.Loc["failed"])}",
        WidgetValue = widgetTokens > 0 ? Active : Inactive,
        WidgetState = widgetTokens > 0 ? Healthy : Unknown,
        WidgetDetail = $"{widgetTokens.ToString(this.Loc.Culture)} {this.Loc["token(s)"]} · {widgetPairingCodes.ToString(this.Loc.Culture)} {this.Loc["pairing code(s)"]}",
        PlanningValue = latestPlanningRun is null ? Unknown : latestPlanningRun.Succeeded ? Healthy : Attention,
        PlanningState = latestPlanningRun is null ? Unknown : latestPlanningRun.Succeeded ? Healthy : Attention,
        PlanningDetail = latestPlanningRun is null
          ? this.Loc["No generator run found."]
          : $"{this.Loc["Last run"]}: {this.FormatDate(latestPlanningRun.StartedAtUtc)} · {openActions.ToString(this.Loc.Culture)} {this.Loc["open action(s)"]}",
        BackupTileValue = this.BackupTileValue(workflows.Backup, this.DatabaseHealth.IsStandbyReachable),
        BackupState = backupState,
        BackupTileDetail = this.DatabaseHealth.IsStandbyReachable
          ? workflows.Backup.Detail
          : this.Loc["Standby is unreachable; check the backup workflow before trusting recovery."],
        BackupWorkflowValue = workflows.Backup.Value,
        BackupWorkflowState = workflows.Backup.State,
        BackupWorkflowDetail = workflows.Backup.Detail,
        BackupWorkflowUrl = workflows.Backup.Url,
        BackupWorkflowUrlLabel = workflows.Backup.UrlLabel,
        StandbyBackupValue = this.DatabaseHealth.IsStandbyReachable ? this.Loc["Ready"] : this.Loc["Blocked"],
        StandbyBackupDetail = this.DatabaseHealth.IsStandbyReachable
          ? this.Loc["Neon accepts the backup/sync path now."]
          : this.Loc["Neon must be reachable before recovery is reliable."],
        RecoveryWorkflowValue = workflows.Recovery.Value,
        RecoveryWorkflowState = workflows.Recovery.State,
        RecoveryWorkflowDetail = workflows.Recovery.Detail,
        RecoveryWorkflowUrl = workflows.Recovery.Url,
        RecoveryWorkflowUrlLabel = workflows.Recovery.UrlLabel,
        RestoreValidationValue = this.Loc["Not tested"],
        RestoreValidationState = Attention,
        RestoreValidationDetail = this.Loc["No restore drill has been executed yet; recovery remains manual and intentionally disabled."],
        CiWorkflowValue = workflows.Ci.Value,
        CiWorkflowState = workflows.Ci.State,
        CiWorkflowDetail = workflows.Ci.Detail,
        CiWorkflowUrl = workflows.Ci.Url,
        CiWorkflowUrlLabel = workflows.Ci.UrlLabel,
      };
    }

    private async Task<QuoteStatusSummary> ReadQuoteStatusAsync(LifePlannerContext db, DateTime sinceUtc)
    {
      MarketQuoteEntity? latestQuote = await db.MarketQuotes
        .AsNoTracking()
        .OrderByDescending(item => item.RetrievedAtUtc)
        .FirstOrDefaultAsync();
      int quoteSuccessLastDay = await db.MarketQuotes
        .AsNoTracking()
        .CountAsync(item => item.RetrievedAtUtc >= sinceUtc && item.Status == MarketQuoteStatus.Success);
      int quoteFailuresLastDay = await db.MarketQuotes
        .AsNoTracking()
        .CountAsync(item => item.RetrievedAtUtc >= sinceUtc && item.Status == MarketQuoteStatus.Failed);
      int quoteUnsupportedLastDay = await db.MarketQuotes
        .AsNoTracking()
        .CountAsync(item => item.RetrievedAtUtc >= sinceUtc && item.Status == MarketQuoteStatus.Unsupported);
      List<MarketQuoteEntity> recentFailures = await db.MarketQuotes
        .AsNoTracking()
        .Where(item => item.RetrievedAtUtc >= sinceUtc && item.Status != MarketQuoteStatus.Success)
        .OrderByDescending(item => item.RetrievedAtUtc)
        .Take(6)
        .ToListAsync();

      string failureTooltip = this.BuildQuoteFailureTooltip(recentFailures);
      MarketQuoteEntity? lastFailure = recentFailures.FirstOrDefault();
      bool hasFailures = quoteFailuresLastDay > 0 || quoteUnsupportedLastDay > 0;
      bool hasFreshQuote = latestQuote is not null && latestQuote.RetrievedAtUtc >= sinceUtc;

      return new QuoteStatusSummary
      {
        LatestQuoteLabel = latestQuote is null ? this.Loc[Unknown] : this.FormatDate(latestQuote.RetrievedAtUtc),
        TileState = hasFreshQuote && !hasFailures ? Healthy : Attention,
        Detail = $"{quoteSuccessLastDay.ToString(this.Loc.Culture)} {this.Loc["succeeded"]} · {quoteFailuresLastDay.ToString(this.Loc.Culture)} {this.Loc["failed"]} · {quoteUnsupportedLastDay.ToString(this.Loc.Culture)} {this.Loc["unsupported"]}",
        FailureTooltip = failureTooltip,
        UpdaterValue = quoteSuccessLastDay > 0 ? Active : Attention,
        UpdaterState = hasFailures ? Attention : quoteSuccessLastDay > 0 ? Healthy : Attention,
        UpdaterDetail = latestQuote is null ? this.Loc["No quote history found."] : $"{latestQuote.Source} · {this.Loc[latestQuote.Status.ToString()]}",
        LastFailureValue = lastFailure is null ? this.Loc["None"] : this.FormatDate(lastFailure.RetrievedAtUtc),
        LastFailureState = lastFailure is null ? Healthy : Attention,
        LastFailureDetail = lastFailure is null ? this.Loc["No failures in last 24 hours."] : this.FormatQuoteFailure(lastFailure),
      };
    }

    private async Task<PostgresRuntimeMetrics> ReadPostgresMetricsAsync(LifePlannerContext db)
    {
      DbConnection connection = db.Database.GetDbConnection();
      if (connection.State != ConnectionState.Open)
      {
        await connection.OpenAsync();
      }

      string databaseName = await this.ReadScalarStringAsync(connection, "SELECT current_database();");
      string version = await this.ReadScalarStringAsync(connection, "SELECT version();");
      long databaseSizeBytes = await this.ReadScalarLongAsync(connection, "SELECT pg_database_size(current_database());");
      long activeConnections = await this.ReadScalarLongAsync(connection, "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = current_database();");
      long maxConnections = await this.ReadScalarLongAsync(connection, "SHOW max_connections;");

      return new PostgresRuntimeMetrics(
        databaseName,
        version,
        this.ExtractPostgresMajorVersion(version),
        databaseSizeBytes,
        activeConnections,
        maxConnections);
    }

    private async Task<string> ReadScalarStringAsync(DbConnection connection, string sql)
    {
      await using DbCommand command = connection.CreateCommand();
      command.CommandText = sql;
      object? value = await command.ExecuteScalarAsync();
      return Convert.ToString(value, this.Loc.Culture) ?? this.Loc[Unknown];
    }

    private async Task<long> ReadScalarLongAsync(DbConnection connection, string sql)
    {
      await using DbCommand command = connection.CreateCommand();
      command.CommandText = sql;
      object? value = await command.ExecuteScalarAsync();
      return Convert.ToInt64(value, this.Loc.Culture);
    }

    private DatabaseStatusSummary BuildUnavailableDatabaseStatus(RepositoryWorkflowStatuses workflows)
    {
      string unknown = this.Loc[Unknown];

      return new DatabaseStatusSummary
      {
        DatabaseCanConnect = false,
        DatabaseTileValue = unknown,
        DatabaseTileDetail = this.Loc["Database unavailable"],
        LiveDatabase = this.DatabaseHealth.IsUsingStandby ? "Neon" : "Supabase",
        DatabaseDetail = this.Loc["Database unavailable"],
        PrimaryValue = "Supabase",
        PrimaryState = this.DatabaseHealth.IsPrimaryReachable ? Healthy : Attention,
        PrimaryDetail = this.DatabaseHealth.IsPrimaryReachable ? this.Loc["Reachable"] : this.Loc["Unreachable"],
        StandbyValue = "Neon",
        StandbyState = this.DatabaseHealth.IsStandbyReachable ? Healthy : Attention,
        StandbyDetail = this.DatabaseHealth.IsStandbyReachable ? this.Loc["Reachable"] : this.Loc["Unreachable"],
        DatabaseProbeValue = this.DatabaseHealth.LastProbeUtc.HasValue ? this.FormatDate(this.DatabaseHealth.LastProbeUtc.Value) : unknown,
        DatabaseProbeState = this.DatabaseHealth.LastProbeUtc.HasValue ? Healthy : Unknown,
        DatabaseProbeDetail = this.DatabaseHealth.IsUsingStandby ? this.Loc["Running on secondary (Neon)"] : this.Loc["Running on primary (Supabase)"],
        PendingMigrationsValue = unknown,
        PendingMigrationsState = Unknown,
        LatestMigration = unknown,
        DatabaseSize = unknown,
        DatabaseStorageDetail = unknown,
        ActiveConnections = unknown,
        ConnectionUsageDetail = unknown,
        PostgresVersion = unknown,
        PostgresVersionDetail = unknown,
        SupabaseQuotaValue = this.Loc["Check dashboard"],
        SupabaseQuotaState = Attention,
        SupabaseQuotaDetail = this.Loc["Supabase quota is a provider-account signal; validate it in the existing Supabase dashboard before restriction dates."],
        LatestQuoteLabel = unknown,
        QuoteTileState = Attention,
        QuoteDetail = this.Loc["No quote history found."],
        QuoteFailureTooltip = this.Loc["Database unavailable"],
        QuoteUpdaterValue = Unknown,
        QuoteUpdaterState = Unknown,
        QuoteUpdaterDetail = unknown,
        LastQuoteFailureValue = unknown,
        LastQuoteFailureState = Unknown,
        LastQuoteFailureDetail = unknown,
        ImapValue = Unknown,
        ImapState = Unknown,
        ImapDetail = unknown,
        AiValue = Unknown,
        AiState = Unknown,
        AiDetail = unknown,
        WidgetValue = Unknown,
        WidgetState = Unknown,
        WidgetDetail = unknown,
        PlanningValue = Unknown,
        PlanningState = Unknown,
        PlanningDetail = unknown,
        BackupTileValue = this.BackupTileValue(workflows.Backup, this.DatabaseHealth.IsStandbyReachable),
        BackupState = this.MergeStates(Attention, workflows.Backup.State),
        BackupTileDetail = this.Loc["Database unavailable"],
        BackupWorkflowValue = workflows.Backup.Value,
        BackupWorkflowState = workflows.Backup.State,
        BackupWorkflowDetail = workflows.Backup.Detail,
        BackupWorkflowUrl = workflows.Backup.Url,
        BackupWorkflowUrlLabel = workflows.Backup.UrlLabel,
        StandbyBackupValue = this.DatabaseHealth.IsStandbyReachable ? this.Loc["Ready"] : this.Loc["Blocked"],
        StandbyBackupDetail = this.DatabaseHealth.IsStandbyReachable ? this.Loc["Neon accepts the backup/sync path now."] : this.Loc["Neon must be reachable before recovery is reliable."],
        RecoveryWorkflowValue = workflows.Recovery.Value,
        RecoveryWorkflowState = workflows.Recovery.State,
        RecoveryWorkflowDetail = workflows.Recovery.Detail,
        RecoveryWorkflowUrl = workflows.Recovery.Url,
        RecoveryWorkflowUrlLabel = workflows.Recovery.UrlLabel,
        RestoreValidationValue = this.Loc["Not tested"],
        RestoreValidationState = Attention,
        RestoreValidationDetail = this.Loc["No restore drill has been executed yet; recovery remains manual and intentionally disabled."],
        CiWorkflowValue = workflows.Ci.Value,
        CiWorkflowState = workflows.Ci.State,
        CiWorkflowDetail = workflows.Ci.Detail,
        CiWorkflowUrl = workflows.Ci.Url,
        CiWorkflowUrlLabel = workflows.Ci.UrlLabel,
      };
    }

    private string BuildQuoteFailureTooltip(IReadOnlyList<MarketQuoteEntity> failures)
    {
      if (failures.Count == 0)
      {
        return this.Loc["No failures in last 24 hours."];
      }

      IEnumerable<string> lines = failures.Select(this.FormatQuoteFailure);
      return $"{this.Loc["Recent quote failures"]}:{System.Environment.NewLine}{string.Join(System.Environment.NewLine, lines)}";
    }

    private string FormatQuoteFailure(MarketQuoteEntity quote)
    {
      string target = string.IsNullOrWhiteSpace(quote.Symbol)
        ? quote.AllocationType.ToString()
        : $"{quote.AllocationType}/{quote.Symbol}";
      string reason = quote.Error ?? quote.Warning ?? quote.Status.ToString();
      return $"{this.FormatDate(quote.RetrievedAtUtc)} · {quote.Source} · {target}: {this.Truncate(reason, 180)}";
    }

    private string ProviderReachability(bool isReachable)
    {
      return isReachable ? this.Loc["Reachable"] : this.Loc["Unreachable"];
    }

    private string ConfiguredValue(params string[] keys)
    {
      return this.HasConfiguration(keys) ? this.Loc[Configured] : this.Loc[Missing];
    }

    private string ConfiguredState(params string[] keys)
    {
      return this.HasConfiguration(keys) ? Configured : Missing;
    }

    private bool HasConfiguration(params string[] keys)
    {
      foreach (string key in keys)
      {
        if (!string.IsNullOrWhiteSpace(this.Configuration[key]))
        {
          return true;
        }
      }

      return false;
    }

    private void TrackProviderKey(List<string> configured, List<string> missing, string label, string key)
    {
      if (this.HasConfiguration(key, $"EnvironmentVariables:{key}"))
      {
        configured.Add(label);
        return;
      }

      missing.Add(label);
    }

    private async Task<RepositoryWorkflowStatuses> ReadRepositoryWorkflowStatusesAsync()
    {
      string? token = this.Configuration["GitHub:Token"]
        ?? this.Configuration["GITHUB_TOKEN"]
        ?? this.Configuration["EnvironmentVariables:GITHUB_TOKEN"];

      if (string.IsNullOrWhiteSpace(token))
      {
        WorkflowRunStatus unavailable = this.GitHubUnavailableStatus(this.Loc["GitHub token missing; live workflow monitoring is disabled."]);
        return new RepositoryWorkflowStatuses(unavailable, unavailable, unavailable);
      }

      HttpClient client = this.HttpClientFactory.CreateClient();
      using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

      WorkflowRunStatus backup = await this.ReadWorkflowRunStatusAsync(client, token, "neon-backup.yml", timeout.Token);
      WorkflowRunStatus recovery = await this.ReadWorkflowRunStatusAsync(client, token, "neon-recovery-sync.yml", timeout.Token, true);
      WorkflowRunStatus ci = await this.ReadWorkflowRunStatusAsync(client, token, "ci.yml", timeout.Token);

      return new RepositoryWorkflowStatuses(backup, recovery, ci);
    }

    private async Task<WorkflowRunStatus> ReadWorkflowRunStatusAsync(
      HttpClient client,
      string token,
      string workflowFile,
      CancellationToken cancellationToken,
      bool manualDisabledIsExpected = false)
    {
      string url = $"{GitHubApiBaseUrl}/actions/workflows/{workflowFile}/runs?per_page=1&branch=master";
      using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.UserAgent.ParseAdd("AppLifePlanner-SystemStatus");
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

      try
      {
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
          string detail = string.Format(
            this.Loc.Culture,
            this.Loc["GitHub monitor returned {0} for {1}."],
            ((int)response.StatusCode).ToString(this.Loc.Culture),
            workflowFile);
          return this.GitHubUnavailableStatus(detail);
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubWorkflowRunsResponse? runs = await JsonSerializer.DeserializeAsync<GitHubWorkflowRunsResponse>(
          body,
          JsonOptions(),
          cancellationToken);
        GitHubWorkflowRun? latest = runs?.WorkflowRuns?.FirstOrDefault();
        if (latest == null)
        {
          if (manualDisabledIsExpected)
          {
            return new WorkflowRunStatus(
              this.Loc["Manual disabled"],
              Attention,
              this.Loc["Manual recovery workflow is intentionally disabled; no restore drill has been executed yet."],
              WorkflowUrl(workflowFile),
              this.Loc["Open workflow"]);
          }

          return new WorkflowRunStatus(
            this.Loc[Unknown],
            Unknown,
            this.Loc["No workflow run found."],
            WorkflowUrl(workflowFile),
            this.Loc["Open workflow"]);
        }

        string? failedStep = await this.ReadFailedWorkflowStepAsync(client, token, latest.JobsUrl, cancellationToken);
        return this.MapWorkflowRunStatus(latest, failedStep, workflowFile);
      }
      catch (OperationCanceledException)
      {
        return this.GitHubUnavailableStatus(this.Loc["GitHub monitor timed out."]);
      }
      catch (HttpRequestException ex)
      {
        return this.GitHubUnavailableStatus($"{this.Loc["GitHub monitor failed"]}: {this.Truncate(ex.Message, 140)}");
      }
      catch (JsonException ex)
      {
        return this.GitHubUnavailableStatus($"{this.Loc["GitHub monitor returned invalid data"]}: {this.Truncate(ex.Message, 140)}");
      }
    }

    private async Task<string?> ReadFailedWorkflowStepAsync(
      HttpClient client,
      string token,
      string jobsUrl,
      CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(jobsUrl))
      {
        return null;
      }

      using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, jobsUrl);
      request.Headers.UserAgent.ParseAdd("AppLifePlanner-SystemStatus");
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

      using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        return null;
      }

      await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
      GitHubWorkflowJobsResponse? jobs = await JsonSerializer.DeserializeAsync<GitHubWorkflowJobsResponse>(
        body,
        JsonOptions(),
        cancellationToken);
      GitHubWorkflowStep? step = jobs?.Jobs?
        .SelectMany(item => item.Steps ?? [])
        .FirstOrDefault(item => string.Equals(item.Conclusion, "failure", StringComparison.OrdinalIgnoreCase));

      return step?.Name;
    }

    private WorkflowRunStatus MapWorkflowRunStatus(GitHubWorkflowRun run, string? failedStep, string workflowFile)
    {
      string state = run.Status switch
      {
        "completed" => string.Equals(run.Conclusion, "success", StringComparison.OrdinalIgnoreCase) ? Healthy : Unavailable,
        "in_progress" or "queued" or "waiting" or "pending" => Attention,
        _ => Unknown,
      };
      string value = run.Status switch
      {
        "completed" when string.Equals(run.Conclusion, "success", StringComparison.OrdinalIgnoreCase) => this.Loc["Success"],
        "completed" when string.Equals(run.Conclusion, "failure", StringComparison.OrdinalIgnoreCase) => this.Loc["failed"],
        "completed" => this.Loc[run.Conclusion ?? Unknown],
        "in_progress" => this.Loc["In progress"],
        "queued" => this.Loc["Queued"],
        _ => this.Loc[Unknown],
      };
      DateTime? referenceDate = run.RunStartedAt ?? run.CreatedAt ?? run.UpdatedAt;
      string title = this.Truncate(run.DisplayTitle ?? workflowFile, 90);
      string detailPrefix = this.WorkflowDetailPrefix(run);
      string detail = referenceDate.HasValue
        ? $"{detailPrefix}: {this.FormatDate(referenceDate.Value)} · {title}"
        : $"{detailPrefix}: {title}";

      if (!string.IsNullOrWhiteSpace(run.HeadSha))
      {
        detail = $"{detail} · {run.HeadSha[..Math.Min(7, run.HeadSha.Length)]}";
      }

      if (!string.IsNullOrWhiteSpace(failedStep))
      {
        detail = $"{detail} · {this.Loc["Failed step"]}: {failedStep}";
      }

      return new WorkflowRunStatus(
        value,
        state,
        detail,
        string.IsNullOrWhiteSpace(run.HtmlUrl) ? WorkflowUrl(workflowFile) : run.HtmlUrl,
        this.Loc["Open run"]);
    }

    private string WorkflowDetailPrefix(GitHubWorkflowRun run)
    {
      if (!string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase))
      {
        return this.Loc["Latest workflow execution"];
      }

      return string.Equals(run.Conclusion, "success", StringComparison.OrdinalIgnoreCase)
        ? this.Loc["Latest successful workflow execution"]
        : this.Loc["Latest workflow execution failed"];
    }

    private WorkflowRunStatus GitHubUnavailableStatus(string detail)
    {
      return new WorkflowRunStatus(this.Loc["GitHub unavailable"], Attention, detail, null, null);
    }

    private string BackupTileValue(WorkflowRunStatus backup, bool isStandbyReachable)
    {
      if (!isStandbyReachable)
      {
        return this.Loc["Check standby"];
      }

      return backup.State switch
      {
        Healthy => this.Loc["Backup healthy"],
        Unavailable => this.Loc["Backup failed"],
        Attention => this.Loc["Monitor unavailable"],
        _ => this.Loc[Unknown],
      };
    }

    private string MergeStates(string first, string second)
    {
      if (first == Unavailable || second == Unavailable)
      {
        return Unavailable;
      }

      if (first == Attention || second == Attention || first == Unknown || second == Unknown)
      {
        return Attention;
      }

      return Healthy;
    }

    private ObservabilitySummary ReadObservabilitySummary(DateTime utcNow)
    {
      IReadOnlyList<string> lines = this.InMemoryLogBuffer.GetRecentLines();
      if (lines.Count == 0)
      {
        return this.BuildUnavailableObservabilitySummary(this.Loc["No log events captured yet in this process. The buffer fills as requests and background jobs run."]);
      }

      List<LogEventRow> events = lines
        .TakeLast(300)
        .Select(line => this.ParseLogEvent(line, utcNow))
        .Where(row => row != null)
        .Cast<LogEventRow>()
        .TakeLast(80)
        .Reverse()
        .ToList();

      int total = events.Count;
      int errors = events.Count(item => item.Level == "ERR" || item.Status == "Failed");
      int warnings = events.Count(item => item.Level == "WRN");
      int slowest = events
        .Where(item => item.ElapsedMs.HasValue)
        .Select(item => item.ElapsedMs!.Value)
        .DefaultIfEmpty(0)
        .Max();

      List<ObservabilityMetric> metrics =
      [
        new ObservabilityMetric("Events read", total.ToString(this.Loc.Culture), Healthy, this.Loc["Recent structured events captured by this running instance."]),
        new ObservabilityMetric("Errors", errors.ToString(this.Loc.Culture), errors == 0 ? Healthy : Unavailable, this.Loc["Error events found in the sampled log window."]),
        new ObservabilityMetric("Warnings", warnings.ToString(this.Loc.Culture), warnings == 0 ? Healthy : Attention, this.Loc["Warning events found in the sampled log window."]),
        new ObservabilityMetric("Slowest operation", $"{slowest.ToString(this.Loc.Culture)} ms", slowest < 5000 ? Healthy : Attention, this.Loc["Highest elapsed time found in structured log properties."]),
      ];

      List<OperationMetric> operations = events
        .GroupBy(item => item.Operation)
        .Select(group =>
        {
          List<int> durations = group.Where(item => item.ElapsedMs.HasValue).Select(item => item.ElapsedMs!.Value).ToList();
          int failures = group.Count(item => item.State == Unavailable);
          string state = failures > 0 ? Unavailable : group.Any(item => item.State == Attention) ? Attention : Healthy;
          string average = durations.Count == 0
            ? "-"
            : $"{durations.Average().ToString("N0", this.Loc.Culture)} ms";
          return new OperationMetric(group.Key, group.Count(), failures, average, state);
        })
        .OrderByDescending(item => item.Failures)
        .ThenByDescending(item => item.Count)
        .Take(8)
        .ToList();

      string state = errors > 0 ? Unavailable : warnings > 0 ? Attention : Healthy;
      return new ObservabilitySummary(
        this.Loc["In-process buffer"],
        state,
        this.Loc["Shows the last events captured by this running instance; the buffer resets on restart or redeploy."],
        metrics,
        operations,
        events.Take(20).ToList());
    }

    private ObservabilitySummary BuildUnavailableObservabilitySummary(string detail)
    {
      List<ObservabilityMetric> metrics =
      [
        new ObservabilityMetric("Events read", "0", Attention, detail),
        new ObservabilityMetric("Errors", "-", Unknown, this.Loc["No local sample is available in this environment."]),
        new ObservabilityMetric("Warnings", "-", Unknown, this.Loc["No local sample is available in this environment."]),
        new ObservabilityMetric("Slowest operation", "-", Unknown, this.Loc["No local sample is available in this environment."]),
      ];

      return new ObservabilitySummary(this.Loc["Console only"], Attention, detail, metrics, [], []);
    }

    private ObservabilitySummary BuildDemoObservabilitySummary(DateTime utcNow)
    {
      List<LogEventRow> events =
      [
        new LogEventRow(this.FormatDate(utcNow.AddMinutes(-3)), "INF", "HostedService", this.Loc["Sample scheduled job"], "Succeeded", "143", Healthy, this.Loc["Simulated scheduled job completed."]),
        new LogEventRow(this.FormatDate(utcNow.AddMinutes(-8)), "WRN", "HostedService", this.Loc["Sample external check"], "Partial", "2180", Attention, this.Loc["Simulated external dependency returned with fallback."]),
        new LogEventRow(this.FormatDate(utcNow.AddMinutes(-16)), "INF", "HTTP", this.Loc["Sample web request"], "Succeeded", "82", Healthy, this.Loc["Simulated web request completed."]),
        new LogEventRow(this.FormatDate(utcNow.AddMinutes(-31)), "INF", "HostedService", this.Loc["Sample health probe"], "Succeeded", "97", Healthy, this.Loc["Simulated health probe completed."]),
      ];
      List<ObservabilityMetric> metrics =
      [
        new ObservabilityMetric("Events read", "64", Healthy, this.Loc["Generic simulated events for the demo status page."]),
        new ObservabilityMetric("Errors", "0", Healthy, this.Loc["No simulated error in the current demo window."]),
        new ObservabilityMetric("Warnings", "1", Attention, this.Loc["One simulated dependency warning."]),
        new ObservabilityMetric("Slowest operation", "2,180 ms", Attention, this.Loc["A simulated dependency check took longer than normal."]),
      ];
      List<OperationMetric> operations =
      [
        new OperationMetric(this.Loc["Sample web request"], 24, 0, "91 ms", Healthy),
        new OperationMetric(this.Loc["Sample external check"], 8, 0, "2,180 ms", Attention),
        new OperationMetric(this.Loc["Sample scheduled job"], 6, 0, "143 ms", Healthy),
        new OperationMetric(this.Loc["Sample health probe"], 6, 0, "97 ms", Healthy),
      ];

      return new ObservabilitySummary(this.Loc["Simulated sample"], Attention, this.Loc["Simulated log metrics show the investigation flow without exposing live operational data."], metrics, operations, events);
    }

    private SystemStatusLine BuildGeoIpLine()
    {
      return this.GeoIpLookup.Status switch
      {
        GeoIpStatus.Available => new SystemStatusLine("GeoIP database", Active, Healthy, this.Loc["Country lookups resolve visitor locations."], null, null, null),
        GeoIpStatus.Missing => new SystemStatusLine("GeoIP database", Missing, Attention, this.Loc["Database missing; visitor countries are not resolved."], null, null, null),
        _ => new SystemStatusLine("GeoIP database", Unknown, Unknown, this.Loc["No lookup has run yet in this process."], null, null, null),
      };
    }

    private async Task<AccessAnalyticsSummary> ReadAccessAnalyticsSummaryAsync(DateTime utcNow)
    {
      await using LifePlannerContext db = await this.DbFactory.CreateDbContextAsync();

      DateTime thirtyDaysAgoUtc = utcNow.AddDays(-30);
      List<AccessEventEntity> last30Days = await db.AccessEvents
        .Where(e => e.CreationDate >= thirtyDaysAgoUtc)
        .ToListAsync();

      DateOnly earliestTrendDate = DateOnly.FromDateTime(utcNow).AddDays(-370);
      List<AccessDailySummaryEntity> dailySummaries = await db.AccessDailySummaries
        .Where(s => s.SummaryDate >= earliestTrendDate)
        .OrderBy(s => s.SummaryDate)
        .ToListAsync();

      if (last30Days.Count == 0 && dailySummaries.Count == 0)
      {
        return BuildEmptyAccessAnalyticsSummary(
          this.Loc["No access data has been recorded yet. Metrics appear once traffic reaches the live application."]);
      }

      DateTime sevenDaysAgoUtc = utcNow.AddDays(-7);
      List<AccessEventEntity> last7Days = last30Days.Where(e => e.CreationDate >= sevenDaysAgoUtc).ToList();
      List<AccessEventEntity> pageViews = last7Days.Where(e => e.Kind == AccessEventKind.PageView).ToList();

      IReadOnlyList<long> sessionDurations = AccessSessionizer.BuildSessionDurationsMs(pageViews);
      string avgSessionLength = sessionDurations.Count > 0
        ? FormatDuration((long)sessionDurations.Average())
        : "-";

      List<AccessTrendPoint> trend = dailySummaries
        .Select(s => new AccessTrendPoint(s.SummaryDate, s.PageViews))
        .ToList();

      return new AccessAnalyticsSummary(
        Healthy,
        this.Loc["See who is accessing the live application, from where, and which pages and features are used the most."],
        AccessSessionizer.CountUniqueVisitors(pageViews),
        sessionDurations.Count,
        pageViews.Count,
        avgSessionLength,
        trend,
        BuildEventRanking(pageViews, e => e.PathOrFeature),
        BuildEventRanking(last7Days.Where(e => e.Kind == AccessEventKind.FeatureUsage).ToList(), e => e.PathOrFeature),
        BuildVisitorRanking(pageViews, e => e.Browser),
        BuildVisitorRanking(pageViews, e => e.DeviceType),
        BuildVisitorRanking(pageViews, e => e.Country));
    }

    private static List<RankedItem> BuildEventRanking(IReadOnlyList<AccessEventEntity> events, Func<AccessEventEntity, string?> selector)
    {
      return events
        .Select(selector)
        .Where(value => !string.IsNullOrEmpty(value))
        .GroupBy(value => value!)
        .Select(group => new RankedItem(group.Key, group.Count()))
        .OrderByDescending(item => item.Count)
        .Take(5)
        .ToList();
    }

    private static List<RankedItem> BuildVisitorRanking(IReadOnlyList<AccessEventEntity> events, Func<AccessEventEntity, string?> selector)
    {
      return events
        .Where(item => !string.IsNullOrEmpty(selector(item)))
        .GroupBy(item => selector(item)!)
        .Select(group => new RankedItem(group.Key, group.Select(AccessSessionizer.VisitorKey).Distinct().Count()))
        .OrderByDescending(item => item.Count)
        .Take(5)
        .ToList();
    }

    private static string FormatDuration(long milliseconds)
    {
      TimeSpan span = TimeSpan.FromMilliseconds(milliseconds);
      return span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s" : $"{span.Seconds}s";
    }

    private static AccessAnalyticsSummary BuildEmptyAccessAnalyticsSummary(string detail)
    {
      return new AccessAnalyticsSummary(Attention, detail, 0, 0, 0, "-", [], [], [], [], [], []);
    }

    private AccessAnalyticsSummary BuildDemoAccessAnalyticsSummary(DateTime utcNow)
    {
      Random random = new Random(utcNow.DayOfYear);
      List<AccessTrendPoint> trend = Enumerable.Range(0, 30)
        .Select(offset => new AccessTrendPoint(DateOnly.FromDateTime(utcNow).AddDays(-29 + offset), random.Next(8, 40)))
        .ToList();

      List<RankedItem> topPages =
      [
        new RankedItem("/hoje", 62),
        new RankedItem("/transactions", 41),
        new RankedItem("/allocations", 27),
        new RankedItem("/planning/okrs", 15),
        new RankedItem("/routine", 9),
      ];
      List<RankedItem> topFeatures =
      [
        new RankedItem("transaction.created", 18),
        new RankedItem("planning.actions_generated", 6),
        new RankedItem("imap.sync_triggered", 4),
      ];
      List<RankedItem> browsers =
      [
        new RankedItem("Chrome 128", 24),
        new RankedItem("Safari 17", 9),
        new RankedItem("Firefox 130", 3),
      ];
      List<RankedItem> deviceTypes = [new RankedItem("Desktop", 27), new RankedItem("Mobile", 9)];
      List<RankedItem> countries = [new RankedItem("BR", 30), new RankedItem("US", 4), new RankedItem("PT", 2)];

      return new AccessAnalyticsSummary(
        Healthy,
        this.Loc["Simulated access analytics for portfolio showcase."],
        18,
        22,
        137,
        "3m 12s",
        trend,
        topPages,
        topFeatures,
        browsers,
        deviceTypes,
        countries);
    }

    private LogEventRow? ParseLogEvent(string line, DateTime fallbackUtc)
    {
      string level = this.ExtractLevel(line);
      string operation = this.ExtractJsonText(line, "Operation") ?? this.InferOperation(line);
      string process = this.ExtractJsonText(line, "Process") ?? (operation.StartsWith("GET ", StringComparison.OrdinalIgnoreCase) ? "HTTP" : "Application");
      string status = this.ExtractJsonText(line, "Status") ?? (level == "ERR" ? "Failed" : "Observed");
      int? elapsedMs = this.ExtractJsonNumber(line, "ElapsedMs");
      string state = level switch
      {
        "ERR" => Unavailable,
        "WRN" => Attention,
        _ => string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ? Unavailable : Healthy,
      };
      string elapsed = elapsedMs.HasValue ? elapsedMs.Value.ToString(this.Loc.Culture) : "-";
      string message = this.ExtractLogMessage(line);

      return new LogEventRow(this.FormatDate(fallbackUtc), level, process, operation, status, elapsed, state, this.Truncate(message, 140))
      {
        ElapsedMs = elapsedMs,
      };
    }

    private string ExtractLevel(string line)
    {
      if (line.Contains(" ERR]", StringComparison.Ordinal))
      {
        return "ERR";
      }

      if (line.Contains(" WRN]", StringComparison.Ordinal))
      {
        return "WRN";
      }

      return "INF";
    }

    private string ExtractLogMessage(string line)
    {
      int close = line.IndexOf("] ", StringComparison.Ordinal);
      string message = close >= 0 ? line[(close + 2)..] : line;
      int propertiesStart = message.IndexOf(" {\"", StringComparison.Ordinal);
      if (propertiesStart > 0)
      {
        message = message[..propertiesStart];
      }

      return message.Trim();
    }

    private string InferOperation(string line)
    {
      if (line.Contains("Market quote", StringComparison.OrdinalIgnoreCase))
      {
        return "Market quote update";
      }

      if (line.Contains("IMAP", StringComparison.OrdinalIgnoreCase))
      {
        return "IMAP sync";
      }

      if (line.Contains("Planning action", StringComparison.OrdinalIgnoreCase))
      {
        return "Planning action generation";
      }

      if (line.Contains("Database health", StringComparison.OrdinalIgnoreCase))
      {
        return "Database health probe";
      }

      return "Application";
    }

    private string? ExtractJsonText(string line, string property)
    {
      string marker = $"\"{property}\":\"";
      int start = line.IndexOf(marker, StringComparison.Ordinal);
      if (start < 0)
      {
        return null;
      }

      start += marker.Length;
      int end = line.IndexOf('"', start);
      return end > start ? line[start..end] : null;
    }

    private int? ExtractJsonNumber(string line, string property)
    {
      string marker = $"\"{property}\":";
      int start = line.IndexOf(marker, StringComparison.Ordinal);
      if (start < 0)
      {
        return null;
      }

      start += marker.Length;
      int end = start;
      while (end < line.Length && char.IsDigit(line[end]))
      {
        end++;
      }

      return int.TryParse(line[start..end], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : null;
    }

    private static JsonSerializerOptions JsonOptions()
    {
      return new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
      };
    }

    private string JoinOrDash(IReadOnlyList<string> values)
    {
      return values.Count == 0 ? "-" : string.Join(", ", values);
    }

    private string ExtractPostgresMajorVersion(string version)
    {
      const string prefix = "PostgreSQL ";
      if (!version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      {
        return this.Loc[Unknown];
      }

      string remainder = version[prefix.Length..];
      int end = remainder.IndexOf(' ', StringComparison.Ordinal);
      return end > 0 ? remainder[..end] : remainder;
    }

    private string FormatBytes(long bytes)
    {
      string[] units = ["B", "KB", "MB", "GB"];
      double value = bytes;
      int unitIndex = 0;
      while (value >= 1024 && unitIndex < units.Length - 1)
      {
        value /= 1024;
        unitIndex++;
      }

      return string.Format(this.Loc.Culture, "{0:N2} {1}", value, units[unitIndex]);
    }

    private string FormatDate(DateTime utc)
    {
      return utc.ToLocalTime().ToString("g", this.Loc.Culture);
    }

    private string Truncate(string value, int maxLength)
    {
      if (value.Length <= maxLength)
      {
        return value;
      }

      return $"{value[..maxLength]}...";
    }

    private sealed record SystemStatusSnapshot(
      IReadOnlyList<SystemStatusTile> TopTiles,
      IReadOnlyList<SystemStatusGroup> Groups,
      ObservabilitySummary Observability,
      AccessAnalyticsSummary AccessAnalytics);

    private sealed record SystemStatusTile(
      string Label,
      string Value,
      string State,
      string? Detail,
      string? Tooltip)
    {
      public string StateLabel
      {
        get
        {
          return State;
        }
      }
    }

    private sealed record SystemStatusGroup(
      string Title,
      string IconCss,
      IReadOnlyList<SystemStatusLine> Lines);

    private sealed record SystemStatusLine(
      string Label,
      string Value,
      string State,
      string? Detail,
      string? Tooltip,
      string? Url,
      string? UrlLabel);

    private sealed record ObservabilitySummary(
      string SourceLabel,
      string State,
      string Detail,
      IReadOnlyList<ObservabilityMetric> Metrics,
      IReadOnlyList<OperationMetric> Operations,
      IReadOnlyList<LogEventRow> Events);

    private sealed record ObservabilityMetric(
      string Label,
      string Value,
      string State,
      string Detail);

    private sealed record OperationMetric(
      string Name,
      int Count,
      int Failures,
      string AverageElapsed,
      string State);

    private sealed record LogEventRow(
      string When,
      string Level,
      string Process,
      string Operation,
      string Status,
      string Elapsed,
      string State,
      string Message)
    {
      public int? ElapsedMs { get; init; }
    }

    private sealed record RuntimeStatusSummary(
      string EnvironmentName,
      string BaseUri,
      string ProcessDetail,
      string RuntimeDetail);

    private sealed record AccessAnalyticsSummary(
      string State,
      string Detail,
      int UniqueVisitors,
      int Sessions,
      int PageViews,
      string AvgSessionLength,
      IReadOnlyList<AccessTrendPoint> Trend,
      IReadOnlyList<RankedItem> TopPages,
      IReadOnlyList<RankedItem> TopFeatures,
      IReadOnlyList<RankedItem> Browsers,
      IReadOnlyList<RankedItem> DeviceTypes,
      IReadOnlyList<RankedItem> Countries);

    private readonly record struct AccessTrendPoint(DateOnly Date, int PageViews);

    private sealed record RankedItem(string Label, int Count);

    private sealed record ConfigurationStatusSummary(
      string SmtpValue,
      string SmtpState,
      string SmtpDetail,
      string PublicRegistrationValue,
      string PublicRegistrationState,
      string KeyedProviderValue,
      string KeyedProviderState,
      string KeyedProviderDetail);

    private sealed record PostgresRuntimeMetrics(
      string DatabaseName,
      string Version,
      string PostgresMajorVersion,
      long DatabaseSizeBytes,
      long ActiveConnections,
      long MaxConnections);

    private sealed record RepositoryWorkflowStatuses(
      WorkflowRunStatus Backup,
      WorkflowRunStatus Recovery,
      WorkflowRunStatus Ci);

    private sealed record WorkflowRunStatus(
      string Value,
      string State,
      string Detail,
      string? Url,
      string? UrlLabel);

    private sealed class GitHubWorkflowRunsResponse
    {
      [JsonPropertyName("workflow_runs")]
      public IReadOnlyList<GitHubWorkflowRun>? WorkflowRuns { get; set; }
    }

    private sealed class GitHubWorkflowRun
    {
      [JsonPropertyName("status")]
      public string? Status { get; set; }

      [JsonPropertyName("conclusion")]
      public string? Conclusion { get; set; }

      [JsonPropertyName("display_title")]
      public string? DisplayTitle { get; set; }

      [JsonPropertyName("html_url")]
      public string? HtmlUrl { get; set; }

      [JsonPropertyName("jobs_url")]
      public string JobsUrl { get; set; } = string.Empty;

      [JsonPropertyName("head_sha")]
      public string? HeadSha { get; set; }

      [JsonPropertyName("created_at")]
      public DateTime? CreatedAt { get; set; }

      [JsonPropertyName("updated_at")]
      public DateTime? UpdatedAt { get; set; }

      [JsonPropertyName("run_started_at")]
      public DateTime? RunStartedAt { get; set; }
    }

    private sealed class GitHubWorkflowJobsResponse
    {
      [JsonPropertyName("jobs")]
      public IReadOnlyList<GitHubWorkflowJob>? Jobs { get; set; }
    }

    private sealed class GitHubWorkflowJob
    {
      [JsonPropertyName("steps")]
      public IReadOnlyList<GitHubWorkflowStep>? Steps { get; set; }
    }

    private sealed class GitHubWorkflowStep
    {
      [JsonPropertyName("name")]
      public string? Name { get; set; }

      [JsonPropertyName("conclusion")]
      public string? Conclusion { get; set; }
    }

    private sealed class QuoteStatusSummary
    {
      public string LatestQuoteLabel { get; set; } = string.Empty;

      public string TileState { get; set; } = Unknown;

      public string Detail { get; set; } = string.Empty;

      public string FailureTooltip { get; set; } = string.Empty;

      public string UpdaterValue { get; set; } = Unknown;

      public string UpdaterState { get; set; } = Unknown;

      public string UpdaterDetail { get; set; } = string.Empty;

      public string LastFailureValue { get; set; } = string.Empty;

      public string LastFailureState { get; set; } = Unknown;

      public string LastFailureDetail { get; set; } = string.Empty;
    }

    private sealed class DatabaseStatusSummary
    {
      public bool DatabaseCanConnect { get; set; }

      public string DatabaseTileValue { get; set; } = string.Empty;

      public string DatabaseTileDetail { get; set; } = string.Empty;

      public string LiveDatabase { get; set; } = string.Empty;

      public string DatabaseDetail { get; set; } = string.Empty;

      public string PrimaryValue { get; set; } = string.Empty;

      public string PrimaryState { get; set; } = Unknown;

      public string PrimaryDetail { get; set; } = string.Empty;

      public string StandbyValue { get; set; } = string.Empty;

      public string StandbyState { get; set; } = Unknown;

      public string StandbyDetail { get; set; } = string.Empty;

      public string DatabaseProbeValue { get; set; } = string.Empty;

      public string DatabaseProbeState { get; set; } = Unknown;

      public string DatabaseProbeDetail { get; set; } = string.Empty;

      public string PendingMigrationsValue { get; set; } = string.Empty;

      public string PendingMigrationsState { get; set; } = Unknown;

      public string LatestMigration { get; set; } = string.Empty;

      public string DatabaseSize { get; set; } = string.Empty;

      public string DatabaseStorageDetail { get; set; } = string.Empty;

      public string ActiveConnections { get; set; } = string.Empty;

      public string ConnectionUsageDetail { get; set; } = string.Empty;

      public string PostgresVersion { get; set; } = string.Empty;

      public string PostgresVersionDetail { get; set; } = string.Empty;

      public string SupabaseQuotaValue { get; set; } = string.Empty;

      public string SupabaseQuotaState { get; set; } = Unknown;

      public string SupabaseQuotaDetail { get; set; } = string.Empty;

      public string LatestQuoteLabel { get; set; } = string.Empty;

      public string QuoteTileState { get; set; } = Unknown;

      public string QuoteDetail { get; set; } = string.Empty;

      public string QuoteFailureTooltip { get; set; } = string.Empty;

      public string QuoteUpdaterValue { get; set; } = string.Empty;

      public string QuoteUpdaterState { get; set; } = Unknown;

      public string QuoteUpdaterDetail { get; set; } = string.Empty;

      public string LastQuoteFailureValue { get; set; } = string.Empty;

      public string LastQuoteFailureState { get; set; } = Unknown;

      public string LastQuoteFailureDetail { get; set; } = string.Empty;

      public string ImapValue { get; set; } = string.Empty;

      public string ImapState { get; set; } = Unknown;

      public string ImapDetail { get; set; } = string.Empty;

      public string AiValue { get; set; } = string.Empty;

      public string AiState { get; set; } = Unknown;

      public string AiDetail { get; set; } = string.Empty;

      public string WidgetValue { get; set; } = string.Empty;

      public string WidgetState { get; set; } = Unknown;

      public string WidgetDetail { get; set; } = string.Empty;

      public string PlanningValue { get; set; } = string.Empty;

      public string PlanningState { get; set; } = Unknown;

      public string PlanningDetail { get; set; } = string.Empty;

      public string BackupTileValue { get; set; } = string.Empty;

      public string BackupState { get; set; } = Unknown;

      public string BackupTileDetail { get; set; } = string.Empty;

      public string BackupWorkflowValue { get; set; } = string.Empty;

      public string BackupWorkflowState { get; set; } = Unknown;

      public string BackupWorkflowDetail { get; set; } = string.Empty;

      public string? BackupWorkflowUrl { get; set; }

      public string? BackupWorkflowUrlLabel { get; set; }

      public string StandbyBackupValue { get; set; } = string.Empty;

      public string StandbyBackupDetail { get; set; } = string.Empty;

      public string RecoveryWorkflowValue { get; set; } = string.Empty;

      public string RecoveryWorkflowState { get; set; } = Unknown;

      public string RecoveryWorkflowDetail { get; set; } = string.Empty;

      public string? RecoveryWorkflowUrl { get; set; }

      public string? RecoveryWorkflowUrlLabel { get; set; }

      public string RestoreValidationValue { get; set; } = string.Empty;

      public string RestoreValidationState { get; set; } = Unknown;

      public string RestoreValidationDetail { get; set; } = string.Empty;

      public string CiWorkflowValue { get; set; } = string.Empty;

      public string CiWorkflowState { get; set; } = Unknown;

      public string CiWorkflowDetail { get; set; } = string.Empty;

      public string? CiWorkflowUrl { get; set; }

      public string? CiWorkflowUrlLabel { get; set; }
    }
  }
}
