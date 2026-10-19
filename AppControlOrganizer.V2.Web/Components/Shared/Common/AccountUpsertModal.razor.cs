//-----------------------------------------------------------------------
// <copyright file="AccountUpsertModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Modal component used to create or update account entities.
/// </summary>
public partial class AccountUpsertModal : UpsertModalBase
{
  /// <summary>The account model being created or edited.</summary>
  [Parameter]
  public AccountEntity Model { get; set; } = null!;

  /// <summary>Callback invoked when a logo file is selected.</summary>
  [Parameter]
  public EventCallback<InputFileChangeEventArgs> OnLogoSelected { get; set; }

  private static readonly DropdownItem<InstitutionType>[] InstitutionTypeItems =
    Enum.GetValues<InstitutionType>().ToDropdownItemsArray(t => t.GetDisplayName());

  private static readonly DropdownItem<AccountType?>[] AccountTypeItems =
    Enum.GetValues<AccountType>().ToNullableDropdownItemsArray(t => t.GetDisplayName());

  /// <summary>
  /// Toggles the presence of the specified currency in the model's supported currencies.
  /// </summary>
  private void ToggleSupportedCurrency(Currency currency)
  {
    if (Model.SupportedCurrencies == null)
    {
      Model.SupportedCurrencies = new System.Collections.Generic.List<Currency>();
    }

    if (Model.SupportedCurrencies.Contains(currency))
    {
      Model.SupportedCurrencies.Remove(currency);
    }
    else
    {
      Model.SupportedCurrencies.Add(currency);
    }

    _ = MarkDirty();
  }

  /// <summary>Clears all supported currencies from the model.</summary>
  private void ClearSupportedCurrencies()
  {
    Model.SupportedCurrencies?.Clear();
    _ = MarkDirty();
  }
}
