//-----------------------------------------------------------------------
// <copyright file="SearchableDropdown.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

/// <summary>
/// Code-behind for SearchableDropdown.razor. Provides a searchable dropdown with filter and clear functionality.
/// </summary>
/// <typeparam name="TValue">The type of the value (typically int, string, or enum).</typeparam>
public partial class SearchableDropdown<TValue> :
          ComponentBase
{
  /// <summary>
  /// JS runtime used to position the floating menu via getBoundingClientRect.
  /// </summary>
  [Inject]
  private IJSRuntime JS { get; set; } = null!;

  /// <summary>
  /// Reference to the outer container element. Passed to JS to measure its
  /// viewport position so the menu can be placed with position:fixed.
  /// </summary>
  private ElementReference dropdownContainerRef;
  /// <summary>
  /// The list of items to display in the dropdown.
  /// </summary>
  [Parameter]
  public IEnumerable<DropdownItem<TValue>> Items { get; set; } = Enumerable.Empty<DropdownItem<TValue>>();

  /// <summary>
  /// The currently selected value.
  /// </summary>
  [Parameter]
  public TValue? Value { get; set; }

  /// <summary>
  /// Callback when the value changes.
  /// </summary>
  [Parameter]
  public EventCallback<TValue?> ValueChanged { get; set; }

  /// <summary>
  /// The value that represents "no selection" (e.g., 0 for int, null for nullable types).
  /// </summary>
  [Parameter]
  public TValue? EmptyValue { get; set; }

  /// <summary>
  /// Placeholder text for the input.
  /// </summary>
  [Parameter]
  public string Placeholder { get; set; } = "Select...";

  /// <summary>
  /// Whether to show the clear button.
  /// </summary>
  [Parameter]
  public bool ShowClearButton { get; set; } = true;

  /// <summary>
  /// Whether the dropdown is disabled.
  /// </summary>
  [Parameter]
  public bool Disabled { get; set; }

  /// <summary>
  /// Additional CSS class for the input.
  /// </summary>
  [Parameter]
  public string? CssClass { get; set; }

  /// <summary>
  /// Callback when value changes (for dirty tracking).
  /// </summary>
  [Parameter]
  public EventCallback OnChange { get; set; }

  private string searchText = string.Empty;
  private bool isOpen;
  private bool isFocused;
  private List<DropdownItem<TValue>> cachedItems = new List<DropdownItem<TValue>>();

  private IEnumerable<DropdownItem<TValue>> FilteredItems
  {
    get
    {
      if (string.IsNullOrWhiteSpace(searchText))
      {
        bool hasSelection = Value != null && !EqualityComparer<TValue>.Default.Equals(Value, EmptyValue);
        if (hasSelection)
        {
          IEnumerable<DropdownItem<TValue>> selected = cachedItems.Where(i => EqualityComparer<TValue>.Default.Equals(i.Value, Value));
          IEnumerable<DropdownItem<TValue>> rest     = cachedItems.Where(i => !EqualityComparer<TValue>.Default.Equals(i.Value, Value));
          return selected.Concat(rest);
        }
        return cachedItems;
      }

      return cachedItems.Where(i => i.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
  }

  /// <inheritdoc />
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    // Whenever the dropdown is open (including after each filter keystroke
    // re-render) re-calculate the menu's viewport position.  The JS function
    // writes --dd-top / --dd-left / --dd-width as CSS custom properties on the
    // container element; the menu reads them via var() in its inline style and
    // positions itself with position:fixed — above every stacking context.
    if (isOpen)
    {
      try
      {
        await JS.InvokeVoidAsync("positionSearchableDropdown", dropdownContainerRef);
      }
      catch (JSDisconnectedException) { }
    }
  }

  protected override void OnParametersSet()
  {
    base.OnParametersSet();

    cachedItems = Items.ToList();

    // Update search text to match current value's text
    if (!isFocused)
    {
      UpdateSearchTextFromValue();
    }
  }

  private void UpdateSearchTextFromValue()
  {
    if (EqualityComparer<TValue>.Default.Equals(Value, EmptyValue) || Value == null)
    {
      searchText = string.Empty;
    }
    else
    {
      DropdownItem<TValue>? selectedItem = cachedItems.FirstOrDefault(i => EqualityComparer<TValue>.Default
                                                    .Equals(i.Value, Value));
      searchText = selectedItem?.Text ?? string.Empty;
    }
  }

  private void OnInputFocus()
  {
    searchText = string.Empty;
    isOpen = true;
    isFocused = true;
  }

  private void OnFocusIn()
  {
    isFocused = true;
  }

  private async Task OnFocusOut()
  {
    // Small delay to allow click events on dropdown items to fire first
    await Task.Delay(150);

    isFocused = false;
    isOpen = false;

    // If text doesn't match any item, revert to current value's text
    DropdownItem<TValue>? matchingItem = cachedItems.FirstOrDefault(i => i.Text
                                      .Equals(searchText, StringComparison.OrdinalIgnoreCase));
    if (matchingItem == null)
    {
      UpdateSearchTextFromValue();
    }
    else if (!EqualityComparer<TValue>.Default.Equals(matchingItem.Value, Value))
    {
      // Auto-select if exact match found
      await SelectItem(matchingItem);
    }

    StateHasChanged();
  }

  private void OnSearchChanged()
  {
    isOpen = true;
  }

  private async Task OnKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Escape")
    {
      isOpen = false;
      UpdateSearchTextFromValue();
    }
    else if (e.Key == "Enter" && isOpen)
    {
      IEnumerable<DropdownItem<TValue>> filtered = FilteredItems;
      if (filtered.Any())
      {
                await SelectItem(filtered.First());
      }
    }
  }

  private async Task SelectItem(DropdownItem<TValue> item)
  {
    searchText = item.Text;
    isOpen = false;

    if (!EqualityComparer<TValue>.Default.Equals(item.Value, Value))
    {
      Value = item.Value;
      await ValueChanged.InvokeAsync(Value);
      await OnChange.InvokeAsync();
    }
  }

  private async Task ClearValue()
  {
    searchText = string.Empty;
    isOpen = false;

    if (!EqualityComparer<TValue>.Default.Equals(Value, EmptyValue))
    {
      Value = EmptyValue;
      await ValueChanged.InvokeAsync(Value);
      await OnChange.InvokeAsync();
    }
  }

  /// <summary>
  /// Determines whether the dropdown has a value or search text to show the clear button.
  /// </summary>
  private bool HasValueOrText()
  {
    bool hasValue = !EqualityComparer<TValue>.Default.Equals(Value, EmptyValue);
    bool hasText = !string.IsNullOrEmpty(searchText);
    return hasValue || hasText;
  }
}

/// <summary>
/// Represents an item in the dropdown.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class DropdownItem<TValue>
{
  /// <summary>
  /// The value of the item.
  /// </summary>
  public TValue Value { get; set; } = default!;

  /// <summary>
  /// The display text of the item.
  /// </summary>
  public string Text { get; set; } = string.Empty;

  /// <summary>
  /// Creates a new DropdownItem.
  /// </summary>
  public DropdownItem() { }

  /// <summary>
  /// Creates a new DropdownItem with the specified value and text.
  /// </summary>
  public DropdownItem(TValue value, string text)
  {
    Value = value;
    Text = text;
  }
}
