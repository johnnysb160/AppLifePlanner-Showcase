//-----------------------------------------------------------------------
// <copyright file="CategoryService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Service for managing categories. Provides CRUD operations with validation. Predefined categories (seeded from enum)
/// can be edited (name/icon) but not deleted. User-defined categories can be fully managed.
/// <para>
/// The full category list is cached in memory (TTL: 30 minutes, key: <see cref="CacheKey"/>).
/// The cache is invalidated whenever a category is created, updated, or deleted so that reads
/// always reflect the latest state. Transaction usage counts are NOT cached here because they
/// change with every transaction write.
/// </para>
/// </summary>
public sealed class CategoryService
{
  private const string CacheKey = "Categories.All";
  private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

  private readonly ICategoryRepository categoryRepository;
  private readonly ITransactionRepository transactionRepository;
  private readonly IMemoryCache memoryCache;

  /// <summary>
  /// Initializes a new instance of the CategoryService class.
  /// </summary>
  public CategoryService(
    ICategoryRepository categoryRepository,
    ITransactionRepository transactionRepository,
    IMemoryCache memoryCache)
  {
    this.categoryRepository = categoryRepository;
    this.transactionRepository = transactionRepository;
    this.memoryCache = memoryCache;
  }

  /// <summary>
  /// Gets all active categories with usage counts. Counts both legacy enum-based categories and new CategoryId-based ones.
  /// The category list is served from cache when available.
  /// </summary>
  public async Task<List<CategoryDisplayDto>> GetAllAsync(CancellationToken cancellationToken = default)
  {
    // Aggregate counts in DB — avoids loading all transactions into memory
    Dictionary<int, int> usageCountsById = await transactionRepository.GetCategoryUsageCountsByIdAsync(cancellationToken);
    Dictionary<int, int> usageCountsByEnum = await transactionRepository.GetCategoryUsageCountsByEnumAsync(cancellationToken);

    List<CategoryEntity> categories = await GetCachedCategoriesAsync(cancellationToken);

    return categories.OrderBy(c => c.IsIncome).ThenBy(c => c.Name).Select(c =>
    {
      int countById = usageCountsById.TryGetValue(c.Id, out int cnt1) ? cnt1 : 0;
      int countByEnum = usageCountsByEnum.TryGetValue(c.Id, out int cnt2) ? cnt2 : 0;

      return new CategoryDisplayDto
      {
        Id = c.Id,
        Name = c.Name,
        Code = c.Code,
        IconCss = c.IconCss,
        IsIncome = c.IsIncome,
        IsPredefined = c.IsPredefined,
        UsageCount = countById + countByEnum
      };
    }).ToList();
  }

  /// <summary>
  /// Creates a new user-defined category.
  /// </summary>
  public async Task<CategoryDisplayDto> CreateAsync(CategoryDto dto, CancellationToken cancellationToken = default)
  {
    string trimmedName = (dto.Name ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(trimmedName))
    {
      throw new InvalidOperationException("Category name is required.");
    }

    // Use cached list for duplicate check
    List<CategoryEntity> all = await GetCachedCategoriesAsync(cancellationToken);
    bool exists = all.Any(c => c.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));

    if (exists)
    {
      throw new InvalidOperationException($"A category named '{trimmedName}' already exists.");
    }

    CategoryEntity entity = new CategoryEntity
    {
      Name = trimmedName,
      IconCss = string.IsNullOrWhiteSpace(dto.IconCss) ? "bi bi-tag" : dto.IconCss.Trim(),
      IsIncome = dto.IsIncome,
      IsPredefined = false,
      Code = null
    };

    await categoryRepository.InsertAsync(entity);
    InvalidateCache();

    return new CategoryDisplayDto
    {
      Id = entity.Id,
      Name = entity.Name,
      Code = entity.Code,
      IconCss = entity.IconCss,
      IsIncome = entity.IsIncome,
      IsPredefined = entity.IsPredefined,
      UsageCount = 0
    };
  }

  /// <summary>
  /// Updates an existing category. For predefined categories, only Name and IconCss can be changed.
  /// </summary>
  public async Task<CategoryDisplayDto> UpdateAsync(int id, CategoryDto dto, CancellationToken cancellationToken = default)
  {
    CategoryEntity? entity = await categoryRepository.SelectAsync(id);
    if (entity == null)
    {
      throw new InvalidOperationException("Category not found.");
    }

    string trimmedName = (dto.Name ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(trimmedName))
    {
      throw new InvalidOperationException("Category name is required.");
    }

    // Use cached list for duplicate check
    List<CategoryEntity> all = await GetCachedCategoriesAsync(cancellationToken);
    bool duplicateExists = all.Any(c => c.Id != id && c.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
    if (duplicateExists)
    {
      throw new InvalidOperationException($"A category named '{trimmedName}' already exists.");
    }

    entity.Name = trimmedName;
    entity.IconCss = string.IsNullOrWhiteSpace(dto.IconCss) ? "bi bi-tag" : dto.IconCss.Trim();

    if (!entity.IsPredefined)
    {
      entity.IsIncome = dto.IsIncome;
    }

    await categoryRepository.UpdateAsync(entity);
    InvalidateCache();

    int usageCount = await GetUsageCountAsync(id, cancellationToken);

    return new CategoryDisplayDto
    {
      Id = entity.Id,
      Name = entity.Name,
      Code = entity.Code,
      IconCss = entity.IconCss,
      IsIncome = entity.IsIncome,
      IsPredefined = entity.IsPredefined,
      UsageCount = usageCount
    };
  }

  /// <summary>
  /// Soft-deletes a category. Predefined categories cannot be deleted. Categories in use cannot be deleted.
  /// </summary>
  public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
  {
    CategoryEntity? entity = await categoryRepository.SelectAsync(id);
    if (entity == null)
    {
      throw new InvalidOperationException("Category not found.");
    }

    if (entity.IsPredefined)
    {
      throw new InvalidOperationException("Predefined categories cannot be deleted.");
    }

    int usageCount = await GetUsageCountAsync(id, cancellationToken);
    if (usageCount > 0)
    {
      throw new InvalidOperationException($"Category is in use by {usageCount} transaction(s) and cannot be deleted.");
    }

    entity.Deleted = true;
    entity.Active = false;
    await categoryRepository.UpdateAsync(entity);
    InvalidateCache();
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  /// <summary>
  /// Returns the full category list from cache, populating it from the database on a cache miss.
  /// TTL is <see cref="CacheTtl"/> (30 minutes). Invalidated by any write operation.
  /// </summary>
  private async Task<List<CategoryEntity>> GetCachedCategoriesAsync(CancellationToken cancellationToken)
  {
    if (memoryCache.TryGetValue(CacheKey, out List<CategoryEntity>? cached) && cached != null)
    {
      return cached;
    }

    List<CategoryEntity> fresh = await categoryRepository.SelectAsync(cancellationToken) ?? new List<CategoryEntity>();

    memoryCache.Set(CacheKey, fresh, CacheTtl);

    return fresh;
  }

  /// <summary>
  /// Removes the category list from cache so the next read re-fetches from the database.
  /// </summary>
  private void InvalidateCache() => memoryCache.Remove(CacheKey);

  /// <summary>
  /// Gets the total usage count for a category (from both CategoryId FK and legacy Category enum).
  /// </summary>
  private async Task<int> GetUsageCountAsync(int categoryId, CancellationToken cancellationToken)
  {
    return await transactionRepository.GetUsageCountForCategoryAsync(categoryId, cancellationToken);
  }
}
