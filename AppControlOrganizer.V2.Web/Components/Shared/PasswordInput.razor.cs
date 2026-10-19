//-----------------------------------------------------------------------
// <copyright file="PasswordInput.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared
{
  using Microsoft.AspNetCore.Components;

  /// <summary>Password input component with show/hide visibility toggle.</summary>
  public partial class PasswordInput : ComponentBase
  {
    /// <summary>Gets or sets the id attribute of the input element.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Gets or sets the current value.</summary>
    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>Gets or sets the callback invoked when the value changes.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Gets or sets the placeholder text.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    private bool show;

    private void ToggleVisibility()
    {
      show = !show;
    }

    private async Task OnInput(ChangeEventArgs e)
    {
      await ValueChanged.InvokeAsync(e.Value?.ToString() ?? string.Empty);
    }
  }
}
