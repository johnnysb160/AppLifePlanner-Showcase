//-----------------------------------------------------------------------
// <copyright file="AllocationEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Services;
/// <summary>
/// Minimal API endpoints for allocation management.
/// </summary>
public static class AllocationEndpoints
{
  /// <summary>
  /// Maps allocation-related API endpoints to the application.
  /// </summary>
  /// <param name="app">The endpoint route builder.</param>
  public static void MapAllocationEndpoints(this IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/allocations")
      .WithTags("Allocations")
      .RequireAuthorization()
      .RequireRateLimiting("api");

    group.MapGet("/", GetAllAllocations).WithName("GetAllAllocations").WithOpenApi();

    group.MapGet("/GetAllocationById/{id:int}", GetAllocationById).WithName("GetAllocationById").WithOpenApi();

    group.MapGet("/by-account/{accountId:int}", GetAllocationsByAccountId)
      .WithName("GetAllocationsByAccountId")
      .WithOpenApi();

    group.MapPost("/CreateAllocation", CreateAllocation).WithName("CreateAllocation").WithOpenApi();

    group.MapPut("/UpdateAllocation/{id:int}", UpdateAllocation).WithName("UpdateAllocation").WithOpenApi();

    group.MapDelete("/DeleteAllocation/{id:int}", DeleteAllocation).WithName("DeleteAllocation").WithOpenApi();
  }

  /// <summary>
  /// Gets all allocations with their accounts.
  /// </summary>
  /// <param name="db">Database context.</param>
  /// <returns>List of allocation DTOs.</returns>
  private static async Task<IResult> GetAllAllocations(IAllocationRepository allocationRepository)
  {
    List<AllocationEntity>? entities = await allocationRepository.SelectAsync(CancellationToken.None);
    return Results.Ok(EndpointHelper.SafeOrder(entities, a => a.Name));
  }

  /// <summary>
  /// Gets a specific allocation by ID.
  /// </summary>
  /// <param name="id">Allocation ID.</param>
  /// <param name="db">Database context.</param>
  /// <returns>Allocation DTO or 404 if not found.</returns>
  private static async Task<IResult> GetAllocationById(int id, IAllocationRepository allocationRepository)
  {
    AllocationEntity? allocation = await allocationRepository.SelectAsync(id);
    return EndpointHelper.OkOrNotFound(allocation, "Allocation not found");
  }

  /// <summary>
  /// Gets allocations filtered by account ID.
  /// </summary>
  /// <param name="accountId">Account ID to filter by.</param>
  /// <param name="db">Database context.</param>
  /// <returns>List of allocation DTOs for the account.</returns>
  private static async Task<IResult> GetAllocationsByAccountId(int accountId, IAllocationRepository allocationRepository)
  {
    List<AllocationEntity>? entities = await allocationRepository.SelectByAccountAsync(accountId, CancellationToken.None);
    return Results.Ok(EndpointHelper.SafeOrder(entities, a => a.Name));
  }

  /// <summary>
  /// Creates a new allocation.
  /// </summary>
  /// <param name="dto">Allocation creation data.</param>
  /// <param name="db">Database context.</param>
  /// <returns>Created allocation DTO.</returns>
  private static async Task<IResult> CreateAllocation(
    CreateAllocationDto dto,
    IAllocationRepository allocationRepository,
    IAccountRepository accountRepository,
    FinancialSyncService syncService)
  {
    // Existence-only check — no need to load the full account entity with its Includes.
    if (!await accountRepository.ExistsAsync(dto.AccountId))
    {
      return Results.BadRequest(new { message = "Account not found" });
    }

    AllocationEntity allocation = new AllocationEntity
    {
      Name = dto.Name,
      Initials = dto.Initials,
      AllocationType = dto.AllocationType,
      PlanningPurpose = dto.PlanningPurpose,
      BuyUnitValue = dto.BuyUnitValue,
      Quantity = dto.Quantity,
      CurrentUnitValue = dto.CurrentUnitValue,
      Currency = dto.Currency,
      SecondCurrency = dto.SecondCurrency,
      ProfitType = dto.ProfitType,
      ProfitPercentage = dto.ProfitPercentage,
      ProfitDate = dto.ProfitDate,
      ApplicationTerm = dto.ApplicationTerm,
      ExpirationDate = dto.ExpirationDate,
      Annotations = dto.Annotations,
      AccountId = dto.AccountId
    };

    await allocationRepository.InsertAsync(allocation);
    await syncService.SyncAccountAsync(dto.AccountId);

    AllocationEntity? created = await allocationRepository.SelectAsync(allocation.Id);

    return Results.Created($"/api/allocations/{created?.Id ?? allocation.Id}", created ?? allocation);
  }

