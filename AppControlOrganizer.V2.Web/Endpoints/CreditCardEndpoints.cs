//-----------------------------------------------------------------------
// <copyright file="CreditCardEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using System.Threading;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

/// <summary>
/// Minimal API endpoints for credit card management.
/// </summary>
public static class CreditCardEndpoints
{
  /// <summary>
  /// Maps credit card-related API endpoints to the application.
  /// </summary>
  /// <param name="app">The endpoint route builder.</param>
  public static void MapCreditCardEndpoints(this IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/creditcards")
      .WithTags("CreditCards")
      .RequireAuthorization()
      .RequireRateLimiting("api");

    group.MapGet("/", GetAllCreditCards).WithName("GetAllCreditCards").WithOpenApi();
    group.MapGet("/GetCreditCardById/{id:int}", GetCreditCardById).WithName("GetCreditCardById").WithOpenApi();
    group.MapGet("/by-account/{accountId:int}", GetCreditCardsByAccountId)
      .WithName("GetCreditCardsByAccountId")
      .WithOpenApi();
    group.MapPost("/CreateCreditCard", CreateCreditCard).WithName("CreateCreditCard").WithOpenApi();
    group.MapPut("/UpdateCreditCard/{id:int}", UpdateCreditCard).WithName("UpdateCreditCard").WithOpenApi();
    group.MapDelete("/DeleteCreditCard/{id:int}", DeleteCreditCard).WithName("DeleteCreditCard").WithOpenApi();
  }

  /// <summary>
  /// Gets all credit cards with their accounts.
  /// </summary>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <returns>List of credit card entities.</returns>
  private static async Task<IResult> GetAllCreditCards(ICreditCardRepository creditCardRepository)
  {
    List<CreditCardEntity>? entities = await creditCardRepository.SelectAsync(CancellationToken.None);
    return Results.Ok(EndpointHelper.SafeOrder(entities, c => c.HolderName));
  }

  /// <summary>
  /// Gets a specific credit card by ID.
  /// </summary>
  /// <param name="id">Credit card ID.</param>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <returns>Credit card entity or 404 if not found.</returns>
  private static async Task<IResult> GetCreditCardById(int id, ICreditCardRepository creditCardRepository)
  {
    CreditCardEntity? card = await creditCardRepository.SelectAsync(id);
    return EndpointHelper.OkOrNotFound(card, "Credit card not found.");
  }

  /// <summary>
  /// Gets credit cards filtered by account ID.
  /// </summary>
  /// <param name="accountId">Account ID to filter by.</param>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <returns>List of credit card entities for the account.</returns>
  private static async Task<IResult> GetCreditCardsByAccountId(int accountId, ICreditCardRepository creditCardRepository)
  {
    // Filter by AccountId directly in the database — avoids loading all cards into memory.
    List<CreditCardEntity>? cards = await creditCardRepository.SelectByAccountAsync(accountId, CancellationToken.None);
    return Results.Ok(EndpointHelper.SafeOrder(cards, c => c.HolderName));
  }

  /// <summary>
  /// Creates a new credit card.
  /// </summary>
  /// <param name="dto">Credit card creation data.</param>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <param name="accountRepository">Account repository.</param>
  /// <returns>Created credit card entity.</returns>
  private static async Task<IResult> CreateCreditCard(
    CreateCreditCardDto dto,
    ICreditCardRepository creditCardRepository,
    IAccountRepository accountRepository)
  {
    // Existence-only check — no need to load the full account entity with its Includes.
    if (!await accountRepository.ExistsAsync(dto.AccountId))
    {
      return Results.BadRequest(new { message = "Account not found." });
    }

    CreditCardEntity card = new CreditCardEntity
    {
      HolderName = dto.HolderName,
      CardType = dto.CardType,
      Flag = dto.Flag,
      Number = dto.Number,
      SecurityCode = dto.SecurityCode,
      Validity = dto.Validity,
      HasLimit = dto.HasLimit,
      LimitValue = dto.LimitValue,
      HasInvoice = dto.HasInvoice,
      InvoiceClosingDay = dto.InvoiceClosingDay,
      InvoicePaymentDay = dto.InvoicePaymentDay,
      HasAnnualFee = dto.HasAnnualFee,
      AnnualFee = dto.AnnualFee,
      HasBenefits = dto.HasBenefits,
      Benefits = dto.Benefits,
      HasRewards = dto.HasRewards,
      Rewards = dto.Rewards,
      HasInsurance = dto.HasInsurance,
      Insurance = dto.Insurance,
      HasAssistance = dto.HasAssistance,
      Assistance = dto.Assistance,
      HasOtherBenefits = dto.HasOtherBenefits,
      OtherBenefits = dto.OtherBenefits,
      Status = dto.Status,
      Currency = dto.Currency,
      AccountId = dto.AccountId,
      TransactionCurrencies = dto.TransactionCurrencies?.ToList() ?? new List<Currency>()
    };

    await creditCardRepository.InsertAsync(card);

    CreditCardEntity? created = await creditCardRepository.SelectAsync(card.Id);
    return Results.Created($"/api/creditcards/{created?.Id ?? card.Id}", created ?? card);
  }

