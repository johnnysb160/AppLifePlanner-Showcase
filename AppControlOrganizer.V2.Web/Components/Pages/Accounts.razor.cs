//-----------------------------------------------------------------------
// <copyright file="Accounts.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using AppControlOrganizer.V2.Shared.Entities;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Web.Components.Shared.Common;
  using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
  using AppControlOrganizer.V2.Web.Helpers;
  using AppControlOrganizer.V2.Web.Services;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.AspNetCore.Components.Forms;
  using Microsoft.AspNetCore.Components.Web;
  using Microsoft.Extensions.Logging;

  /// <summary>
  /// Code-behind for Accounts.razor. Manages bank accounts and credit cards with CRUD operations.
  /// </summary>
  public partial class Accounts : ComponentBase, IDisposable
  {
    /// <summary>
    /// Gets or sets the account identifier requested by contextual navigation.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "accountId")]
    public int? RequestedAccountId { get; set; }

    /// <summary>
    /// Gets or sets the account repository used to access account data.
    /// </summary>
    [Inject]
    public IAccountRepository AccountRepository { get; set; } = null!;

    /// <summary>
    /// Gets or sets the credit card repository used to access credit card data.
    /// </summary>
    [Inject]
    public ICreditCardRepository CreditCardRepository { get; set; } = null!;

    /// <summary>
    /// Gets or sets the scoped state container that persists account and card data across
    /// page navigations within the same Blazor circuit, eliminating redundant DB round-trips.
    /// </summary>
    [Inject]
    public AccountsStateContainer AccountsState { get; set; } = null!;

    /// <summary>
    /// Gets or sets the service used to recompute persisted account financial totals after reconciliation changes.
    /// </summary>
    [Inject]
    public FinancialSyncService FinancialSyncService { get; set; } = null!;

    /// <summary>
    /// Gets or sets the authentication state provider used to protect write handlers.
    /// </summary>
    [Inject]
    public AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    private static IEnumerable<DropdownItem<InstitutionType>> InstitutionTypeItems =>
      Enum.GetValues<InstitutionType>().Select(t => new DropdownItem<InstitutionType>(t, t.GetDisplayName()));

    private static IEnumerable<DropdownItem<AccountType?>> AccountTypeItems =>
      Enum.GetValues<AccountType>().Select(t => new DropdownItem<AccountType?>(t, t.GetDisplayName()));

    private static IEnumerable<DropdownItem<Currency>> CurrencyItems =>
      Enum.GetValues<Currency>().Select(c => new DropdownItem<Currency>(c, c.GetDisplayName()));

    private static IEnumerable<DropdownItem<CardType>> CardTypeItems =>
      Enum.GetValues<CardType>().Select(t => new DropdownItem<CardType>(t, t.GetDisplayName()));

    private static IEnumerable<DropdownItem<CardFlag>> CardFlagItems =>
      Enum.GetValues<CardFlag>().Select(f => new DropdownItem<CardFlag>(f, f.GetDisplayName()));

    private IEnumerable<DropdownItem<int>> AccountDropdownItems =>
      this.accounts.Select(a => new DropdownItem<int>(a.Id, a.Name));

    #region Drag and Drop Handlers

    /// <summary>
    /// Handles drag start for accounts.
    /// </summary>
    private void OnAccountDragStart(int accountId)
    {
      this.dragAccountId = accountId;
    }

    /// <summary>
    /// Ensure card DisplayOrder is set for existing credit cards. Backfill once if zeros detected.
    /// </summary>
    private async Task EnsureCardDisplayOrderAsync(CancellationToken cancellationToken = default)
    {
      bool needsSave = false;
      List<CreditCardEntity>? dbCards = await this.CreditCardRepository.SelectAsync(cancellationToken);
      if (dbCards == null)
      {
        return;
      }

      if (dbCards.Any(c => c.DisplayOrder == 0))
      {
        int ord = 1;
        foreach (CreditCardEntity c in dbCards)
        {
          if (c.DisplayOrder == 0)
          {
            c.DisplayOrder = ord++;
            needsSave = true;
          }
          else
          {
            ord = c.DisplayOrder + 1;
          }
        }

        if (needsSave)
        {
          try
          {
            foreach (CreditCardEntity card in dbCards.Where(c => c.DisplayOrder != 0))
            {
              await this.CreditCardRepository.UpdateAsync(card);
            }
          }
          catch
          {
          }
        }
      }
    }

    /// <summary>
    /// Handles drag over for accounts.
    /// </summary>
    private void OnAccountDragOver(DragEventArgs e)
    {
      if (e == null)
      {
        return;
      }
    }

    /// <summary>
    /// Handles drop for accounts.
    /// </summary>
    private async Task OnAccountDrop(int targetAccountId)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      if (this.dragAccountId == null)
      {
        return;
      }

      int draggedId = this.dragAccountId.Value;
      if (draggedId == targetAccountId)
      {
        this.dragAccountId = null;
        return;
      }

      AccountEntity? dragged = this.accounts.FirstOrDefault(a => a.Id == draggedId);
      AccountEntity? target = this.accounts.FirstOrDefault(a => a.Id == targetAccountId);
      if (dragged == null || target == null)
      {
        this.dragAccountId = null;
        return;
      }

      int targetIndex = this.accounts.IndexOf(target);
      this.accounts.Remove(dragged);
      this.accounts.Insert(targetIndex, dragged);

      await this.SaveAccountDisplayOrderAsync(CancellationToken.None);

      this.dragAccountId = null;
      await this.InvokeAsync(() => this.StateHasChanged());
    }

    /// <summary>
    /// Handles drag start for cards.
    /// </summary>
    private void OnCardDragStart(int cardId, int accountId)
    {
      this.dragCardId = cardId;
      this.dragCardAccountId = accountId;
    }

    /// <summary>
    /// Handles drag over for cards.
    /// </summary>
    private void OnCardDragOver(DragEventArgs e)
    {
      if (e == null)
      {
        return;
      }
    }

    /// <summary>
    /// Handles drop for cards.
    /// </summary>
    private async Task OnCardDrop(int targetCardId, int targetAccountId)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      if (this.dragCardId == null)
      {
        return;
      }

      int draggedId = this.dragCardId.Value;
      if (draggedId == targetCardId)
      {
        this.dragCardId = null;
        this.dragCardAccountId = null;
        return;
      }

      CreditCardEntity? dragged = this.cards.FirstOrDefault(c => c.Id == draggedId);
      CreditCardEntity? target = this.cards.FirstOrDefault(c => c.Id == targetCardId);
      if (dragged == null || target == null)
      {
        this.dragCardId = null;
        this.dragCardAccountId = null;
        return;
      }

      int targetIndex = this.cards.IndexOf(target);
      this.cards.Remove(dragged);
      this.cards.Insert(targetIndex, dragged);

      await this.SaveCardDisplayOrderAsync(CancellationToken.None);

      this.dragCardId = null;
      this.dragCardAccountId = null;
      await this.InvokeAsync(() => this.StateHasChanged());
    }

    /// <summary>
    /// Persist accounts display order in a single batch SaveChanges.
    /// </summary>
    private async Task SaveAccountDisplayOrderAsync(CancellationToken cancellationToken = default)
    {
      int order = 1;
      foreach (AccountEntity acc in this.accounts)
      {
        acc.DisplayOrder = order++;
      }

      try
      {
        foreach (AccountEntity acc in this.accounts)
        {
          await this.AccountRepository.UpdateAsync(acc);
        }

        await this.LoadAccountsAsync(cancellationToken);
      }
      catch
      {
      }
    }

    /// <summary>
    /// Persist card display order globally (all cards).
    /// </summary>
    private async Task SaveCardDisplayOrderAsync(CancellationToken cancellationToken = default)
    {
      int order = 1;
      foreach (CreditCardEntity c in this.cards)
      {
        c.DisplayOrder = order++;
      }

      try
      {
        foreach (CreditCardEntity c in this.cards)
        {
          await this.CreditCardRepository.UpdateAsync(c);
        }

        await this.LoadCardsAsync(cancellationToken);
      }
      catch
      {
      }
    }

    /// <summary>
    /// Ensure DisplayOrder is set for existing records. Backfill once if zeros detected.
    /// </summary>
    private async Task EnsureDisplayOrderAsync(CancellationToken cancellationToken = default)
    {
      bool needsSave = false;

      List<AccountEntity>? accounts = await this.AccountRepository.SelectAsync(cancellationToken);
      if (accounts != null && accounts.Any(a => a.DisplayOrder == 0))
      {
        int ord = 1;
        foreach (AccountEntity a in accounts.OrderBy(x => x.Id))
        {
          if (a.DisplayOrder == 0)
          {
            a.DisplayOrder = ord++;
            needsSave = true;
          }
          else
          {
            ord = a.DisplayOrder + 1;
          }
        }

        if (needsSave)
        {
          try
          {
            foreach (AccountEntity acc in accounts.Where(a => a.DisplayOrder != 0))
            {
              await this.AccountRepository.UpdateAsync(acc);
            }
          }
          catch
          {
          }
        }
      }
    }

    #endregion

    #region Account State Fields

    private List<AccountEntity> accounts = new List<AccountEntity>();
    private int? dragAccountId = null;
    private AccountEntity currentAccount = new AccountEntity();
    private AccountEntity? accountToDelete;
    private bool isInitialAccountsLoading = true;
    private bool showModal = false;
    private bool showDeleteConfirm = false;
    private bool showConfirmDiscard = false;
    private bool isEditing = false;
    private bool isSaving = false;
    private bool isDirty = false;
    private int? hoveredAccountId = null;

    #endregion

    #region Card State Fields

    private List<CreditCardEntity> cards = new List<CreditCardEntity>();
    private int? dragCardId = null;
    private int? dragCardAccountId = null;
    private CreditCardEntity currentCard = new CreditCardEntity();
    private CreditCardEntity? cardToDelete;
    private bool showCardModal = false;
    private bool showCardDeleteConfirm = false;
    private bool showCardConfirmDiscard = false;
    private bool isCardEditing = false;
    private bool isCardSaving = false;
    private bool isInitialCardsLoading = true;
    private bool isCardDirty = false;

    #endregion

    #region Message State

    private string message = string.Empty;
    private bool isError = false;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Lifecycle: initialize once after first render to avoid prerender/interactive double-load flicker.
    /// </summary>
    private bool initialized = false;
    private bool requestedAccountOpened;

    /// <inheritdoc />
    public void Dispose() => ValuesVis.OnChange -= OnValuesVisibilityChanged;

    private void OnValuesVisibilityChanged() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void OnInitialized() => ValuesVis.OnChange += OnValuesVisibilityChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (!this.initialized)
      {
        this.initialized = true;

        if (this.AccountsState.HasFreshData)
        {
          // --- Cache hit: render instantly from the in-memory snapshot ---
          this.accounts = this.AccountsState.Accounts!;
          this.cards = this.AccountsState.Cards!;
          this.isInitialAccountsLoading = false;
          this.isInitialCardsLoading = false;
          this.OpenRequestedAccountIfAvailable();
          await this.InvokeAsync(this.StateHasChanged);

          // Silently refresh in background so data stays current.
          _ = this.BackgroundRefreshAsync();
        }
        else
        {
          // --- Cache miss: full load with spinner ---
          await this.EnsureDisplayOrderAsync(CancellationToken.None);
          await this.EnsureCardDisplayOrderAsync(CancellationToken.None);
          await this.LoadAccountsAsync(CancellationToken.None, showSpinner: true);
          await this.LoadCardsAsync(CancellationToken.None, showSpinner: true);
          this.OpenRequestedAccountIfAvailable();
          await this.InvokeAsync(this.StateHasChanged);
        }
      }
    }

    /// <summary>
    /// Refreshes accounts and cards from the database in the background after a cache-hit render.
    /// Updates the state container so subsequent navigations also get a fresh snapshot.
    /// ObjectDisposedException is swallowed in case the user navigates away before completion.
    /// </summary>
    private async Task BackgroundRefreshAsync()
    {
      try
      {
        await this.EnsureDisplayOrderAsync(CancellationToken.None);
        await this.EnsureCardDisplayOrderAsync(CancellationToken.None);
        await this.LoadAccountsAsync(CancellationToken.None);
        await this.LoadCardsAsync(CancellationToken.None);
        await this.InvokeAsync(this.StateHasChanged);
      }
      catch (ObjectDisposedException)
      {
        // Component was disposed (user navigated away) before the refresh completed — safe to ignore.
      }
    }

    /// <summary>
    /// Parameterless wrapper for Razor bindings and EventCallback compatibility.
    /// </summary>
    private Task SaveAccount()
    {
      return this.SaveAccountAsync(CancellationToken.None);
    }

    /// <summary>
    /// Parameterless wrapper for Razor bindings.
    /// </summary>
    private Task DeleteAccount()
    {
      return this.DeleteAccountAsync(CancellationToken.None);
    }

    /// <summary>
    /// Parameterless wrapper for Razor bindings.
    /// </summary>
    private Task SaveCard()
    {
      return this.SaveCardAsync(CancellationToken.None);
    }

    /// <summary>
    /// Parameterless wrapper for Razor bindings.
    /// </summary>
    private Task DeleteCard()
    {
      return this.DeleteCardAsync(CancellationToken.None);
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads accounts with related data.
    /// </summary>
    private async Task LoadAccountsAsync(CancellationToken cancellationToken = default, bool showSpinner = false)
    {
      List<AccountEntity>? result = await this.AccountRepository.SelectAsync(cancellationToken);
      this.accounts = result ?? new List<AccountEntity>();

      this.isInitialAccountsLoading = false;

      // Persist the latest snapshot so the next page visit can render instantly from cache.
      this.AccountsState.Set(this.accounts, this.cards);
    }

    /// <summary>
    /// Parameterless wrapper for loading used by other callers if needed.
    /// </summary>
    private Task LoadAccounts()
    {
      return this.LoadAccountsAsync(CancellationToken.None);
    }

    private void OpenRequestedAccountIfAvailable()
    {
      if (this.requestedAccountOpened || !this.RequestedAccountId.HasValue)
      {
        return;
      }

      AccountEntity? requested = this.accounts.FirstOrDefault(account =>
        account.Id == this.RequestedAccountId.Value && !account.Deleted);
      if (requested == null)
      {
        return;
      }

      this.requestedAccountOpened = true;
      this.ShowEditModal(requested);
    }

    /// <summary>
    /// Loads credit cards.
    /// </summary>
    private async Task LoadCardsAsync(CancellationToken cancellationToken = default, bool showSpinner = false)
    {
      try
      {
        List<CreditCardEntity>? result = await this.CreditCardRepository.SelectAsync(cancellationToken);
        this.cards = result ?? new List<CreditCardEntity>();
        this.isInitialCardsLoading = false;

        // Persist the latest snapshot so the next page visit can render instantly from cache.
        this.AccountsState.Set(this.accounts, this.cards);
      }
      finally
      {
      }
    }

    /// <summary>
    /// Parameterless wrapper for loading.
    /// </summary>
    private Task LoadCards()
    {
      return this.LoadCardsAsync(CancellationToken.None);
    }

    #endregion

    #region Account Modal Operations

    /// <summary>
    /// Opens modal for creating a new account.
    /// </summary>
    private void ShowCreateModal()
    {
      this.currentAccount = new AccountEntity
      {
        AccountIsActive = true,
        Currency = Currency.BRL,
        InstitutionType = InstitutionType.Bank,
        BalanceMode = AccountBalanceMode.PositionBased,
        SupportedCurrencies = new List<Currency> { Currency.BRL }
      };
      this.isDirty = false;
      this.isEditing = false;
      this.showModal = true;
    }

    /// <summary>
    /// Opens modal for editing an existing account.
    /// </summary>
    private void ShowEditModal(AccountEntity account)
    {
      this.currentAccount = new AccountEntity
      {
        Id = account.Id,
        Name = account.Name,
        InstitutionType = account.InstitutionType,
        Logo = account.Logo,
        NumberAccount = account.NumberAccount,
        Agency = account.Agency,
        BankCode = account.BankCode,
        AccountType = account.AccountType,
        HasCard = account.HasCard,
        AccountIsActive = account.AccountIsActive,
        HasFeesOrCharges = account.HasFeesOrCharges,
        FeesOrChargesType = account.FeesOrChargesType,
        FeesOrChargesValue = account.FeesOrChargesValue,
        HasInvestments = account.HasInvestments,
        Currency = account.Currency,
        Balance = account.Balance,
        TotalInvested = account.TotalInvested,
        BalanceMode = account.BalanceMode,
        OpeningBalance = account.OpeningBalance,
        OpeningBalanceDate = account.OpeningBalanceDate,
        SupportedCurrencies = account.SupportedCurrencies.Any()
          ? new List<Currency>(account.SupportedCurrencies)
          : new List<Currency> { account.Currency }
      };
      this.isDirty = false;
      this.isEditing = true;
      this.showModal = true;
    }

    /// <summary>
    /// Closes account modal and resets state.
    /// </summary>
    private void HideModal()
    {
      this.showModal = false;
      this.showConfirmDiscard = false;
      this.isDirty = false;
      this.currentAccount = new AccountEntity();
    }

    /// <summary>
    /// Toggles a currency in the supported currencies list.
    /// </summary>
    private void ToggleSupportedCurrency(Currency currency)
    {
      if (this.currentAccount.SupportedCurrencies.Contains(currency))
      {
        this.currentAccount.SupportedCurrencies.Remove(currency);
      }
      else
      {
        this.currentAccount.SupportedCurrencies.Add(currency);
      }

      this.MarkDirty();
    }

    /// <summary>
    /// Clears all supported currencies.
    /// </summary>
    private void ClearSupportedCurrencies()
    {
      this.currentAccount.SupportedCurrencies.Clear();
      this.MarkDirty();
    }

    /// <summary>
    /// Marks account form as dirty.
    /// </summary>
    private void MarkDirty()
    {
      this.isDirty = true;
    }

    /// <summary>
    /// Handles logo file selection and converts to Base64 data URI.
    /// </summary>
    private async Task OnAccountLogoSelected(InputFileChangeEventArgs e)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      const int maxLogoSizeBytes = 512 * 1024;

      if (e.File == null)
      {
        return;
      }

      string contentType = e.File.ContentType;
      if (string.IsNullOrEmpty(contentType)
        || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
      {
        this.ShowMessage("Invalid file type. Please select an image.", true);
        return;
      }

      try
      {
  using (Stream stream = e.File.OpenReadStream(maxLogoSizeBytes))
        {
  using (MemoryStream ms = new MemoryStream())
          {
            await stream.CopyToAsync(ms);
            byte[] bytes = ms.ToArray();
            string base64 = Convert.ToBase64String(bytes);
            this.currentAccount.Logo = $"data:{contentType};base64,{base64}";
            this.MarkDirty();
          }
        }
      }
      catch (Exception ex)
      {
        this.ShowMessage($"Failed to read image: {ex.Message}", true);
      }
    }

    /// <summary>
    /// Requests account modal close.
    /// </summary>
    private void RequestCloseModal()
    {
      if (this.isDirty)
      {
        this.showConfirmDiscard = true;
      }
      else
      {
        this.HideModal();
      }
    }

    /// <summary>
    /// Handles backdrop click for account modal.
    /// </summary>
    private void OnBackdropClick()
    {
      this.RequestCloseModal();
    }

    /// <summary>
    /// Handles keyboard events for account modal.
    /// </summary>
    private void OnKeyDown(KeyboardEventArgs e)
    {
      if (e.Key == "Escape")
      {
        this.RequestCloseModal();
      }
    }

    /// <summary>
    /// Cancels discard confirmation for account.
    /// </summary>
    private void CancelDiscard()
    {
      this.showConfirmDiscard = false;
    }

    /// <summary>
    /// Confirms discard for account modal.
    /// </summary>
    private void ConfirmDiscard()
    {
      this.HideModal();
    }

    #endregion

    #region Account CRUD Operations

    /// <summary>
    /// Saves (creates or updates) the current account.
    /// </summary>
    private async Task SaveAccountAsync(CancellationToken cancellationToken = default)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      this.isSaving = true;
      this.StateHasChanged();
      await Task.Delay(1);

      //if (!this.currentAccount.SupportedCurrencies.Contains(this.currentAccount.Currency))
      //{
      //  this.currentAccount.SupportedCurrencies.Insert(0, this.currentAccount.Currency);
      //}

      if (string.IsNullOrWhiteSpace(this.currentAccount.Logo))
      {
        this.currentAccount.Logo = Helper.UnknowLogoDataUri;
      }

      if (this.isEditing)
      {
        AccountEntity? existing = await this.AccountRepository.SelectOneAsync(a => a.Id == this.currentAccount.Id, cancellationToken);
        if (existing != null)
        {
          existing.Name = this.currentAccount.Name;
          existing.InstitutionType = this.currentAccount.InstitutionType;
          existing.Logo = string.IsNullOrWhiteSpace(this.currentAccount.Logo) ? Helper.UnknowLogoDataUri : this.currentAccount.Logo;
          existing.NumberAccount = this.currentAccount.NumberAccount;
          existing.Agency = this.currentAccount.Agency;
          existing.BankCode = this.currentAccount.BankCode;
          existing.AccountType = this.currentAccount.AccountType;
          existing.HasCard = this.currentAccount.HasCard;
          existing.AccountIsActive = this.currentAccount.AccountIsActive;
          existing.HasFeesOrCharges = this.currentAccount.HasFeesOrCharges;
          existing.FeesOrChargesType = this.currentAccount.FeesOrChargesType;
          existing.FeesOrChargesValue = this.currentAccount.FeesOrChargesValue;
          existing.HasInvestments = this.currentAccount.HasInvestments;
          existing.Currency = this.currentAccount.SupportedCurrencies.First();
          existing.SupportedCurrencies = new List<Currency>(this.currentAccount.SupportedCurrencies);
          existing.BalanceMode = this.currentAccount.BalanceMode;
          existing.OpeningBalance = this.currentAccount.OpeningBalance;
          existing.OpeningBalanceDate = this.currentAccount.OpeningBalanceDate;

          await this.AccountRepository.UpdateAsync(existing);
          await this.FinancialSyncService.SyncAccountAsync(existing.Id, cancellationToken);
        }
      }
      else
      {
        await this.AccountRepository.InsertAsync(this.currentAccount);
        await this.FinancialSyncService.SyncAccountAsync(this.currentAccount.Id, cancellationToken);
      }

      await this.LoadAccountsAsync(cancellationToken);
      this.HideModal();
      this.ShowMessage(this.isEditing ? Loc["Account updated successfully!"] : Loc["Account created successfully!"], false);

      this.isSaving = false;
    }

    /// <summary>
    /// Opens delete confirmation dialog for account.
    /// </summary>
    private void ConfirmDelete(AccountEntity account)
    {
      this.accountToDelete = account;
      this.showDeleteConfirm = true;
    }

    /// <summary>
    /// Soft-deletes the selected account and all related data.
    /// </summary>
    private async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      if (this.accountToDelete == null)
      {
        return;
      }

      this.isSaving = true;
      this.StateHasChanged();
      await Task.Delay(1);

      try
      {
        AccountEntity? account = await this.AccountRepository.SelectOneAsync(a => a.Id == this.accountToDelete.Id, cancellationToken);
        if (account != null)
        {
          await this.AccountRepository.DeleteAsync(account.Id);
          await this.LoadAccountsAsync(cancellationToken);
          await this.LoadCardsAsync(cancellationToken);
          this.ShowMessage("Account deleted successfully!", false);
        }
      }
      catch (Exception ex)
      {
        this.ShowMessage($"Error deleting: {ex.Message}", true);
      }
      finally
      {
        this.isSaving = false;
        this.showDeleteConfirm = false;
        this.accountToDelete = null;
      }
    }

    #endregion

    #region Card Modal Operations

    /// <summary>
    /// Opens modal for creating a new card.
    /// </summary>
    private void ShowCreateCardModal()
    {
      this.currentCard = new CreditCardEntity
      {
        Status = true,
        Currency = Currency.BRL,
        CardType = CardType.Credit,
        Flag = CardFlag.Visa
      };
      this.isCardDirty = false;
      this.isCardEditing = false;
      this.showCardModal = true;
    }

    /// <summary>
    /// Opens modal for editing an existing card.
    /// </summary>
    private void ShowEditCardModal(CreditCardEntity card)
    {
      this.currentCard = new CreditCardEntity
      {
        Id = card.Id,
        HolderName = card.HolderName,
        CardType = card.CardType,
        Flag = card.Flag,
        Number = card.Number,
        SecurityCode = card.SecurityCode,
        Validity = card.Validity,
        HasLimit = card.HasLimit,
        LimitValue = card.LimitValue,
        HasInvoice = card.HasInvoice,
        InvoiceClosingDay = card.InvoiceClosingDay,
        InvoicePaymentDay = card.InvoicePaymentDay,
        HasAnnualFee = card.HasAnnualFee,
        AnnualFee = card.AnnualFee,
        HasBenefits = card.HasBenefits,
        Benefits = card.Benefits,
        Status = card.Status,
        Currency = card.Currency,
        AccountId = card.AccountId,
        TransactionCurrencies = new List<Currency>(card.TransactionCurrencies)
      };
      this.isCardDirty = false;
      this.isCardEditing = true;
      this.showCardModal = true;
    }

    /// <summary>
    /// Closes card modal and resets state.
    /// </summary>
    private void HideCardModal()
    {
      this.showCardModal = false;
      this.showCardConfirmDiscard = false;
      this.isCardDirty = false;
      this.currentCard = new CreditCardEntity();
    }

    /// <summary>
    /// Marks card form as dirty.
    /// </summary>
    private void MarkCardDirty()
    {
      this.isCardDirty = true;
    }

    /// <summary>
    /// Toggles a currency in the transaction currencies list.
    /// </summary>
    private void ToggleTransactionCurrency(Currency currency)
    {
      if (this.currentCard.TransactionCurrencies.Contains(currency))
      {
        this.currentCard.TransactionCurrencies.Remove(currency);
      }
      else
      {
        this.currentCard.TransactionCurrencies.Add(currency);
      }

      this.MarkCardDirty();
    }

    /// <summary>
    /// Removes a currency from the transaction currencies list.
    /// </summary>
    private void RemoveTransactionCurrency(Currency currency)
    {
      this.currentCard.TransactionCurrencies.Remove(currency);
      this.MarkCardDirty();
    }

    /// <summary>
    /// Clears all transaction currencies.
    /// </summary>
    private void ClearTransactionCurrencies()
    {
      this.currentCard.TransactionCurrencies.Clear();
      this.MarkCardDirty();
    }

    /// <summary>
    /// Requests card modal close.
    /// </summary>
    private void RequestCloseCardModal()
    {
      if (this.isCardDirty)
      {
        this.showCardConfirmDiscard = true;
      }
      else
      {
        this.HideCardModal();
      }
    }

    /// <summary>
    /// Handles backdrop click for card modal.
    /// </summary>
    private void OnCardBackdropClick()
    {
      this.RequestCloseCardModal();
    }

    /// <summary>
    /// Handles keyboard events for card modal.
    /// </summary>
    private void OnCardKeyDown(KeyboardEventArgs e)
    {
      if (e.Key == "Escape")
      {
        this.RequestCloseCardModal();
      }
    }

    /// <summary>
    /// Cancels discard confirmation for card.
    /// </summary>
    private void CancelCardDiscard()
    {
      this.showCardConfirmDiscard = false;
    }

    /// <summary>
    /// Confirms discard for card modal.
    /// </summary>
    private void ConfirmCardDiscard()
    {
      this.HideCardModal();
    }

    #endregion

    #region Card CRUD Operations

    /// <summary>
    /// Saves (creates or updates) the current card.
    /// </summary>
    private async Task SaveCardAsync(CancellationToken cancellationToken = default)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      if (this.currentCard.AccountId == 0)
      {
        this.ShowMessage("Select an account", true);
        return;
      }

      this.isCardSaving = true;
      this.StateHasChanged();
      await Task.Delay(1);

      try
      {
        if (this.isCardEditing)
        {
          CreditCardEntity? existing = await this.CreditCardRepository.SelectAsync(this.currentCard.Id);
          if (existing != null)
          {
            existing.HolderName = this.currentCard.HolderName;
            existing.CardType = this.currentCard.CardType;
            existing.Flag = this.currentCard.Flag;
            existing.Number = this.currentCard.Number;
            existing.SecurityCode = this.currentCard.SecurityCode;
            existing.Validity = this.currentCard.Validity;
            existing.HasLimit = this.currentCard.HasLimit;
            existing.LimitValue = this.currentCard.LimitValue;
            existing.HasInvoice = this.currentCard.HasInvoice;
            existing.InvoiceClosingDay = this.currentCard.InvoiceClosingDay;
            existing.InvoicePaymentDay = this.currentCard.InvoicePaymentDay;
            existing.Status = this.currentCard.Status;
            existing.Currency = this.currentCard.Currency;
            existing.AccountId = this.currentCard.AccountId;
            existing.TransactionCurrencies = new List<Currency>(this.currentCard.TransactionCurrencies);

            await this.CreditCardRepository.UpdateAsync(existing);
          }
        }
        else
        {
          await this.CreditCardRepository.InsertAsync(this.currentCard);
        }

        await this.LoadCardsAsync(cancellationToken);
        await this.LoadAccountsAsync(cancellationToken);
        this.HideCardModal();
        this.ShowMessage(this.isCardEditing ? "Card updated successfully!" : "Card created successfully!", false);
      }
      catch (Exception ex)
      {
        this.ShowMessage($"Error saving: {ex.Message}", true);
      }
      finally
      {
        this.isCardSaving = false;
      }
    }

    /// <summary>
    /// Opens delete confirmation dialog for card.
    /// </summary>
    private void ConfirmDeleteCard(CreditCardEntity card)
    {
      this.cardToDelete = card;
      this.showCardDeleteConfirm = true;
    }

    /// <summary>
    /// Soft-deletes the selected card.
    /// </summary>
    private async Task DeleteCardAsync(CancellationToken cancellationToken = default)
    {
      if (!await this.EnsureWriteAllowedAsync())
      {
        return;
      }

      if (this.cardToDelete == null)
      {
        return;
      }

      this.isCardSaving = true;
      this.StateHasChanged();
      await Task.Delay(1);

      try
      {
        CreditCardEntity? card = await this.CreditCardRepository.SelectOneAsync(c => c.Id == this.cardToDelete.Id, cancellationToken);
        if (card != null)
        {
          await this.CreditCardRepository.DeleteAsync(card.Id);
          await this.LoadCardsAsync(cancellationToken);
          await this.LoadAccountsAsync(cancellationToken);
          this.ShowMessage("Card deleted successfully!", false);
        }
      }
      catch (Exception ex)
      {
        this.ShowMessage($"Error deleting: {ex.Message}", true);
      }
      finally
      {
        this.isCardSaving = false;
        this.showCardDeleteConfirm = false;
        this.cardToDelete = null;
      }
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// Displays a temporary toast message.
    /// </summary>
    private void ShowMessage(string msg, bool error)
    {
      this.message = msg;
      this.isError = error;

      _ = this.ClearMessageAfterDelayAsync(CancellationToken.None);
    }

    private async Task<bool> EnsureWriteAllowedAsync()
    {
      AuthenticationState state = await this.AuthStateProvider.GetAuthenticationStateAsync();
      if (state.User.Identity?.IsAuthenticated == true)
      {
        return true;
      }

      this.ShowMessage(Loc[AppControlOrganizer.V2.Web.Data.DemoReadOnlyException.MessageKey], true);
      return false;
    }

    /// <summary>
    /// Clears the message after a delay and requests UI update.
    /// </summary>
    private async Task ClearMessageAfterDelayAsync(CancellationToken cancellationToken = default)
    {
      await Task.Delay(3000, cancellationToken);
      this.message = string.Empty;
      await this.InvokeAsync(() => this.StateHasChanged());
    }

    /// <summary>
    /// Gets all currencies related to an account (account currency, allocations, cards).
    /// </summary>
    private static IReadOnlyList<Currency> GetAccountCurrencies(AccountEntity account)
    {
      HashSet<Currency> currencies = new HashSet<Currency> { account.Currency };

      foreach (AllocationEntity alloc in account.Allocations)
      {
        currencies.Add(alloc.Currency);
        if (alloc.SecondCurrency.HasValue)
        {
          currencies.Add(alloc.SecondCurrency.Value);
        }
      }

      foreach (CreditCardEntity card in account.Cards)
      {
        currencies.Add(card.Currency);
        foreach (Currency transactionCurrency in card.TransactionCurrencies)
        {
          currencies.Add(transactionCurrency);
        }
      }

      return currencies.OrderBy(c => c.ToString()).ToList();
    }

    #endregion
  }
}
