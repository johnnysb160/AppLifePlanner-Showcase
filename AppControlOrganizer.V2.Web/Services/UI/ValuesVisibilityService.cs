/*
//-----------------------------------------------------------------------
// <copyright file="ValuesVisibilityService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------
*/

namespace AppControlOrganizer.V2.Web.Services.UI;

/// <summary>
/// Scoped service that controls whether monetary values are visible or masked.
/// Default: hidden. State lives only for the duration of the browser session.
/// </summary>
public sealed class ValuesVisibilityService
{
    public bool IsVisible { get; private set; } = false;

    public event Action? OnChange;

    public void Toggle()
    {
        IsVisible = !IsVisible;
        OnChange?.Invoke();
    }

    public void SetVisible(bool visible)
    {
        if (IsVisible == visible) return;
        IsVisible = visible;
        OnChange?.Invoke();
    }

	/// <summary>
	/// Returns the value formatted, or "•••••" if hidden.
	/// </summary>
	public string Mask(string formattedValue) => IsVisible ? formattedValue : "•••••";
}
