//-----------------------------------------------------------------------
// <copyright file="TransactionEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using System.Security.Claims;
using System.Threading;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Analytics;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Minimal API endpoints for transaction management.
/// </summary>
public static class TransactionEndpoints
{
  /// <summary>
  /// Maps transaction-related API endpoints to the application.
  /// </summary>
  /// <param name="app">The endpoint route builder.</param>
  public static void MapTransactionEndpoints(this IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/transactions")
      .WithTags("Transactions")
      .RequireAuthorization()
      .RequireRateLimiting("api");

    group.MapGet("/", GetAllTransactions).WithName("GetAllTransactions").WithOpenApi();
    group.MapGet("/GetTransactionById/{id:int}", GetTransactionById).WithName("GetTransactionById").WithOpenApi();
    group.MapGet("/by-month/{year:int}/{month:int}", GetTransactionsByMonth)
      .WithName("GetTransactionsByMonth")
      .WithOpenApi();
    group.MapGet("/by-category/{category}", GetTransactionsByCategory)
      .WithName("GetTransactionsByCategory")
      .WithOpenApi();
    group.MapPost("/CreateTransaction", CreateTransaction).WithName("CreateTransaction").WithOpenApi();
    group.MapPost("/delete-by-external", DeleteByExternalIds).WithName("DeleteByExternalIds").WithOpenApi();
    group.MapPut("/UpdateTransaction/{id:int}", UpdateTransaction).WithName("UpdateTransaction").WithOpenApi();
    group.MapDelete("/DeleteTransaction/{id:int}", DeleteTransaction).WithName("DeleteTransaction").WithOpenApi();
    group.MapGet("/summary/{year:int}/{month:int}", GetMonthlySummary).WithName("GetMonthlySummary").WithOpenApi();
  }

  /// <summary>
  /// Request model for bulk deletion by external IDs.
  /// </summary>
  public sealed class DeleteByExternalRequest
  {
    /// <summary>Gets or sets the array of external IDs to delete.</summary>
    public int[] ExternalIds { get; set; } = Array.Empty<int>();
  }

  /// <summary>
  /// Soft-deletes transactions by external IDs.
  /// </summary>
  /// <param name="request">Request containing external IDs to delete.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>Count of deleted items.</returns>
  private static async Task<IResult> DeleteByExternalIds(
    DeleteByExternalRequest request,
    ITransactionRepository transactionRepository)
  {
    if (request.ExternalIds == null || request.ExternalIds.Length == 0)
    {
      return Results.BadRequest(new { message = "No external ids provided." });
    }

    // Filter by ExternalIds directly in the database — avoids loading all transactions into memory.
    int[] ids = request.ExternalIds;
    ICollection<TransactionEntity>? matched = await transactionRepository.SelectAsync(
      t => t.ExternalId.HasValue && ids.Contains(t.ExternalId.Value));

    int deleted = 0;
    if (matched != null)
    {
      foreach (TransactionEntity item in matched)
      {
        await transactionRepository.DeleteAsync(item.Id);
        deleted++;
      }
    }

    return Results.Ok(new { deleted });
  }

  /// <summary>
  /// Gets all transactions ordered by date descending.
  /// </summary>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>List of transaction entities.</returns>
  private static async Task<IResult> GetAllTransactions(ITransactionRepository transactionRepository)
  {
    List<TransactionEntity>? entities = await transactionRepository.SelectAsync(CancellationToken.None);
    List<TransactionEntity> transactions = (entities ?? new List<TransactionEntity>())
      .OrderByDescending(a => a.Date).ThenBy(a => a.Category).ToList();
    return Results.Ok(transactions);
  }

  /// <summary>
  /// Gets a specific transaction by ID.
  /// </summary>
  /// <param name="id">Transaction ID.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>Transaction entity or 404 if not found.</returns>
  private static async Task<IResult> GetTransactionById(int id, ITransactionRepository transactionRepository)
  {
    TransactionEntity? transaction = await transactionRepository.SelectAsync(id);
    return EndpointHelper.OkOrNotFound(transaction, "Transaction not found.");
  }

  /// <summary>
  /// Gets transactions for a specific month.
  /// </summary>
  /// <param name="year">Year to filter.</param>
  /// <param name="month">Month to filter.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>List of transaction entities for the month.</returns>
  private static async Task<IResult> GetTransactionsByMonth(
    int year,
    int month,
    ITransactionRepository transactionRepository)
  {
    DateTime startDate = new DateTime(year, month, 1);
    DateTime endDate = startDate.AddMonths(1).AddDays(-1);

    List<TransactionEntity>? entities = await transactionRepository.SelectByDateRangeAsync(startDate, endDate, CancellationToken.None);
    List<TransactionEntity> transactions = (entities ?? new List<TransactionEntity>())
      .OrderByDescending(a => a.Date).ThenBy(a => a.Category).ToList();
    return Results.Ok(transactions);
  }

  /// <summary>
  /// Gets transactions filtered by category.
  /// </summary>
  /// <param name="category">Category to filter by.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>List of transaction entities for the category.</returns>
  private static async Task<IResult> GetTransactionsByCategory(Category category, ITransactionRepository transactionRepository)
  {
    // Filter by Category enum directly in the database — avoids loading all transactions into memory.
    List<TransactionEntity>? transactions = await transactionRepository.SelectByCategoryEnumAsync(category, CancellationToken.None);
    return Results.Ok(transactions ?? new List<TransactionEntity>());
  }

