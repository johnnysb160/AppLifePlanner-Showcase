//-----------------------------------------------------------------------
// <copyright file="AccountRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Repositories;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Repositories.Base;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides data access operations for account entities within the LifePlanner application.
/// </summary>
/// <remarks>This repository encapsulates methods for retrieving and managing account data using Entity Framework
/// Core. It is typically used to abstract database interactions and should be instantiated with a valid context
/// factory. Thread safety depends on the underlying context factory implementation.</remarks>
public class AccountRepository : BaseRepository<AccountEntity, LifePlannerContext>, IAccountRepository
{
  private readonly IEncryptionService encryptionService;

  /// <summary>
  /// Initializes a new instance of the AccountRepository class.
  /// </summary>
  public AccountRepository(IDbContextFactory<LifePlannerContext> contextFactory, IEncryptionService encryptionService, Microsoft.Extensions.Logging.ILogger<AccountRepository>? logger = null, AppControlOrganizer.V2.Web.Helpers.ConfigurationsValues? cfg = null)
    : base(contextFactory, logger, cfg)
  {
    this.encryptionService = encryptionService;
  }

  /// <summary>
  /// Asynchronously retrieves a list of account entities, including their non-deleted allocations and cards, ordered by
  /// display order.
  /// </summary>
  /// <remarks>The returned list includes only allocations and cards that are not marked as deleted. The operation
  /// is performed using a new database context instance.</remarks>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of account entities with related
  /// allocations and cards included. Returns null if no accounts are found.</returns>
  public async Task<List<AccountEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return await ExecuteWithRetryAsync(async dbContext =>
    {
      DbSet<AccountEntity> dataset = dbContext.Set<AccountEntity>();
      // Read-only query: avoid change tracking to reduce overhead when only reading data
      List<AccountEntity> list = await dataset.AsNoTracking()
        .Include(a => a.Allocations.Where(al => !al.Deleted))
        .Include(a => a.Cards.Where(c => !c.Deleted))
        .OrderBy(a => a.DisplayOrder).ToListAsync(cancellationToken);

      // Decrypt sensitive fields for UI consumption
      foreach (AccountEntity a in list)
      {
        if (a != null)
        {
          a.NumberAccount = encryptionService.DecryptSafe(a.NumberAccount);
          a.Agency = encryptionService.DecryptSafe(a.Agency);
          a.BankCode = encryptionService.DecryptSafe(a.BankCode);
        }
      }

      return list;
    }, cancellationToken);
  }

  /// <summary>
  /// Selects an account by its identifier and decrypts sensitive fields.
  /// </summary>
  public override async Task<AccountEntity?> SelectAsync(long id)
  {
    AccountEntity? account = await base.SelectAsync(id);
    if (account != null)
    {
      account.NumberAccount = encryptionService.DecryptSafe(account.NumberAccount);
      account.Agency = encryptionService.DecryptSafe(account.Agency);
      account.BankCode = encryptionService.DecryptSafe(account.BankCode);
    }

    return account;
  }

  /// <summary>
  /// Marks the specified account and its related allocations and cards as deleted asynchronously.
  /// </summary>
  /// <remarks>This method performs a soft delete by setting the Deleted property to <see langword="true"/> and
  /// the Active property to <see langword="false"/> on the account and its related allocations and cards. The account
  /// and related entities remain in the database but are marked as deleted.</remarks>
  /// <param name="id">The unique identifier of the account to delete.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the updated account entity with its
  /// deleted and active status set accordingly. If no account with the specified identifier exists, returns a new
  /// instance of <see cref="AccountEntity"/>.</returns>
  public override async Task<AccountEntity> DeleteAsync(long id)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    DbSet<AccountEntity> dataset = dbContext.Set<AccountEntity>();
    AccountEntity? account = await dataset.Include(a => a.Allocations)
      .Include(a => a.Cards)
      .SingleOrDefaultAsync(a => a.Id == id);
    if (account == null)
    {
      return Activator.CreateInstance<AccountEntity>();
    }

    account.Deleted = true;
    account.Active = false;

    foreach (AllocationEntity alloc in account.Allocations)
    {
      alloc.Deleted = true;
      alloc.Active = false;
    }

    foreach (CreditCardEntity card in account.Cards)
    {
      card.Deleted = true;
      card.Active = false;
    }

    await dbContext.SaveChangesAsync();

    return account;
  }

  /// <summary>
  /// Ensure sensitive account fields are encrypted before insert.
  /// </summary>
  public override async Task InsertAsync(AccountEntity item)
  {
    item.NumberAccount = encryptionService.EncryptSafe(item.NumberAccount);
    item.Agency = encryptionService.EncryptSafe(item.Agency);
    item.BankCode = encryptionService.EncryptSafe(item.BankCode);
    await base.InsertAsync(item);
  }

  /// <summary>
  /// Ensure sensitive account fields are encrypted before update.
  /// </summary>
  public override async Task<AccountEntity> UpdateAsync(AccountEntity item)
  {
    item.NumberAccount = encryptionService.EncryptSafe(item.NumberAccount);
    item.Agency = encryptionService.EncryptSafe(item.Agency);
    item.BankCode = encryptionService.EncryptSafe(item.BankCode);
    return await base.UpdateAsync(item);
  }
}
