//-----------------------------------------------------------------------
// <copyright file="CardUpsertModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Helpers;
using Microsoft.AspNetCore.Components;

public partial class CardUpsertModal : UpsertModalBase
{
  [Parameter] public CreditCardEntity Model { get; set; } = null!;

  [Parameter] public IEnumerable<DropdownItem<int>> AccountItems { get; set; } = Array.Empty<DropdownItem<int>>();

  [Parameter] public IReadOnlyList<AccountEntity>? AccountsLookup { get; set; }

  private static readonly DropdownItem<CardType>[] CardTypeItems =
    Enum.GetValues<CardType>().ToDropdownItemsArray(t => t.GetDisplayName());

  private static readonly DropdownItem<CardFlag>[] CardFlagItems =
    Enum.GetValues<CardFlag>().ToDropdownItemsArray(f => f.GetDisplayName());

  private static readonly DropdownItem<Currency>[] CurrencyItems =
    Enum.GetValues<Currency>().ToDropdownItemsArray(c => c.GetDisplayName());

  private async Task ToggleTransactionCurrency(Currency currency)
  {
    if (Model.TransactionCurrencies.Contains(currency))
    {
      Model.TransactionCurrencies.Remove(currency);
    }
    else
    {
      Model.TransactionCurrencies.Add(currency);
    }

    await MarkDirty();
  }

  private async Task RemoveTransactionCurrency(Currency currency)
  {
    Model.TransactionCurrencies.Remove(currency);
    await MarkDirty();
  }

  private async Task ClearTransactionCurrencies()
  {
    Model.TransactionCurrencies.Clear();
    await MarkDirty();
  }
}
