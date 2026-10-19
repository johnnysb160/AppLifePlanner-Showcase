//-----------------------------------------------------------------------
// <copyright file="TransactionImportFlow.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace AppControlOrganizer.V2.Web.Components.Shared.Imports;

public partial class TransactionImportFlow :
          ComponentBase
{
  [Inject]
  private ILogger<TransactionImportFlow> Logger { get; set; } = null!;

  [Inject]
  public IDbContextFactory<LifePlannerContext> DbFactory { get; set; } = null!;

  [Inject]
  public TransactionImportService ImportService { get; set; } = null!;

  [Inject]
  public ICreditCardRepository CreditCardRepository { get; set; } = null!;

  [Parameter]
  public EventCallback OnDataChanged { get; set; }

  private bool ShowImportModal { get; set; }

  private bool ShowReviewModal { get; set; }

  private List<AccountEntity> Accounts { get; set; } = new();

  private List<CreditCardEntity> CreditCards { get; set; } = new();

  private List<PendingItem> PendingItems { get; set; } = new();

  public Task OpenAsync(CancellationToken cancellationToken = default)
  {
    // Show modal immediately and load data asynchronously so UI responds quickly.
    PendingItems.Clear();
    ShowImportModal = true;
    StateHasChanged();

    _ = Task.Run(async () =>
    {
      try
      {
        await LoadDataAsync(cancellationToken);
        await InvokeAsync(StateHasChanged);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Failed to load import modal data");
      }
    });

    return Task.CompletedTask;
  }

  private async Task LoadDataAsync(CancellationToken cancellationToken)
  {
    await using LifePlannerContext db = await DbFactory.CreateDbContextAsync(cancellationToken);

    Accounts = await db.Accounts
            .Where(a => !a.Deleted && a.Active)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    List<CreditCardEntity>? cards = await CreditCardRepository.SelectAsync(cancellationToken);
    CreditCards = (cards ?? new List<CreditCardEntity>())
              .Where(c => c.Active && c.Status)
              .OrderBy(c => c.HolderName)
              .ToList();
  }

  private async Task<ImportResult> HandleImport(ImportRequest request,
                        IProgress<ImportProgressInfo>? progress,
                        CancellationToken cancellationToken)
  {
    try
    {
      return await ImportService.ImportAsync(request, progress, cancellationToken);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Import operation failed");
      return new ImportResult { ErrorMessage = ex.Message };
    }
  }

  private async Task<ImportResult> HandleAcceptPending(List<PendingItem> items,
                            IProgress<int>? progress,
                            CancellationToken cancellationToken)
  {
    try
    {
      return await ImportService.InsertPendingItemsAsync(items, progress, cancellationToken);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Insert pending items failed");
      return new ImportResult { ErrorMessage = ex.Message };
    }
  }

  private async Task OnImportCompleted(ImportResult result)
  {
    ShowImportModal = false;

    if (result.Inserted > 0 || result.Enriched > 0)
    {
      await OnDataChanged.InvokeAsync();
    }

    if (result.PendingItems.Count > 0)
    {
      PendingItems = result.PendingItems;
      ShowReviewModal = true;
    }

    StateHasChanged();
  }

  private void CloseImportModal()
  {
    ShowImportModal = false;
    StateHasChanged();
  }

  private async Task CloseReviewModal()
  {
    ShowReviewModal = false;
    PendingItems.Clear();
    await OnDataChanged.InvokeAsync();
    StateHasChanged();
  }

  private async Task OnReviewItemsAccepted(int count)
  {
    if (count > 0)
    {
      await OnDataChanged.InvokeAsync();
    }
  }
}
