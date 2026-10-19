//-----------------------------------------------------------------------
// <copyright file="FinancialSyncService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Recomputes <see cref="AccountEntity.Balance"/> and <see cref="AccountEntity.TotalInvested"/>
/// for a given account whenever a transaction or allocation is created, updated, or deleted.
/// </summary>
public sealed class FinancialSyncService
{
	private readonly IDbContextFactory<LifePlannerContext> contextFactory;
	private readonly MarketQuoteCache quoteCache;
	private readonly AllocationQuotePricingService pricingService;
  private readonly IFinancialFactsService financialFactsService;

	/// <summary>
	/// Initializes a new instance of <see cref="FinancialSyncService"/>.
	/// </summary>
	public FinancialSyncService(
		IDbContextFactory<LifePlannerContext> contextFactory,
		MarketQuoteCache quoteCache,
    AllocationQuotePricingService pricingService,
    IFinancialFactsService financialFactsService)
	{
		this.contextFactory = contextFactory;
		this.quoteCache = quoteCache;
		this.pricingService = pricingService;
    this.financialFactsService = financialFactsService;
	}

	/// <summary>
	/// Recomputes and persists <see cref="AccountEntity.Balance"/> and
	/// <see cref="AccountEntity.TotalInvested"/> for the specified account.
	/// No-ops silently when <paramref name="accountId"/> is null or the account no longer exists.
	/// </summary>
	/// <param name="accountId">The account to sync. Null values are ignored.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task SyncAccountAsync(int? accountId, CancellationToken cancellationToken = default)
	{
		if (accountId is null)
		{
			return;
		}

		await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(cancellationToken);

		AccountEntity? account = await db.Accounts
			.FirstOrDefaultAsync(a => a.Id == accountId.Value, cancellationToken);

		if (account is null)
		{
			return;
		}

    string? ownerUserId = db.Entry(account).Property<string>("OwnerUserId").CurrentValue;

    // The showcase tenant is read-only for requests; only the demonstration maintenance run refreshes it.
    if (ownerUserId == LifePlannerContext.DemoUserId && !LifePlannerContext.IsDemoWriteScopeActive)
    {
      return;
    }

    // 1. Loads the latest quote dictionary from the cache
    IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> quotes =
      await quoteCache.GetLatestSuccessQuotesAsync(cancellationToken);

    // BuyUnitValue and Quantity are encrypted as TEXT — load into memory so EF decrypts via ValueConverter.
    List<AllocationEntity> allocations = await db.Allocations
      .Where(a => a.AccountId == accountId.Value && !a.Deleted)
      .ToListAsync(cancellationToken);

    List<TransactionEntity> transactions = await db.Transactions
      .Where(t => t.AccountId == accountId.Value && !t.Deleted)
      .ToListAsync(cancellationToken);
    FinancialAccountOperationalPosition operationalPosition =
      FinancialPositionCalculator.CalculateOperationalAccountPosition(
        account,
        allocations,
        transactions,
        quotes,
        pricingService,
        FinancialFactsService.ResolvePurpose);

    if (account.BalanceMode != AccountBalanceMode.ProviderBased)
    {
      account.Balance = operationalPosition.Balance;
    }

    account.TotalInvested = operationalPosition.TotalInvested;

    if (!string.IsNullOrWhiteSpace(ownerUserId))
    {
      await ApplyAutomaticTransactionClassificationsAsync(db, ownerUserId, cancellationToken);
    }

    await db.SaveChangesAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(ownerUserId))
    {
      this.financialFactsService.Invalidate(ownerUserId);
      await this.financialFactsService.CaptureSnapshotAsync(
        ownerUserId,
        DateOnly.FromDateTime(DateTime.Now),
        null,
        cancellationToken);
    }
  }

  private static async Task ApplyAutomaticTransactionClassificationsAsync(
    LifePlannerContext db,
    string ownerUserId,
    CancellationToken cancellationToken)
  {
    List<TransactionEntity> transactions = await db.Transactions
      .IgnoreQueryFilters()
      .Where(transaction => EF.Property<string>(transaction, "OwnerUserId") == ownerUserId
        && !transaction.Deleted)
      .ToListAsync(cancellationToken);
    HashSet<int> internalTransfers = FinancialFactsService.DetectInternalTransfers(transactions);

    foreach (TransactionEntity transaction in transactions)
    {
      FinancialFactsService.ApplyAutomaticClassification(transaction, internalTransfers);
    }
  }
}
