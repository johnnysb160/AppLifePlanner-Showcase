//-----------------------------------------------------------------------
// <copyright file="TransactionImportModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Components.Shared.Common;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Helpers;

namespace AppControlOrganizer.V2.Web.Components.Shared.Imports;

public partial class TransactionImportModal :
          ComponentBase
{
  private const long MaxFileSize = 25 * 1024 * 1024; // 25MB

  [Inject]
  private ILogger<TransactionImportModal> Logger { get; set; } = null!;

  [Parameter]
  public bool IsVisible { get; set; }

  [Parameter]
  public EventCallback OnCancelled { get; set; }

  [Parameter]
  public EventCallback<ImportResult> OnImportCompleted { get; set; }

  [Parameter]
  public List<AccountEntity> Accounts { get; set; } = new List<AccountEntity>();

  [Parameter]
  public List<CreditCardEntity> CreditCards { get; set; } = new List<CreditCardEntity>();

  [Parameter]
  public Func<ImportRequest, IProgress<ImportProgressInfo>?, CancellationToken, Task<ImportResult>>? ImportHandler { get; set; }


  private ImportSourceType SourceType { get; set; } = ImportSourceType.BankStatement;

  private ImportFileFormat FileFormat { get; set; } = ImportFileFormat.Csv;

  private string ProgressLabel { get; set; } = "Importing...";

  private string lastProgressLabel = string.Empty;

  private int SelectedAccountId { get; set; }

  private int SelectedCreditCardId { get; set; }

  private IBrowserFile? SelectedFile { get; set; }

  private string SelectedFileName { get; set; } = string.Empty;

  private string ValidationMessage { get; set; } = string.Empty;

  private bool IsImporting { get; set; }

  private int Progress { get; set; }

  private ImportResult? ImportSummary { get; set; }

  // Track previous visibility to detect modal open transition
  private bool wasVisible;

  #region Dropdown Items

  private IEnumerable<DropdownItem<int>> AccountDropdownItems =>
    Accounts.Select(a => new DropdownItem<int>(a.Id, a.Name));

  private IEnumerable<DropdownItem<int>> CreditCardDropdownItems =>
    FilteredCreditCards.Select(c => new DropdownItem<int>(
      c.Id,
      GetCreditCardDisplayName(c)));

  #endregion

  private IEnumerable<CreditCardEntity> FilteredCreditCards =>
    SelectedAccountId == 0
      ? Enumerable.Empty<CreditCardEntity>()
      : CreditCards.Where(c => c.AccountId == SelectedAccountId);

  private AccountEntity? SelectedAccount =>
    SelectedAccountId > 0 ? Accounts.FirstOrDefault(a => a.Id == SelectedAccountId) : null;

  private CreditCardEntity? SelectedCreditCard =>
    SelectedCreditCardId > 0 ? CreditCards.FirstOrDefault(c => c.Id == SelectedCreditCardId) : null;

  private static string GetCreditCardDisplayName(CreditCardEntity card)
  {
    string holderName = GetCreditCardHolderName(card);
    string lastFour = GetCreditCardLastFour(card.Number) ?? "N/A";
    return $"{holderName} - ****{lastFour}";
  }

  private static string GetCreditCardHolderName(CreditCardEntity card)
  {
    if (!IsEncryptedPayload(card.HolderName) && !string.IsNullOrWhiteSpace(card.HolderName))
    {
      return card.HolderName;
    }

    return $"{card.Flag.GetDisplayName()} {card.CardType.GetDisplayName()}";
  }

  private static string? GetCreditCardLastFour(string? cardNumber)
  {
    if (string.IsNullOrWhiteSpace(cardNumber) || IsEncryptedPayload(cardNumber))
    {
      return null;
    }

    string digits = new string(cardNumber.Where(char.IsDigit).ToArray());
    return digits.Length >= 4 ? digits[^4..] : null;
  }

  private static bool IsEncryptedPayload(string? value)
  {
    return value?.StartsWith("enc:", StringComparison.OrdinalIgnoreCase) == true;
  }

  private bool CanImport
  {
    get
    {
      if (SelectedAccountId == 0 || SelectedFile == null)
      {
        return false;
      }

      if (SourceType == ImportSourceType.CardInvoice && SelectedCreditCardId == 0)
      {
        return false;
      }

      return true;
    }
  }

  /// <summary>
  /// Called when parameters are set. Detects modal open transition to reset state.
  /// </summary>
  protected override void OnParametersSet()
  {
    // Detect transition from closed to open
    if (IsVisible && !wasVisible && !IsImporting)
    {
      Reset();
    }

    wasVisible = IsVisible;
  }

  public void Reset()
  {
    SourceType = ImportSourceType.BankStatement;
    FileFormat = ImportFileFormat.Csv;
    SelectedAccountId = 0;
    SelectedCreditCardId = 0;
    SelectedFile = null;
    SelectedFileName = string.Empty;
    ValidationMessage = string.Empty;
    IsImporting = false;
    Progress = 0;
    ImportSummary = null;
  }

  private void SetSourceType(ImportSourceType sourceType)
  {
    SourceType = sourceType;
    SelectedCreditCardId = 0;
    ValidationMessage = string.Empty;
  }

  private async Task OnFileSelected(InputFileChangeEventArgs e)
  {
    try
    {
      // Only store IBrowserFile reference and name. Avoid accessing file size or other properties
      // that may trigger large transfers or client-side behavior during OnChange.
      SelectedFile = e.File;
      SelectedFileName = e.File.Name;
      ValidationMessage = string.Empty;

      // Yield to avoid doing heavy work on the input event sync context
      await Task.Yield();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error in OnFileSelected");
      ValidationMessage = $"Error selecting file: {ex.Message}";
    }
  }

  private string GetAcceptedFileTypes()
  {
    return FileFormat switch
    {
      ImportFileFormat.Ofx => ".ofx,.OFX",
      ImportFileFormat.Csv => ".csv,.CSV",
      _ => ".csv,.ofx"
    };
  }

  private static string GetAccountHref(int accountId)
  {
    return $"accounts?accountId={accountId}";
  }

  private string GetImportedTransactionsHref()
  {
    string href = $"transactions?accountId={SelectedAccountId}&sourceKind={GetTransactionSourceKind()}";

    if (SourceType == ImportSourceType.CardInvoice && SelectedCreditCardId > 0)
    {
      href += $"&cardId={SelectedCreditCardId}";
    }

    return href;
  }

  private static TransactionSourceKind GetTransactionSourceKind(ImportSourceType sourceType)
  {
    return sourceType == ImportSourceType.CardInvoice
      ? TransactionSourceKind.CardInvoice
      : TransactionSourceKind.BankStatement;
  }

  private TransactionSourceKind GetTransactionSourceKind()
  {
    return GetTransactionSourceKind(SourceType);
  }

  private async Task OnBackdropClick()
  {
    if (!IsImporting)
    {
      await OnCancelled.InvokeAsync();
    }
  }

  private async Task OnCancel()
  {
    if (!IsImporting)
    {
      await OnCancelled.InvokeAsync();
    }
  }

  private async Task ExecuteImport()
  {
    Logger.LogInformation("ExecuteImport called. CanImport={CanImport}, ImportHandler={HasHandler}",
      CanImport, ImportHandler != null);

    if (!CanImport || ImportHandler == null)
    {
      return;
    }

    ValidationMessage = string.Empty;
    IsImporting = true;
    Progress = 0;
    ProgressLabel = "Importing...";
    lastProgressLabel = string.Empty;
    ImportSummary = null;
    StateHasChanged();
    await Task.Delay(1);

    try
    {
      Logger.LogInformation("Starting import for file: {FileName}", SelectedFileName);

      string? cardLastFour = null;
      if (SourceType == ImportSourceType.CardInvoice && SelectedCreditCardId > 0)
      {
        CreditCardEntity? selectedCard = CreditCards.FirstOrDefault(c => c.Id == SelectedCreditCardId);
        cardLastFour = GetCreditCardLastFour(selectedCard?.Number);
      }

      Logger.LogInformation("Opening file stream...");
  using MemoryStream ms = new MemoryStream();
      Stream stream;
      try
      {
        stream = SelectedFile!.OpenReadStream(maxAllowedSize: MaxFileSize);
      }
      catch (IOException ex)
      {
        Logger.LogWarning(ex, "File exceeds maximum allowed size of {MaxSize}MB", MaxFileSize / (1024 * 1024));
        ValidationMessage = $"File exceeds maximum allowed size ({MaxFileSize / (1024 * 1024)}MB).";
        return;
      }

      Logger.LogInformation("Reading file into memory...");
      byte[] buffer = new byte[32 * 1024];
      int bytesRead;
      long totalRead = 0;
      while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
      {
        await ms.WriteAsync(buffer.AsMemory(0, bytesRead));
        totalRead += bytesRead;
      }
      ms.Position = 0;
      Logger.LogInformation("File read complete. Total bytes: {TotalBytes}", totalRead);

      ImportRequest request = new ImportRequest
      {
        SourceType = SourceType,
        FileFormat = FileFormat,
        AccountId = SelectedAccountId,
        CreditCardId = SourceType == ImportSourceType.CardInvoice ? SelectedCreditCardId : null,
        CreditCardLastFour = cardLastFour,
        FileStream = ms,
        FileName = SelectedFileName
      };

      Logger.LogInformation("Calling ImportHandler...");
      Progress<ImportProgressInfo> progress = new Progress<ImportProgressInfo>(OnProgressUpdated);
      ImportResult result = await Task.Run(async () =>
      {
        return await ImportHandler(request, progress, CancellationToken.None);
      });


      // Propagate account currency to each pending item for UI formatting
      if (result.PendingItems != null)
      {
        foreach (PendingItem pi in result.PendingItems)
        {
          pi.Currency = Accounts.FirstOrDefault(a => a.Id == request.AccountId)?.Currency
                ?? AppControlOrganizer.V2.Shared.Enums.Currency.BRL;
        }
      }
      Logger.LogInformation("ImportHandler completed. Inserted={Inserted}, Errors={Errors}",
        result.Inserted, result.Errors);

      Progress = 100;
      ImportSummary = result;

      if (!string.IsNullOrEmpty(result.ErrorMessage))
      {
        ValidationMessage = result.ErrorMessage;
      }

      await OnImportCompleted.InvokeAsync(result);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Import failed for file {FileName}", SelectedFileName);
      ValidationMessage = $"Error: {ex.Message}";
    }
    finally
    {
      IsImporting = false;
      StateHasChanged();
    }
  }

  private void OnProgressUpdated(ImportProgressInfo info)
  {
    int percent = info?.Percent ?? 0;
    if (percent < 0) percent = 0;
    if (percent > 100) percent = 100;

    string label = string.IsNullOrWhiteSpace(info?.Message) ? "Importing..." : info!.Message;

    if (percent == Progress && string.Equals(label, lastProgressLabel, StringComparison.Ordinal))
    {
      return;
    }

    Progress = percent;
    ProgressLabel = label;
    lastProgressLabel = label;

    _ = InvokeAsync(StateHasChanged);
  }
}
