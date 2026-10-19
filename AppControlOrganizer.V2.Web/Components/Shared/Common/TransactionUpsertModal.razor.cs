//-----------------------------------------------------------------------
// <copyright file="TransactionUpsertModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using System.Collections.Generic;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using Microsoft.AspNetCore.Components;

public partial class TransactionUpsertModal : UpsertModalBase
{
  [Parameter] public TransactionEntity Model { get; set; } = null!;

  [Parameter] public List<AccountEntity> Accounts { get; set; } = new();

  [Parameter] public EventCallback OnOpenCategories { get; set; }

  private void OnAccountChanged(ChangeEventArgs e)
  {
    Model.AccountId = int.TryParse(e.Value?.ToString(), out int id) && id > 0 ? id : null;
    MarkDirty();
  }

  private AccountEntity? SelectedAccount
  {
    get
    {
      if (!Model.AccountId.HasValue)
      {
        return null;
      }

      return Accounts.Find(account => account.Id == Model.AccountId.Value);
    }
  }

  private static string GetAccountHref(int accountId)
  {
    return $"accounts?accountId={accountId}";
  }

  private async Task OpenCategoriesAsync()
  {
    if (OnOpenCategories.HasDelegate)
    {
      await OnOpenCategories.InvokeAsync();
    }
  }

  private static string GetSourceKindLabel(TransactionSourceKind sourceKind)
  {
    return sourceKind switch
    {
      TransactionSourceKind.Legacy => "Legacy",
      TransactionSourceKind.Manual => "Manual",
      TransactionSourceKind.BankStatement => "Bank statement source",
      TransactionSourceKind.CardInvoice => "Card invoice source",
      TransactionSourceKind.Email => "Email source",
      TransactionSourceKind.Api => "API source",
      _ => sourceKind.ToString()
    };
  }
}
