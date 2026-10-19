//-----------------------------------------------------------------------
// <copyright file="UpsertModalBase.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

/// <summary>
/// Abstract base class for upsert (create/edit) modal components.
/// Centralises the parameters and event-callback helpers shared by all modal forms:
/// Visible, IsEditing, IsSaving, OnValidSubmit, OnRequestClose, OnMarkDirty,
/// and the keyboard/close helper methods.
/// </summary>
public abstract class UpsertModalBase : ComponentBase
{
  /// <summary>Indicates whether the modal is currently visible.</summary>
  [Parameter]
  public bool Visible { get; set; }

  /// <summary>True when the modal is in edit (vs. create) mode.</summary>
  [Parameter]
  public bool IsEditing { get; set; }

  /// <summary>True while an async save operation is in progress.</summary>
  [Parameter]
  public bool IsSaving { get; set; }

  /// <summary>Callback invoked when the form submits with valid data.</summary>
  [Parameter]
  public EventCallback OnValidSubmit { get; set; }

  /// <summary>Callback invoked when the modal should close.</summary>
  [Parameter]
  public EventCallback OnRequestClose { get; set; }

  /// <summary>Callback invoked when the form data changes (marks the form dirty).</summary>
  [Parameter]
  public EventCallback OnMarkDirty { get; set; }

  /// <summary>
  /// Invokes the OnMarkDirty callback if a delegate is registered.
  /// </summary>
  protected Task MarkDirty()
    => OnMarkDirty.HasDelegate ? OnMarkDirty.InvokeAsync() : Task.CompletedTask;

  /// <summary>
  /// Invokes the OnRequestClose callback if a delegate is registered.
  /// </summary>
  protected Task RequestClose()
    => OnRequestClose.HasDelegate ? OnRequestClose.InvokeAsync() : Task.CompletedTask;

  /// <summary>
  /// Handles keyboard events — closes the modal on Escape.
  /// </summary>
  protected async Task HandleKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Escape")
    {
      await RequestClose();
    }
  }
}
