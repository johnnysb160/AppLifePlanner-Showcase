//-----------------------------------------------------------------------
// <copyright file="FakeRepositories.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using System.Linq.Expressions;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

/// <summary>
/// In-memory account repository for process tests.
/// </summary>
internal sealed class FakeAccountRepository : IAccountRepository
{
  private readonly List<AccountEntity> accounts;

  public FakeAccountRepository(IEnumerable<AccountEntity>? accounts = null)
  {
    this.accounts = accounts?.ToList() ?? new List<AccountEntity>();
  }

  public Task<List<AccountEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult<List<AccountEntity>?>(this.accounts.Where(item => !item.Deleted).ToList());
  }

  public Task<AccountEntity?> SelectAsync(long id)
  {
    return Task.FromResult(this.accounts.FirstOrDefault(item => item.Id == id && !item.Deleted));
  }

  public Task<ICollection<AccountEntity>> SelectAsync()
  {
    return Task.FromResult<ICollection<AccountEntity>>(this.accounts.Where(item => !item.Deleted).ToList());
  }

  public Task<ICollection<AccountEntity>?> SelectAsync(Expression<Func<AccountEntity, bool>> condition)
  {
    return Task.FromResult<ICollection<AccountEntity>?>(
      this.accounts.AsQueryable().Where(condition).Where(item => !item.Deleted).ToList());
  }

  public Task<ICollection<AccountEntity>> SelectAsync(
    Expression<Func<AccountEntity, bool>> condition,
    int pageNumber,
    int pageSize)
  {
    return Task.FromResult<ICollection<AccountEntity>>(
      this.accounts.AsQueryable().Where(condition).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList());
  }

  public Task<AccountEntity?> SelectOneAsync(
    Expression<Func<AccountEntity, bool>> condition,
    CancellationToken cancellationToken)
  {
    return Task.FromResult(this.accounts.AsQueryable().Where(condition).FirstOrDefault(item => !item.Deleted));
  }

  public Task<int> SelectCountAsync(
    CancellationToken cancellationToken,
    Expression<Func<AccountEntity, bool>>? condition = null)
  {
    IQueryable<AccountEntity> query = this.accounts.AsQueryable().Where(item => !item.Deleted);
    if (condition != null)
    {
      query = query.Where(condition);
    }

    return Task.FromResult(query.Count());
  }

  public Task<AccountEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.accounts.FirstOrDefault(item => !item.Deleted));
  }

  public Task InsertAsync(AccountEntity entity)
  {
    entity.Id = entity.Id == 0 ? this.accounts.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1 : entity.Id;
    this.accounts.Add(entity);
    return Task.CompletedTask;
  }

  public Task<AccountEntity> UpdateAsync(AccountEntity item)
  {
    return Task.FromResult(item);
  }

  public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.accounts.Any(item => item.Id == id && !item.Deleted));
  }

  public Task<AccountEntity> DeleteAsync(long id)
  {
    AccountEntity entity = this.accounts.First(item => item.Id == id);
    entity.Deleted = true;
    return Task.FromResult(entity);
  }
}

/// <summary>
/// In-memory transaction repository for process tests.
/// </summary>
internal sealed class FakeTransactionRepository : ITransactionRepository
{
  private readonly List<TransactionEntity> transactions;

  public FakeTransactionRepository(IEnumerable<TransactionEntity>? transactions = null)
  {
    this.transactions = transactions?.ToList() ?? new List<TransactionEntity>();
  }

  public IReadOnlyList<TransactionEntity> Transactions
  {
    get
    {
      return this.transactions;
    }
  }

