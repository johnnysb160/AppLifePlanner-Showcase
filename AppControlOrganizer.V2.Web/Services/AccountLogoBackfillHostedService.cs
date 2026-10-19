//-----------------------------------------------------------------------
// <copyright file="AccountLogoBackfillHostedService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Logging;
using System.Globalization;

/// <summary>
/// Background service that fills missing account logos on startup. Best-effort: failures are logged but do not crash
/// the application.
/// </summary>
public class AccountLogoBackfillHostedService : BackgroundService
{
    private readonly IAccountRepository accountRepository;
    private readonly AccountLogoResolver resolver;
    private readonly ILogger<AccountLogoBackfillHostedService> logger;

    /// <summary>
    /// Initializes a new instance of the AccountLogoBackfillHostedService class with the specified account repository,
    /// logo resolver, and logger.
    /// </summary>
    /// <param name="accountRepository">The repository used to access and update account data.</param>
    /// <param name="resolver">The resolver used to determine the appropriate logo for each account.</param>
    /// <param name="logger">The logger used to record operational and error information for the service.</param>
    public AccountLogoBackfillHostedService(IAccountRepository accountRepository,
                                            AccountLogoResolver resolver,
                                            ILogger<AccountLogoBackfillHostedService> logger)
    {
        this.accountRepository = accountRepository;
        this.resolver = resolver;
        this.logger = logger;
    }

    /// <summary>
    /// Executes the background logo backfill operation until the service is stopped.
    /// </summary>
    /// <remarks>This method is called by the host to run the background service. It delays briefly to allow
    /// application startup to complete before beginning the backfill process. Exceptions are handled internally to
    /// prevent the application from crashing.</remarks>
    /// <param name="stoppingToken">A cancellation token that is triggered when the host is performing a graceful shutdown. The operation should
    /// monitor this token to stop execution promptly when cancellation is requested.</param>
    /// <returns>A task that represents the asynchronous execution of the background operation.</returns>
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // Small delay to let app startup complete. Avoid throwing on startup cancellation because Visual Studio can break
    // on first-chance TaskCanceledException before the hosted service reaches its expected shutdown handling.
    Task startupDelay = Task.Delay(TimeSpan.FromSeconds(3));
    Task cancellationDelay = Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    Task completedDelay = await Task.WhenAny(startupDelay, cancellationDelay);

    if (completedDelay != startupDelay || stoppingToken.IsCancellationRequested)
    {
      return;
    }

    try
    {
      await BackfillLogosAsync(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      // Expected on shutdown
    }
    catch (Exception ex)
    {
      // Log and continue - never crash the app
      this.logger.LogError(ex, "Logo backfill failed unexpectedly");
    }
  }

    /// <summary>
    /// Backfills missing logo data for accounts that do not have a logo assigned.
    /// </summary>
    /// <remarks>Accounts that already have a logo are not affected. If the operation is canceled via the
    /// provided token, processing stops and only accounts processed up to that point are updated.</remarks>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous backfill operation.</returns>
    private async Task BackfillLogosAsync(CancellationToken ct)
    {
        List<AccountEntity>? accountsOrNull = await this.accountRepository.SelectAsync(ct);
        List<AccountEntity> accounts = accountsOrNull ?? new List<AccountEntity>();
        List<AccountEntity> accountsWithoutLogo = accounts.Where(a => string.IsNullOrEmpty(a.Logo)).ToList();

        if (accountsWithoutLogo.Count == 0)
        {
            this.logger.LogInformation("No accounts require logo backfill");
            return;
        }

        this.logger.LogInformation("Starting logo backfill for {Count} accounts", accountsWithoutLogo.Count);

        int updated = 0;
        foreach (AccountEntity account in accountsWithoutLogo)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                string logo = await this.resolver.ResolveLogoDataUriAsync(account, ct);
                account.Logo = logo;
                updated++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                  ex,
                  "Failed to resolve logo for account {AccountIdHash}",
                  LogSafe.Hash(account.Id.ToString(CultureInfo.InvariantCulture)));
                account.Logo = Helper.UnknowLogoDataUri;
            }

            await this.accountRepository.UpdateAsync(account);
        }

        this.logger.LogInformation("Logo backfill completed: {Updated} accounts updated", updated);
    }
}
