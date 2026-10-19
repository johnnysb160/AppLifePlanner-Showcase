//-----------------------------------------------------------------------
// <copyright file="CreditCardRepository.cs" company="AppLifePlanner">
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
/// Provides data access operations for credit card entities within the LifePlanner application.
/// </summary>
public class CreditCardRepository : BaseRepository<CreditCardEntity, LifePlannerContext>, ICreditCardRepository
{
  /// <summary>
  /// The encryption service used to encrypt and decrypt credit card data.
  /// </summary>
  private readonly IEncryptionService encryptionService;

  /// <summary>
  /// Initializes a new instance of the CreditCardRepository class with the specified database context factory and
  /// encryption service.
  /// </summary>
  public CreditCardRepository(IDbContextFactory<LifePlannerContext> contextFactory, IEncryptionService encryptionService) : base(contextFactory)
  {
    this.encryptionService = encryptionService;
  }

  /// <summary>
  /// Returns all credit cards including soft-deleted ones. Used by maintenance/background tasks.
  /// </summary>
  public async Task<List<CreditCardEntity>?> SelectAllIncludingDeletedAsync(CancellationToken cancellationToken = default)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    return await dbContext.CreditCards.IgnoreQueryFilters().ToListAsync(cancellationToken);
  }

  /// <summary>
  /// Asynchronously retrieves a list of credit card entities, including their related account, ordered by display order.
  /// </summary>
  public async Task<List<CreditCardEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    DbSet<CreditCardEntity> dataset = dbContext.Set<CreditCardEntity>();
    List<CreditCardEntity> list = await dataset.Where(c => !c.Deleted)
      .Include(c => c.Account)
      .OrderBy(c => c.DisplayOrder)
      .ToListAsync(cancellationToken);

    if (list != null)
    {
      foreach (CreditCardEntity c in list)
      {
        if (c == null)
        {
          continue;
        }

        c.HolderName = encryptionService.DecryptSafe(c.HolderName) ?? string.Empty;
        c.Number = encryptionService.DecryptSafe(c.Number);
      }
    }

    return list;
  }

  /// <summary>
  /// Retrieves credit cards filtered by account ID directly in the database query,
  /// avoiding the need to load all cards and filter in memory.
  /// </summary>
  public async Task<List<CreditCardEntity>?> SelectByAccountAsync(int accountId, CancellationToken cancellationToken = default)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    DbSet<CreditCardEntity> dataset = dbContext.Set<CreditCardEntity>();
    List<CreditCardEntity> list = await dataset
      .Where(c => !c.Deleted && c.AccountId == accountId)
      .Include(c => c.Account)
      .OrderBy(c => c.DisplayOrder)
      .ToListAsync(cancellationToken);

    foreach (CreditCardEntity c in list)
    {
      c.HolderName = encryptionService.DecryptSafe(c.HolderName) ?? string.Empty;
      c.Number = encryptionService.DecryptSafe(c.Number);
    }

    return list;
  }

  /// <summary>
  /// Override select by id to decrypt sensitive values before returning to caller.
  /// </summary>
  public override async Task<CreditCardEntity?> SelectAsync(long id)
  {
    CreditCardEntity? card = await base.SelectAsync(id);
    if (card != null)
    {
      card.Number = encryptionService.DecryptSafe(card.Number);
      card.SecurityCode = encryptionService.DecryptSafe(card.SecurityCode);
    }

    return card;
  }

  /// <summary>
  /// Ensure sensitive fields are encrypted before insert.
  /// </summary>
  public override async Task InsertAsync(CreditCardEntity item)
  {
    // Ensure HolderName is encrypted before insert. Other sensitive fields use model converters.
    item.HolderName = encryptionService.EncryptSafe(item.HolderName) ?? string.Empty;
    await base.InsertAsync(item);
  }

  /// <summary>
  /// Ensure sensitive fields are encrypted before update.
  /// </summary>
  public override async Task<CreditCardEntity> UpdateAsync(CreditCardEntity item)
  {
    // Ensure HolderName is encrypted before update. Other sensitive fields use model converters.
    item.HolderName = encryptionService.EncryptSafe(item.HolderName) ?? string.Empty;
    return await base.UpdateAsync(item);
  }

  /// <summary>
  /// Marks the specified credit card as deleted and inactive asynchronously.
  /// </summary>
  public override async Task<CreditCardEntity> DeleteAsync(long id)
  {
    using LifePlannerContext dbContext = await CreateContextAsync();
    DbSet<CreditCardEntity> dataset = dbContext.Set<CreditCardEntity>();
    CreditCardEntity? card = await dataset.SingleOrDefaultAsync(c => c.Id == id);
    if (card == null)
    {
      return Activator.CreateInstance<CreditCardEntity>();
    }

    card.Deleted = true;
    card.Active = false;

    await dbContext.SaveChangesAsync();

    return card;
  }
}
