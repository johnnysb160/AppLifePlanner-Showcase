//-----------------------------------------------------------------------
// <copyright file="LanguageLoader.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Layout
{
  using AppControlOrganizer.V2.Web.Services.Localization;
  using Microsoft.AspNetCore.Components;

  /// <summary>
  /// Interactive component that initialises the <see cref="LocService"/> for the
  /// SignalR circuit scope so that all interactive child pages receive the correct
  /// language preference before they render.
  /// </summary>
  public partial class LanguageLoader : ComponentBase
  {
    /// <summary>Gets or sets the language preference of the signed-in user, resolved by the layout.</summary>
    [Parameter]
    public string? Language { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
      // Applied synchronously so the language is set in the circuit scope before any page
      // rendered after this component initializes; an asynchronous lookup here would let fast
      // pages render (and stay) in the default language.
      Loc.Set(this.Language);
    }
  }
}