  /// <summary>
  /// Updates an existing allocation.
  /// </summary>
  /// <param name="id">Allocation ID.</param>
  /// <param name="dto">Allocation update data.</param>
  /// <param name="db">Database context.</param>
  /// <returns>Updated allocation DTO or 404 if not found.</returns>
  private static async Task<IResult> UpdateAllocation(
    int id,
    UpdateAllocationDto dto,
    IAllocationRepository allocationRepository,
    IAccountRepository accountRepository,
    FinancialSyncService syncService)
  {
    AllocationEntity? allocation = await allocationRepository.SelectAsync(id);
    if (allocation is null)
    {
      return EndpointHelper.OkOrNotFound(allocation, "Allocation not found");
    }

    if (allocation.AccountId != dto.AccountId && !await accountRepository.ExistsAsync(dto.AccountId))
    {
      return Results.BadRequest(new { message = "Account not found" });
    }

    int previousAccountId = allocation.AccountId;

    allocation.Name = dto.Name;
    allocation.Initials = dto.Initials;
    allocation.AllocationType = dto.AllocationType;
    allocation.PlanningPurpose = dto.PlanningPurpose;
    allocation.BuyUnitValue = dto.BuyUnitValue;
    allocation.Quantity = dto.Quantity;
    allocation.CurrentUnitValue = dto.CurrentUnitValue;
    allocation.Currency = dto.Currency;
    allocation.SecondCurrency = dto.SecondCurrency;
    allocation.ProfitType = dto.ProfitType;
    allocation.ProfitPercentage = dto.ProfitPercentage;
    allocation.ProfitDate = dto.ProfitDate;
    allocation.ApplicationTerm = dto.ApplicationTerm;
    allocation.ExpirationDate = dto.ExpirationDate;
    allocation.Annotations = dto.Annotations;
    allocation.AccountId = dto.AccountId;

    await allocationRepository.UpdateAsync(allocation);

    // Sync both old and new accounts when the allocation moves between accounts.
    await syncService.SyncAccountAsync(dto.AccountId);
    if (previousAccountId != dto.AccountId)
    {
      await syncService.SyncAccountAsync(previousAccountId);
    }

    AllocationEntity? updated = await allocationRepository.SelectAsync(id);
    return Results.Ok(updated ?? allocation);
  }

  /// <summary>
  /// Soft-deletes an allocation.
  /// </summary>
  /// <param name="id">Allocation ID.</param>
  /// <param name="db">Database context.</param>
  /// <returns>204 No Content or 404 if not found.</returns>
  private static async Task<IResult> DeleteAllocation(
    int id,
    IAllocationRepository allocationRepository,
    FinancialSyncService syncService)
  {
    AllocationEntity? allocation = await allocationRepository.SelectAsync(id);
    if (allocation is null)
    {
      return EndpointHelper.NoContentOrNotFound(allocation, "Allocation not found");
    }

    int accountId = allocation.AccountId;
    await allocationRepository.DeleteAsync(id);
    await syncService.SyncAccountAsync(accountId);

    return Results.NoContent();
  }
}