  /// <summary>
  /// Creates a new transaction.
  /// </summary>
  /// <param name="dto">Transaction creation data.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>Created transaction entity.</returns>
  private static async Task<IResult> CreateTransaction(
    CreateTransactionDto dto,
    ITransactionRepository transactionRepository,
    FinancialSyncService syncService,
    IAccessAnalyticsRecorder accessAnalyticsRecorder,
    HttpContext httpContext)
  {
    TransactionEntity transaction = new TransactionEntity
    {
      Date = dto.Date,
      Category = dto.Category,
      Value = dto.Value,
      Description = dto.Description,
      Income = dto.Income,
      AccountId = dto.AccountId,
      SourceKind = TransactionSourceKind.Api,
      PlanningImpact = dto.PlanningImpact,
      PlanningClassificationConfirmed = dto.PlanningImpact != TransactionPlanningImpact.Unclassified,
      PlanningClassificationSource = dto.PlanningImpact != TransactionPlanningImpact.Unclassified
        ? PlanningClassificationSource.Manual
        : PlanningClassificationSource.None,
    };

    await transactionRepository.InsertAsync(transaction);
    await syncService.SyncAccountAsync(dto.AccountId);
    accessAnalyticsRecorder.RecordFeatureUsage("transaction.created", httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

    TransactionEntity? created = await transactionRepository.SelectAsync(transaction.Id);
    return Results.Created($"/api/transactions/{created?.Id ?? transaction.Id}", created ?? transaction);
  }

  /// <summary>
  /// Updates an existing transaction.
  /// </summary>
  /// <param name="id">Transaction ID.</param>
  /// <param name="dto">Transaction update data.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>Updated transaction entity or 404 if not found.</returns>
  private static async Task<IResult> UpdateTransaction(
    int id,
    UpdateTransactionDto dto,
    ITransactionRepository transactionRepository,
    FinancialSyncService syncService)
  {
    TransactionEntity? transaction = await transactionRepository.SelectAsync(id);
    if (transaction is null)
    {
      return EndpointHelper.OkOrNotFound(transaction, "Transaction not found.");
    }

    int? previousAccountId = transaction.AccountId;

    transaction.Date = dto.Date;
    transaction.Category = dto.Category;
    transaction.Value = dto.Value;
    transaction.Description = dto.Description;
    transaction.Income = dto.Income;
    transaction.AccountId = dto.AccountId;
    transaction.PlanningImpact = dto.PlanningImpact;
    transaction.PlanningClassificationConfirmed = dto.PlanningImpact != TransactionPlanningImpact.Unclassified;
    transaction.PlanningClassificationSource = transaction.PlanningClassificationConfirmed
      ? PlanningClassificationSource.Manual
      : PlanningClassificationSource.None;

    await transactionRepository.UpdateAsync(transaction);

    // Sync both old and new accounts when the linked account changes.
    await syncService.SyncAccountAsync(dto.AccountId);
    if (previousAccountId != dto.AccountId)
    {
      await syncService.SyncAccountAsync(previousAccountId);
    }

    TransactionEntity? updated = await transactionRepository.SelectAsync(id);
    return Results.Ok(updated ?? transaction);
  }

  /// <summary>
  /// Soft-deletes a transaction.
  /// </summary>
  /// <param name="id">Transaction ID.</param>
  /// <param name="transactionRepository">Transaction repository.</param>
  /// <returns>204 No Content or 404 if not found.</returns>
  private static async Task<IResult> DeleteTransaction(
    int id,
    ITransactionRepository transactionRepository,
    FinancialSyncService syncService)
  {
    TransactionEntity? transaction = await transactionRepository.SelectAsync(id);
    if (transaction is null)
    {
      return EndpointHelper.NoContentOrNotFound(transaction, "Transaction not found.");
    }

    int? accountId = transaction.AccountId;
    await transactionRepository.DeleteAsync(id);
    await syncService.SyncAccountAsync(accountId);

    return Results.NoContent();
  }

  /// <summary>
  /// Gets a monthly financial summary.
  /// </summary>
  /// <param name="year">Year to summarize.</param>
  /// <param name="month">Month to summarize.</param>
  /// <param name="db">Database context.</param>
  /// <returns>Summary object with totals and category breakdown.</returns>
  private static async Task<IResult> GetMonthlySummary(int year, int month, LifePlannerContext db)
  {
    DateTime startDate = new DateTime(year, month, 1);
    DateTime endDate = startDate.AddMonths(1).AddDays(-1);

    List<TransactionEntity> transactions = await db.Transactions
      .Where(a => a.Date >= startDate && a.Date <= endDate)
      .ToListAsync();

    object summary = new
    {
      Year = year,
      Month = month,
      TotalIncome = transactions.Where(a => a.Income).Sum(a => a.Value),
      TotalExpense = transactions.Where(a => !a.Income).Sum(a => a.Value),
      Balance = transactions.Where(a => a.Income).Sum(a => a.Value)
        - transactions.Where(a => !a.Income).Sum(a => a.Value),
      ByCategory = transactions
        .GroupBy(a => a.Category)
        .Select(g => new
        {
          Category = g.Key.ToString(),
          Total = g.Sum(a => a.Income ? a.Value : -a.Value),
          Income = g.Where(a => a.Income).Sum(a => a.Value),
          Expense = g.Where(a => !a.Income).Sum(a => a.Value),
          Count = g.Count()
        })
        .OrderByDescending(x => x.Total)
        .ToList()
    };

    return Results.Ok(summary);
  }
}
