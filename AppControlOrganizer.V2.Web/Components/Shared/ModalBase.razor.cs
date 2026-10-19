//-----------------------------------------------------------------------
// <copyright file="ModalBase.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AppControlOrganizer.V2.Web.Components.Shared
{
  /// <summary>
  /// Code-behind for ModalBase.razor. Reusable modal component with Bootstrap styling and dirty state confirmation.
  /// </summary>
  public partial class ModalBase :
            ComponentBase
  {
    #region Parameters

    /// <summary>
    /// Modal visibility state.
    /// </summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>
    /// Callback when modal visibility changes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> IsVisibleChanged { get; set; }

    /// <summary>
    /// Modal title text.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating if form has unsaved changes.
    /// </summary>
    [Parameter]
    public bool IsDirty { get; set; }

    /// <summary>
    /// Modal size: "sm", "lg", "xl", or empty for default.
    /// </summary>
    [Parameter]
    public string Size { get; set; } = string.Empty;

    /// <summary>
    /// Modal body content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Modal footer content (buttons, etc.).
    /// </summary>
    [Parameter]
    public RenderFragment? FooterContent { get; set; }

    /// <summary>
    /// Callback when modal is closed (after confirmation if dirty).
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    #endregion

    #region State Fields

    /// <summary>
    /// Internal state for confirmation dialog.
    /// </summary>
    private bool showConfirmClose = false;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Computes Bootstrap size class based on Size parameter.
    /// </summary>
    private string SizeClass => Size switch
    {
      "sm" => "modal-sm",
      "lg" => "modal-lg",
      "xl" => "modal-xl",
      _ => string.Empty
    };

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles backdrop (outside) click.
    /// </summary>
    private async Task OnBackdropClick()
    {
      await RequestClose();
    }

    /// <summary>
    /// Handles keyboard events (ESC key).
    /// </summary>
    private async Task OnKeyDown(KeyboardEventArgs e)
    {
      if (e.Key == "Escape")
      {
        await RequestClose();
      }
    }

    /// <summary>
    /// Initiates close request - shows confirmation if dirty.
    /// </summary>
    private async Task RequestClose()
    {
      if (IsDirty)
      {
        showConfirmClose = true;
      }
      else
      {
        await CloseModal();
      }
    }

    /// <summary>
    /// Cancels the close confirmation dialog.
    /// </summary>
    private void CancelClose()
    {
      showConfirmClose = false;
    }

    /// <summary>
    /// Confirms close and discards changes.
    /// </summary>
    private async Task ConfirmClose()
    {
      showConfirmClose = false;
      await CloseModal();
    }

    /// <summary>
    /// Actually closes the modal and notifies parent.
    /// </summary>
    private async Task CloseModal()
    {
      await IsVisibleChanged.InvokeAsync(false);
      await OnClose.InvokeAsync();
    }

    #endregion
  }
}
