//-----------------------------------------------------------------------
// <copyright file="AllocationUpsertModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using System.Globalization;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.AspNetCore.Components;

public partial class AllocationUpsertModal : UpsertModalBase
{
  [Parameter] public AllocationEntity Model { get; set; } = null!;

  [Parameter] public List<AccountEntity> Accounts { get; set; } = new();

  [Parameter] public IReadOnlyDictionary<QuoteKey, MarketQuoteSnapshot> LatestQuotes { get; set; } = new Dictionary<QuoteKey, MarketQuoteSnapshot>();

  private IEnumerable<DropdownItem<int>> AccountDropdownItems
    => (Accounts ?? new List<AccountEntity>()).Select(a => new DropdownItem<int>(a.Id, a.Name));

  private static readonly DropdownItem<AllocationType>[] AllocationTypeItems =
    Enum.GetValues<AllocationType>().ToDropdownItemsArray(t => t.GetDisplayName());

  private static readonly DropdownItem<Currency>[] CurrencyItems =
    Enum.GetValues<Currency>().ToDropdownItemsArray(c => c.GetDisplayName());

  private bool IsFixedIncome
  {
    get
    {
      return Model != null && AssetQuoteResolver.IsFixedIncome(Model.AllocationType);
    }
  }

  /// <summary>
  /// Gets the latest quote for the current allocation type, if available.
  /// </summary>
  private MarketQuoteSnapshot? GetCurrentQuote()
  {
    if (Model == null || LatestQuotes == null)
    {
      return null;
    }

    MarketQuoteCache.TryGetForAllocation(LatestQuotes, Model.AllocationType, Model.Initials, out MarketQuoteSnapshot? quote);
    return quote;
  }

  /// <summary>
  /// Formats the quote info for display.
  /// </summary>
  private string FormatQuoteInfo(MarketQuoteSnapshot quote)
  {
    string priceStr = Helper.FormatCurrency(quote.QuoteCurrency, quote.Price);
    string timeStr = quote.QuoteTimestampUtc?.ToString("yyyy-MM-dd HH:mm") ?? quote.RetrievedAtUtc.ToString("yyyy-MM-dd HH:mm");
    return $"{priceStr} — {quote.Source} — {timeStr} UTC";
  }

  private static string GetAccountHref(int accountId)
  {
    return $"accounts?accountId={accountId}";
  }

  private string GetQuoteHref()
  {
    string quote = !string.IsNullOrWhiteSpace(Model.Initials)
      ? Model.Initials.Trim()
      : Model.AllocationType.ToString();
    return $"allocations?panel=quotes&quote={Uri.EscapeDataString(quote)}";
  }

  private decimal GetAppliedTotal()
  {
    if (Model == null)
    {
      return 0m;
    }

    return Model.BuyUnitValue * Model.Quantity;
  }

  private decimal GetCurrentTotal()
  {
    if (Model == null)
    {
      return 0m;
    }

    decimal unitValue = Model.CurrentUnitValue ?? Model.BuyUnitValue;
    return unitValue * Model.Quantity;
  }

  private string FormatMoney(decimal value)
  {
    // The currency symbol follows the money (BRL is always shown Brazilian-style), but the digit
    // grouping and decimal separator follow the interface language.
    return Model.Currency == Currency.BRL
      ? value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))
      : $"{Model.Currency.GetDisplayName()} {value.ToString("N2", Loc.Culture)}";
  }
}