  public Task<List<TransactionEntity>?> SelectAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult<List<TransactionEntity>?>(this.transactions.Where(item => !item.Deleted).ToList());
  }

  public Task<DateTime?> SelectMostRecentDateAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions.Count == 0 ? null : this.transactions.Max(item => (DateTime?)item.Date));
  }

  public Task<List<TransactionEntity>?> SelectByDateRangeAsync(
    DateTime startDate,
    DateTime endDate,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult<List<TransactionEntity>?>(
      this.transactions.Where(item => item.Date >= startDate && item.Date <= endDate && !item.Deleted).ToList());
  }

  public Task<List<TransactionEntity>?> SelectByCategoryAsync(
    int categoryId,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult<List<TransactionEntity>?>(
      this.transactions.Where(item => item.CategoryId == categoryId && !item.Deleted).ToList());
  }

  public Task<List<TransactionEntity>?> SelectByCategoryEnumAsync(
    Category category,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult<List<TransactionEntity>?>(
      this.transactions.Where(item => item.Category == category && !item.Deleted).ToList());
  }

  public Task<Dictionary<int, int>> GetCategoryUsageCountsByIdAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions
      .Where(item => item.CategoryId.HasValue && !item.Deleted)
      .GroupBy(item => item.CategoryId!.Value)
      .ToDictionary(group => group.Key, group => group.Count()));
  }

  public Task<Dictionary<int, int>> GetCategoryUsageCountsByEnumAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions
      .Where(item => !item.CategoryId.HasValue && !item.Deleted)
      .GroupBy(item => (int)item.Category)
      .ToDictionary(group => group.Key, group => group.Count()));
  }

  public Task<int> GetUsageCountForCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions.Count(item => item.CategoryId == categoryId && !item.Deleted));
  }

  public Task<TransactionEntity?> SelectAsync(long id)
  {
    return Task.FromResult(this.transactions.FirstOrDefault(item => item.Id == id && !item.Deleted));
  }

  public Task<ICollection<TransactionEntity>> SelectAsync()
  {
    return Task.FromResult<ICollection<TransactionEntity>>(this.transactions.Where(item => !item.Deleted).ToList());
  }

  public Task<ICollection<TransactionEntity>?> SelectAsync(Expression<Func<TransactionEntity, bool>> condition)
  {
    return Task.FromResult<ICollection<TransactionEntity>?>(
      this.transactions.AsQueryable().Where(condition).Where(item => !item.Deleted).ToList());
  }

  public Task<ICollection<TransactionEntity>> SelectAsync(
    Expression<Func<TransactionEntity, bool>> condition,
    int pageNumber,
    int pageSize)
  {
    return Task.FromResult<ICollection<TransactionEntity>>(
      this.transactions.AsQueryable().Where(condition).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList());
  }

  public Task<TransactionEntity?> SelectOneAsync(
    Expression<Func<TransactionEntity, bool>> condition,
    CancellationToken cancellationToken)
  {
    return Task.FromResult(this.transactions.AsQueryable().Where(condition).FirstOrDefault(item => !item.Deleted));
  }

  public Task<int> SelectCountAsync(
    CancellationToken cancellationToken,
    Expression<Func<TransactionEntity, bool>>? condition = null)
  {
    IQueryable<TransactionEntity> query = this.transactions.AsQueryable().Where(item => !item.Deleted);
    if (condition != null)
    {
      query = query.Where(condition);
    }

    return Task.FromResult(query.Count());
  }

  public Task<TransactionEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions.FirstOrDefault(item => !item.Deleted));
  }

  public Task InsertAsync(TransactionEntity entity)
  {
    entity.Id = entity.Id == 0 ? this.transactions.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1 : entity.Id;
    this.transactions.Add(entity);
    return Task.CompletedTask;
  }

  public Task<TransactionEntity> UpdateAsync(TransactionEntity item)
  {
    return Task.FromResult(item);
  }

  public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.transactions.Any(item => item.Id == id && !item.Deleted));
  }

  public Task<TransactionEntity> DeleteAsync(long id)
  {
    TransactionEntity entity = this.transactions.First(item => item.Id == id);
    entity.Deleted = true;
    return Task.FromResult(entity);
  }
}