  /// <summary>
  /// Updates an existing credit card.
  /// </summary>
  /// <param name="id">Credit card ID.</param>
  /// <param name="dto">Credit card update data.</param>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <param name="accountRepository">Account repository.</param>
  /// <returns>Updated credit card entity or 404 if not found.</returns>
  private static async Task<IResult> UpdateCreditCard(
    int id,
    UpdateCreditCardDto dto,
    ICreditCardRepository creditCardRepository,
    IAccountRepository accountRepository)
  {
    CreditCardEntity? card = await creditCardRepository.SelectAsync(id);
    if (card is null)
    {
      return EndpointHelper.OkOrNotFound(card, "Credit card not found.");
    }

    if (card.AccountId != dto.AccountId && !await accountRepository.ExistsAsync(dto.AccountId))
    {
      return Results.BadRequest(new { message = "Account not found." });
    }

    card.HolderName = dto.HolderName;
    card.CardType = dto.CardType;
    card.Flag = dto.Flag;
    card.Number = dto.Number;
    card.SecurityCode = dto.SecurityCode;
    card.Validity = dto.Validity;
    card.HasLimit = dto.HasLimit;
    card.LimitValue = dto.LimitValue;
    card.HasInvoice = dto.HasInvoice;
    card.InvoiceClosingDay = dto.InvoiceClosingDay;
    card.InvoicePaymentDay = dto.InvoicePaymentDay;
    card.HasAnnualFee = dto.HasAnnualFee;
    card.AnnualFee = dto.AnnualFee;
    card.HasBenefits = dto.HasBenefits;
    card.Benefits = dto.Benefits;
    card.HasRewards = dto.HasRewards;
    card.Rewards = dto.Rewards;
    card.HasInsurance = dto.HasInsurance;
    card.Insurance = dto.Insurance;
    card.HasAssistance = dto.HasAssistance;
    card.Assistance = dto.Assistance;
    card.HasOtherBenefits = dto.HasOtherBenefits;
    card.OtherBenefits = dto.OtherBenefits;
    card.Status = dto.Status;
    card.Currency = dto.Currency;
    card.AccountId = dto.AccountId;
    card.TransactionCurrencies = dto.TransactionCurrencies?.ToList() ?? new List<Currency>();

    await creditCardRepository.UpdateAsync(card);

    // Re-fetch by ID only — avoids loading all cards just to return the updated one.
    CreditCardEntity? updated = await creditCardRepository.SelectAsync(id);
    return Results.Ok(updated ?? card);
  }

  /// <summary>
  /// Soft-deletes a credit card.
  /// </summary>
  /// <param name="id">Credit card ID.</param>
  /// <param name="creditCardRepository">Credit card repository.</param>
  /// <returns>204 No Content or 404 if not found.</returns>
  private static async Task<IResult> DeleteCreditCard(int id, ICreditCardRepository creditCardRepository)
  {
    CreditCardEntity? card = await creditCardRepository.SelectAsync(id);
    if (card is null)
    {
      return EndpointHelper.NoContentOrNotFound(card, "Credit card not found.");
    }

    await creditCardRepository.DeleteAsync(id);

    return Results.NoContent();
  }
}
