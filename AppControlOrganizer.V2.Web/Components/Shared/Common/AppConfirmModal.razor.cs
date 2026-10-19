//-----------------------------------------------------------------------
// <copyright file="AppConfirmModal.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AppControlOrganizer.V2.Web.Components.Shared.Common
{
  public partial class AppConfirmModal : ComponentBase
  {
    /// <summary>Whether the modal is visible.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Modal title.</summary>
    [Parameter] public string Title { get; set; } = "Confirm";

    /// <summary>Optional HTML message (used when Body is null).</summary>
    [Parameter] public string? Message { get; set; }

    /// <summary>Optional custom body content.</summary>
    [Parameter] public RenderFragment? Body { get; set; }

    /// <summary>Confirm button text.</summary>
    [Parameter] public string ConfirmText { get; set; } = "Confirm";

    /// <summary>Cancel button text.</summary>
    [Parameter] public string CancelText { get; set; } = "Cancel";

    /// <summary>Bootstrap classes for confirm button (e.g. btn-danger).</summary>
    [Parameter] public string ConfirmButtonCss { get; set; } = "btn-primary";

    /// <summary>Extra CSS for dialog.</summary>
    [Parameter] public string DialogExtraCss { get; set; } = string.Empty;

    /// <summary>Optional size CSS (e.g. modal-sm, modal-lg).</summary>
    [Parameter] public string DialogSizeCss { get; set; } = string.Empty;

    /// <summary>Backdrop opacity (0.0 - 1.0).</summary>
    [Parameter] public double BackdropOpacity { get; set; } = 0.5;

    /// <summary>Disables confirm action without marking busy.</summary>
    [Parameter] public bool DisableConfirm { get; set; }

    /// <summary>Marks modal as busy (disables buttons and optionally shows spinner).</summary>
    [Parameter] public bool IsBusy { get; set; }

    /// <summary>Whether to show a spinner on confirm while busy.</summary>
    [Parameter] public bool ShowSpinnerOnConfirm { get; set; } = true;

    /// <summary>Whether to show the X close button.</summary>
    [Parameter] public bool ShowCloseButton { get; set; } = true;

    /// <summary>Raised when user confirms.</summary>
    [Parameter] public EventCallback OnConfirm { get; set; }

    /// <summary>Raised when user cancels (or closes).</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private async Task ConfirmAsync()
    {
      if (IsBusy || DisableConfirm)
      {
        return;
      }

      if (OnConfirm.HasDelegate)
      {
        await OnConfirm.InvokeAsync();
      }
    }

    private async Task CancelAsync()
    {
      if (IsBusy)
      {
        return;
      }

      if (OnCancel.HasDelegate)
      {
        await OnCancel.InvokeAsync();
      }
    }

    private async Task HandleBackdropClick()
    {
      // Keep behavior consistent with previous pages: backdrop click cancels.
      await CancelAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
      // ESC closes/cancels.
      if (e.Key == "Escape")
      {
        await CancelAsync();
      }
    }
  }
}
