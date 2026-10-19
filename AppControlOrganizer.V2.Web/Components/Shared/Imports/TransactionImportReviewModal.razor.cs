//-----------------------------------------------------------------------
// <copyright file="TransactionImportReviewModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace AppControlOrganizer.V2.Web.Components.Shared.Imports;

/// <summary>
/// Code-behind for TransactionImportReviewModal.razor.
/// </summary>
public partial class TransactionImportReviewModal :
          ComponentBase
{
  #region Parameters

  [Parameter]
  public bool IsVisible { get; set; }

  [Parameter]
  public List<PendingItem> PendingItems { get; set; } = new List<PendingItem>();

  [Parameter]
  public List<AccountEntity> Accounts { get; set; } = new List<AccountEntity>();

  [Parameter]
  public List<CreditCardEntity> CreditCards { get; set; } = new List<CreditCardEntity>();

  [Parameter]
  public EventCallback OnClosed { get; set; }

  [Parameter]
  public EventCallback<int> OnItemsAccepted { get; set; }

  [Parameter]
  public Func<List<PendingItem>, IProgress<int>, CancellationToken, Task<ImportResult>>? AcceptHandler { get; set; }

  #endregion

  #region State

  private bool IsProcessing { get; set; }

  private int ProcessProgress { get; set; }

  private string ResultMessage { get; set; } = string.Empty;

  private bool IsResultError { get; set; }

  private string LastAcceptedHref { get; set; } = string.Empty;

  private bool SelectAllChecked { get; set; }

  // Backup for edit cancellation
  private Dictionary<Guid, (DateTime Date, decimal Value, string Description, bool Income)> EditBackups
  {
    get;
    set;
  } 
        = new Dictionary<Guid, (DateTime, decimal, string, bool)>();

  #endregion

  #region Computed

  private int SelectedCount => PendingItems.Count(i => i.IsSelected);

  private bool HasSelection => SelectedCount > 0;

  #endregion

  #region Methods

  /// <summary>
  /// Resets modal state.
  /// </summary>
  public void Reset()
  {
    IsProcessing = false;
    ProcessProgress = 0;
    ResultMessage = string.Empty;
    IsResultError = false;
    SelectAllChecked = false;
    LastAcceptedHref = string.Empty;
    EditBackups.Clear();
  }

  private void SelectAll()
  {
    foreach (PendingItem item in PendingItems)
    {
            item.IsSelected = true;
    }

    SelectAllChecked = true;
  }

  private void DeselectAll()
  {
    foreach (PendingItem item in PendingItems)
    {
            item.IsSelected = false;
    }

    SelectAllChecked = false;
  }

  private void OnSelectAllChanged()
  {
    foreach (PendingItem item in PendingItems)
    {
            item.IsSelected = SelectAllChecked;
    }
  }

  private async Task AcceptAll()
  {
    SelectAll();
    await AcceptSelected();
  }

  private async Task AcceptSelected()
  {
    List<PendingItem> selectedItems = PendingItems.Where(i => i.IsSelected).ToList();

    if (selectedItems.Count == 0 || AcceptHandler == null)
    {
            return;
    }

    IsProcessing = true;
    ProcessProgress = 0;
    ResultMessage = string.Empty;
    LastAcceptedHref = BuildPendingItemsHref(selectedItems);

    try
    {
      Progress<int> progress = new Progress<int>(p =>
      {
        ProcessProgress = p;
        _ = InvokeAsync(StateHasChanged);
      });

      ImportResult result = await AcceptHandler(selectedItems, progress, CancellationToken.None);

      // Remove successfully inserted items from the list
      foreach (PendingItem item in selectedItems)
      {
        PendingItems.Remove(item);
      }

      ResultMessage = $"Inserted: {result.Inserted}, Skipped (duplicates): {result.SkippedDuplicates}, Errors: {result.Errors}";
      IsResultError = result.Errors > 0;

      await OnItemsAccepted.InvokeAsync(result.Inserted);
    }
    catch (Exception ex)
    {
      ResultMessage = $"Error: {ex.Message}";
      IsResultError = true;
    }
    finally
    {
      IsProcessing = false;
      ProcessProgress = 100;
      SelectAllChecked = false;
    }
  }

  private void RejectSelected()
  {
    List<PendingItem> selectedItems = PendingItems.Where(i => i.IsSelected).ToList();

    foreach (PendingItem item in selectedItems)
    {
            PendingItems.Remove(item);
    }

    SelectAllChecked = false;
    ResultMessage = $"{selectedItems.Count} item(s) rejected.";
    IsResultError = false;
  }

  private async Task AcceptSingle(PendingItem item)
  {
    if (AcceptHandler == null)
    {
            return;
    }

    IsProcessing = true;
    ProcessProgress = 0;
    ResultMessage = string.Empty;

    try
    {
      Progress<int> progress = new Progress<int>(p =>
      {
        ProcessProgress = p;
        _ = InvokeAsync(StateHasChanged);
      });

      ImportResult result = await AcceptHandler(new List<PendingItem> { item }, progress, CancellationToken.None);

      if (result.Inserted > 0)
      {
        PendingItems.Remove(item);
        ResultMessage = "Item accepted and inserted.";
        IsResultError = false;
        LastAcceptedHref = BuildPendingItemsHref(new List<PendingItem> { item });
        await OnItemsAccepted.InvokeAsync(1);
      }
      else if (result.SkippedDuplicates > 0)
      {
        PendingItems.Remove(item);
        ResultMessage = "Item skipped (duplicate detected).";
        IsResultError = false;
      }
      else
      {
        ResultMessage = "Failed to insert item.";
        IsResultError = true;
      }
    }
    catch (Exception ex)
    {
      ResultMessage = $"Error: {ex.Message}";
      IsResultError = true;
    }
    finally
    {
      IsProcessing = false;
      ProcessProgress = 100;
    }
  }

  private void RejectSingle(PendingItem item)
  {
    PendingItems.Remove(item);
    ResultMessage = "Item rejected.";
    IsResultError = false;
    LastAcceptedHref = string.Empty;
  }

  private void StartEdit(PendingItem item)
  {
    // Backup current values
    EditBackups[item.TempId] = (item.Date, item.Value, item.Description, item.Income);
    item.IsEditing = true;
  }

  private void SaveEdit(PendingItem item)
  {
    item.IsEditing = false;
    EditBackups.Remove(item.TempId);
  }

  private void CancelEdit(PendingItem item)
  {
    if (EditBackups.TryGetValue(item.TempId, out (DateTime Date, decimal Value, string Description, bool Income) backup))
    {
      item.Date = backup.Date;
      item.Value = backup.Value;
      item.Description = backup.Description;
      item.Income = backup.Income;
      EditBackups.Remove(item.TempId);
    }

    item.IsEditing = false;
  }

  private async Task OnClose()
  {
    if (!IsProcessing)
    {
            await OnClosed.InvokeAsync();
    }
  }

  private static string TruncateText(string text, int maxLength)
  {
    if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
    {
            return text;
    }

    return text.Substring(0, maxLength) + "...";
  }

  private AccountEntity? GetAccount(int? accountId)
  {
    if (!accountId.HasValue)
    {
      return null;
    }

    return Accounts.FirstOrDefault(account => account.Id == accountId.Value);
  }

  private CreditCardEntity? GetCreditCard(int? cardId)
  {
    if (!cardId.HasValue)
    {
      return null;
    }

    return CreditCards.FirstOrDefault(card => card.Id == cardId.Value);
  }

  private static string GetAccountHref(int accountId)
  {
    return $"accounts?accountId={accountId}";
  }

  private static string BuildPendingItemsHref(List<PendingItem> items)
  {
    PendingItem? first = items.OrderBy(item => item.Date).FirstOrDefault();
    if (first == null)
    {
      return string.Empty;
    }

    string href = $"transactions?month={first.Date.Month}&year={first.Date.Year}&category={first.Category}&type={(first.Income ? "income" : "expense")}";

    if (first.AccountId.HasValue)
    {
      href += $"&accountId={first.AccountId.Value}";
    }

    if (first.CreditCardId.HasValue)
    {
      href += $"&cardId={first.CreditCardId.Value}";
    }

    if (first.SourceKind != TransactionSourceKind.Legacy)
    {
      href += $"&sourceKind={first.SourceKind}";
    }

    return href;
  }

  #endregion
}
