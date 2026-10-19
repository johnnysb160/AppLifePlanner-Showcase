//-----------------------------------------------------------------------
// <copyright file="AccountEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services;

/// <summary>
/// Minimal API endpoints for financial account management.
/// </summary>
public static class AccountEndpoints
{
  /// <summary>
  /// Maps account-related API endpoints to the application.
  /// </summary>
  /// <param name="app">The endpoint route builder.</param>
  public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/accounts")
      .WithTags("Accounts")
      .RequireAuthorization()
      .RequireRateLimiting("api");

    group.MapGet("/", GetAllAccounts).WithName("GetAllAccounts").WithOpenApi();

    group.MapGet("/GetAccountById/{id:int}", GetAccountById).WithName("GetAccountById").WithOpenApi();

    group.MapPost("/CreateAccount", CreateAccount).WithName("CreateAccount").WithOpenApi();

    group.MapPut("/UpdateAccount/{id:int}", UpdateAccount).WithName("UpdateAccount").WithOpenApi();

    group.MapDelete("/DeleteAccount/{id:int}", DeleteAccount).WithName("DeleteAccount").WithOpenApi();
  }

  /// <summary>
  /// Gets all accounts with their allocations and cards.
  /// </summary>
  /// <param name="db">Database context.</param>
  /// <returns>List of account entities.</returns>
  private static async Task<IResult> GetAllAccounts(IAccountRepository accountRepository)
  {
    List<AccountEntity>? entities = await accountRepository.SelectAsync(CancellationToken.None);
    return Results.Ok(EndpointHelper.SafeOrder(entities, a => a.Name));
  }

  /// <summary>
  /// Gets a specific account by ID.
  /// </summary>
  /// <param name="id">Account ID.</param>
  /// <param name="db">Database context.</param>
  /// <returns>Account entity or 404 if not found.</returns>
  private static async Task<IResult> GetAccountById(int id, IAccountRepository accountRepository)
  {
    AccountEntity? account = await accountRepository.SelectOneAsync(a => a.Id == id, CancellationToken.None);
    return EndpointHelper.OkOrNotFound(account, "Account not found");
  }

  /// <summary>
  /// Creates a new account using the specified account details and persists it to the repository.
  /// </summary>
  /// <param name="dto">An object containing the details required to create the account. All required fields must be provided; some fields
  /// may have default values if not specified.</param>
  /// <param name="accountRepository">The repository used to persist the new account entity asynchronously. Must not be null.</param>
  /// <returns>A result indicating that the account was successfully created. The result includes the URI of the newly created
  /// account and the account data.</returns>
  private static async Task<IResult> CreateAccount(
    CreateAccountDto dto,
    IAccountRepository accountRepository,
    FinancialSyncService financialSyncService)
  {
    AccountEntity account = new AccountEntity
    {
      Name = dto.Name,
      InstitutionType = dto.InstitutionType,
      Logo = string.IsNullOrWhiteSpace(dto.Logo) ? Helper.UnknowLogoDataUri : dto.Logo,
      NumberAccount = dto.NumberAccount,
      Agency = dto.Agency,
      BankCode = dto.BankCode,
      AccountType = dto.AccountType,
      HasCard = dto.HasCard,
      AccountIsActive = dto.AccountIsActive,
      HasFeesOrCharges = dto.HasFeesOrCharges,
      FeesOrChargesType = dto.FeesOrChargesType,
      FeesOrChargesValue = dto.FeesOrChargesValue,
      HasInvestments = dto.HasInvestments,
      Currency = dto.Currency,
      BalanceMode = dto.BalanceMode,
      OpeningBalance = dto.OpeningBalance,
      OpeningBalanceDate = dto.OpeningBalanceDate
    };

    await accountRepository.InsertAsync(account);
    await financialSyncService.SyncAccountAsync(account.Id);

    return Results.Created($"/api/accounts/{account.Id}", account);
  }

  /// <summary>
  /// Updates the account with the specified identifier using the provided data transfer object.
  /// </summary>
  /// <param name="id">The unique identifier of the account to update.</param>
  /// <param name="dto">An object containing the updated account information.</param>
  /// <param name="accountRepository">The repository used to access and update account data.</param>
  /// <returns>A result indicating the outcome of the update operation. Returns a 200 OK result with the updated account if
  /// successful; otherwise, returns a 404 Not Found result if the account does not exist.</returns>
  private static async Task<IResult> UpdateAccount(
    int id,
    UpdateAccountDto dto,
    IAccountRepository accountRepository,
    FinancialSyncService financialSyncService)
  {
    AccountEntity? account = await accountRepository.SelectAsync(id);
    if (account is null)
    {
      return EndpointHelper.OkOrNotFound(account, "Account not found");
    }

    account.Id = id;
    account.Name = dto.Name;
    account.InstitutionType = dto.InstitutionType;
    account.Logo = string.IsNullOrWhiteSpace(dto.Logo) ? Helper.UnknowLogoDataUri : dto.Logo;
    account.NumberAccount = dto.NumberAccount;
    account.Agency = dto.Agency;
    account.BankCode = dto.BankCode;
    account.AccountType = dto.AccountType;
    account.HasCard = dto.HasCard;
    account.AccountIsActive = dto.AccountIsActive;
    account.HasFeesOrCharges = dto.HasFeesOrCharges;
    account.FeesOrChargesType = dto.FeesOrChargesType;
    account.FeesOrChargesValue = dto.FeesOrChargesValue;
    account.HasInvestments = dto.HasInvestments;
    account.Currency = dto.Currency;
    account.BalanceMode = dto.BalanceMode;
    account.OpeningBalance = dto.OpeningBalance;
    account.OpeningBalanceDate = dto.OpeningBalanceDate;

    await accountRepository.UpdateAsync(account);
    await financialSyncService.SyncAccountAsync(id);

    AccountEntity? updated = await accountRepository.SelectAsync(id);
    return Results.Ok(updated ?? account);
  }

  /// <summary>
  /// Deletes the account with the specified identifier.
  /// </summary>
  /// <param name="id">The unique identifier of the account to delete.</param>
  /// <param name="accountRepository">The repository used to access and manage account data.</param>
  /// <returns>A result indicating the outcome of the operation. Returns a 204 No Content response if the account is successfully
  /// deleted; returns a 404 Not Found response if the account does not exist.</returns>
  private static async Task<IResult> DeleteAccount(int id, IAccountRepository accountRepository)
  {
    AccountEntity? account = await accountRepository.SelectOneAsync(a => a.Id == id, CancellationToken.None);
    if (account is null)
    {
      return EndpointHelper.NoContentOrNotFound(account, "Account not found");
    }

    await accountRepository.DeleteAsync(id);

    return Results.NoContent();
  }
}
