//-----------------------------------------------------------------------
// <copyright file="CategoryManagementModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Components.Shared.Common;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

namespace AppControlOrganizer.V2.Web.Components.Shared.Categories;

/// <summary>
/// Code-behind for CategoryManagementModal. Provides full CRUD for categories. Predefined categories can be edited but
/// not deleted. User-defined categories support full management.
/// </summary>
public partial class CategoryManagementModal :
          ComponentBase
{
  [Inject]
  public CategoryService CategoryService { get; set; } = null!;

  [Parameter]
  public EventCallback OnCategoriesChanged { get; set; }

  [Parameter]
  public EventCallback OnClosed { get; set; }

  [Parameter]
  public int? ContextMonth { get; set; }

  [Parameter]
  public int? ContextYear { get; set; }

  [Inject]
  private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

  private bool IsVisible { get; set; }

  private bool isLoading;
  private bool isSaving;
  private bool showForm;
  private bool isEditMode;
  private bool showDeleteConfirm;

  private string errorMessage = string.Empty;
  private string successMessage = string.Empty;

  private List<CategoryDisplayDto> categories = new List<CategoryDisplayDto>();
  private CategoryDisplayDto? editingCategory;
  private CategoryDisplayDto? categoryToDelete;

  // Form fields
  private string formName = string.Empty;
  private string formIconCss = "bi bi-tag";
  private bool formIsIncome;

  #region Dropdown Items

  private static IEnumerable<DropdownItem<bool>> IsIncomeItems =>
    new List<DropdownItem<bool>>
    {
      new DropdownItem<bool>(false, "Expense"),
      new DropdownItem<bool>(true, "Income")
    };

  private static IEnumerable<DropdownItem<string>> IconDropdownItems =>
    AvailableIcons.Select(kv => new DropdownItem<string>(kv.Key, kv.Value));

  #endregion

  /// <summary>
  /// Available icons for category selection.
  /// </summary>
  // Bootstrap icon options for categories. Stored as CSS class names.
  private static readonly Dictionary<string, string> AvailableIcons = new Dictionary<string, string>
  {
    { "bi bi-tag", "Tag" },
    { "bi bi-wallet2", "Wallet" },
    { "bi bi-cash-coin", "Cash" },
    { "bi bi-house", "House" },
    { "bi bi-lightbulb", "Lightbulb" },
    { "bi bi-egg-fried", "Food" },
    { "bi bi-briefcase", "Briefcase" },
    { "bi bi-wifi", "WiFi" },
    { "bi bi-heart-pulse", "Health" },
    { "bi bi-car-front", "Car" },
    { "bi bi-controller", "Controller" },
    { "bi bi-tags", "Tags" },
    { "bi bi-mortarboard", "Education" },
    { "bi bi-cart", "Cart" },
    { "bi bi-airplane", "Travel" },
    { "bi bi-phone", "Phone" },
    { "bi bi-piggy-bank", "Savings" },
    { "bi bi-building", "Building" },
    { "bi bi-house-door", "House Door" },
    { "bi bi-calendar", "Calendar" },
    { "bi bi-clock", "Clock" },
    { "bi bi-graph-up", "Graph Up" },
    { "bi bi-graph-down", "Graph Down" },
    { "bi bi-people", "People" },
    { "bi bi-person-circle", "Person" },
    { "bi bi-shield-lock", "Security" },
    { "bi bi-cloud", "Cloud" },
    { "bi bi-briefcase-fill", "Briefcase Fill" },
    { "bi bi-coin", "Coin" },
    { "bi bi-bank", "Bank" },
    { "bi bi-credit-card", "Credit Card" },
    { "bi bi-app-indicator", "App" },
    { "bi bi-basket", "Basket" },
    { "bi bi-bag", "Bag" },
    { "bi bi-bar-chart", "Bar Chart" },
    { "bi bi-bell", "Bell" },
    { "bi bi-bicycle", "Bicycle" },
    { "bi bi-book", "Book" },
    { "bi bi-box", "Box" },
    { "bi bi-brush", "Brush" },
    { "bi bi-building-fill", "Building Fill" },
    { "bi bi-bug", "Bug" },
    { "bi bi-bullseye", "Bullseye" },
    { "bi bi-bus-front", "Bus" },
    { "bi bi-calendar-event", "Calendar Event" },
    { "bi bi-camera", "Camera" },
    { "bi bi-camera-reels", "Camera Reels" },
    { "bi bi-chat", "Chat" },
    { "bi bi-check2-circle", "Check" },
    { "bi bi-chevron-right", "Chevron Right" },
    { "bi bi-cloud-arrow-up", "Cloud Upload" },
    { "bi bi-cloud-arrow-down", "Cloud Download" },
    { "bi bi-code", "Code" },
    { "bi bi-compass", "Compass" },
    { "bi bi-cpu", "CPU" },
    { "bi bi-cup", "Cup" },
    { "bi bi-database", "Database" },
    { "bi bi-droplet", "Droplet" },
    { "bi bi-envelope", "Envelope" },
    { "bi bi-exclamation-circle", "Alert" },
    { "bi bi-eye", "Eye" },
    { "bi bi-file-earmark", "File" },
    { "bi bi-flag", "Flag" },
    { "bi bi-folder", "Folder" },
    { "bi bi-funnel", "Funnel" },
    { "bi bi-gem", "Gem" },
    { "bi bi-geo-alt", "Location" },
    { "bi bi-gift-fill", "Gift Fill" },
    { "bi bi-gear", "Settings" },
    { "bi bi-graph-up-arrow", "Graph Up Arrow" },
    { "bi bi-grid", "Grid" },
    { "bi bi-hdd", "HDD" },
    { "bi bi-heart", "Heart" },
    { "bi bi-house-fill", "House Fill" },
    { "bi bi-image", "Image" },
    { "bi bi-inbox", "Inbox" },
    { "bi bi-info-circle", "Info" },
    { "bi bi-key", "Key" },
    { "bi bi-laptop", "Laptop" },
    { "bi bi-layers", "Layers" },
    { "bi bi-link-45deg", "Link" },
    { "bi bi-list", "List" },
    { "bi bi-lock", "Lock" },
    { "bi bi-map", "Map" },
    { "bi bi-megaphone", "Megaphone" },
    { "bi bi-menu-button", "Menu" },
    { "bi bi-mic", "Mic" },
    { "bi bi-minecart", "Minecart" },
    { "bi bi-moon", "Moon" },
    { "bi bi-mortarboard-fill", "Education Fill" },
    { "bi bi-music-note", "Music" },
    { "bi bi-newspaper", "News" },
    { "bi bi-palette", "Palette" },
    { "bi bi-paperclip", "Paperclip" },
    { "bi bi-patch-check", "Patch Check" },
    { "bi bi-percent", "Percent" },
    { "bi bi-person-badge", "Person Badge" },
    { "bi bi-phone-fill", "Phone Fill" },
    { "bi bi-pin-map", "Pin" },
    { "bi bi-power", "Power" },
    { "bi bi-receipt", "Receipt" },
    { "bi bi-recycle", "Recycle" },
    { "bi bi-record-circle", "Record" },
    { "bi bi-rocket", "Rocket" },
    { "bi bi-rollers", "Rollers" },
    { "bi bi-scissors", "Scissors" },
    { "bi bi-search", "Search" },
    { "bi bi-shop", "Shop" },
    { "bi bi-skip-forward", "Skip Forward" },
    { "bi bi-sliders", "Sliders" },
    { "bi bi-snow", "Snow" },
    { "bi bi-soundwave", "Sound" },
    { "bi bi-speaker", "Speaker" },
    { "bi bi-speedometer2", "Speedometer" },
    { "bi bi-star", "Star" },
    { "bi bi-telephone", "Telephone" },
    { "bi bi-terminal", "Terminal" },
    { "bi bi-ticket", "Ticket" },
    { "bi bi-trash", "Trash" },
    { "bi bi-trophy", "Trophy" },
    { "bi bi-tv", "TV" },
    { "bi bi-umbrella", "Umbrella" },
    { "bi bi-unlock", "Unlock" },
    { "bi bi-upload", "Upload" },
    { "bi bi-usb", "USB" },
    { "bi bi-vehicle", "Vehicle" },
    { "bi bi-wallet", "Wallet (alt)" },
    { "bi bi-wallet-fill", "Wallet Fill" }
  };

  /// <summary>
  /// Handle keyboard events for modal (Escape to close).
  /// </summary>
  private void OnKeyDown(KeyboardEventArgs e)
  {
    if (e == null)
    {
      return;
    }

    if (string.Equals(e.Key, "Escape", StringComparison.Ordinal))
    {
      Hide();
    }
  }

  /// <summary>
  /// Shows the modal and loads category data.
  /// </summary>
  public Task ShowAsync()
  {
    IsVisible = true;
    ResetForm();

    StateHasChanged();
    return Task.CompletedTask;
  }

  /// <summary>
  /// Invoked after the component has rendered. Performs additional initialization or state updates as needed.
  /// </summary>
  /// <remarks>This method is typically used to trigger actions that should occur only after the component is
  /// rendered, such as loading data or updating state. When <paramref name="firstRender"/> is <see langword="true"/>, it
  /// is common to perform one-time setup tasks.</remarks>
  /// <param name="firstRender">Indicates whether this is the first time the component has rendered. If <see langword="true"/>, initialization
  /// logic may be executed.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (IsVisible && categories.Count == 0 && !isLoading)
    {
      isLoading = true;

      await LoadCategoriesAsync();

      isLoading = false;
      StateHasChanged();
    }
  }

  /// <summary>
  /// Hides the modal.
  /// </summary>
  public void Hide()
  {
    IsVisible = false;
    ResetForm();
    StateHasChanged();
    _ = OnClosed.InvokeAsync();
  }

  /// <summary>
  /// Handles backdrop click to close modal.
  /// </summary>
  private void OnBackdropClick()
  {
    // Close immediately when clicking outside the modal.
    Hide();
  }

  /// <summary>
  /// Loads all categories with usage counts from the service.
  /// </summary>
  private async Task LoadCategoriesAsync()
  {
    isLoading = true;
    errorMessage = string.Empty;
    StateHasChanged();

    try
    {
      categories = await CategoryService.GetAllAsync();
    }
    catch (Exception ex)
    {
      errorMessage = $"Error loading categories: {ex.Message}";
    }
    finally
    {
      isLoading = false;
    }
  }

  /// <summary>
  /// Shows the add form.
  /// </summary>
  private void ShowAddForm()
  {
    ResetForm();
    showForm = true;
    isEditMode = false;
  }

  /// <summary>
  /// Shows the edit form for a category.
  /// </summary>
  private void ShowEditForm(CategoryDisplayDto category)
  {
    editingCategory = category;
    formName = category.Name;
    formIconCss = category.IconCss;
    formIsIncome = category.IsIncome;
    showForm = true;
    isEditMode = true;
    errorMessage = string.Empty;
    successMessage = string.Empty;
  }

  /// <summary>
  /// Cancels the form and resets state.
  /// </summary>
  private void CancelForm()
  {
    ResetForm();
  }

  /// <summary>
  /// Resets form fields and state.
  /// </summary>
  private void ResetForm()
  {
    showForm = false;
    isEditMode = false;
    editingCategory = null;
    formName = string.Empty;
    formIconCss = "bi bi-tag";
    formIsIncome = false;
    errorMessage = string.Empty;
    successMessage = string.Empty;
  }

  /// <summary>
  /// Saves the category (create or update).
  /// </summary>
  private async Task SaveCategory()
  {
    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(formName))
    {
      errorMessage = "Category name is required.";
      return;
    }

    isSaving = true;
    errorMessage = string.Empty;
    successMessage = string.Empty;
    StateHasChanged();
    await Task.Delay(1);
    try
    {
      CategoryDto dto = new CategoryDto
      {
        Name = formName.Trim(),
        IconCss = formIconCss,
        IsIncome = formIsIncome
      };

      if (isEditMode && editingCategory != null)
      {
        await CategoryService.UpdateAsync(editingCategory.Id, dto);
        successMessage = $"Category '{dto.Name}' updated successfully.";
      }
      else
      {
        await CategoryService.CreateAsync(dto);
        successMessage = $"Category '{dto.Name}' created successfully.";
      }

      ResetForm();
      await LoadCategoriesAsync();
      await OnCategoriesChanged.InvokeAsync();
    }
    catch (InvalidOperationException ex)
    {
      errorMessage = ex.Message;
    }
    catch (Exception ex)
    {
      errorMessage = $"Error saving category: {ex.Message}";
    }
    finally
    {
      isSaving = false;
    }
  }

  /// <summary>
  /// Shows delete confirmation for a category.
  /// </summary>
  private void ConfirmDelete(CategoryDisplayDto category)
  {
    categoryToDelete = category;
    showDeleteConfirm = true;
    errorMessage = string.Empty;
    successMessage = string.Empty;
  }

  /// <summary>
  /// Cancels delete confirmation.
  /// </summary>
  private void CancelDelete()
  {
    categoryToDelete = null;
    showDeleteConfirm = false;
  }

  /// <summary>
  /// Deletes the selected category.
  /// </summary>
  private async Task DeleteCategory()
  {
    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    if (categoryToDelete == null)
    {
      return;
    }

    isSaving = true;
    errorMessage = string.Empty;
    StateHasChanged();
    await Task.Delay(1);
    try
    {
      await CategoryService.DeleteAsync(categoryToDelete.Id);
      successMessage = $"Category '{categoryToDelete.Name}' deleted successfully.";
      showDeleteConfirm = false;
      categoryToDelete = null;
      await LoadCategoriesAsync();
      await OnCategoriesChanged.InvokeAsync();
    }
    catch (InvalidOperationException ex)
    {
      errorMessage = ex.Message;
      showDeleteConfirm = false;
    }
    catch (Exception ex)
    {
      errorMessage = $"Error deleting category: {ex.Message}";
      showDeleteConfirm = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private string GetCategoryTransactionsHref(CategoryDisplayDto category)
  {
    string type = category.IsIncome ? "income" : "expense";
    string period = ContextMonth is >= 1 and <= 12 && ContextYear.HasValue
      ? $"month={ContextMonth.Value}&year={ContextYear.Value}&"
      : string.Empty;

    if (!string.IsNullOrWhiteSpace(category.Code))
    {
      return $"transactions?{period}category={Uri.EscapeDataString(category.Code)}&type={type}";
    }

    return $"transactions?{period}categoryId={category.Id}&type={type}";
  }

  private async Task<bool> EnsureWriteAllowedAsync()
  {
    AuthenticationState state = await AuthStateProvider.GetAuthenticationStateAsync();
    if (state.User.Identity?.IsAuthenticated == true)
    {
      return true;
    }

    errorMessage = Loc[DemoReadOnlyException.MessageKey];
    return false;
  }
}
