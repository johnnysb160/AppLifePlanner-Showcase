//-----------------------------------------------------------------------
// <copyright file="MainLayout.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Layout
{
  using AppControlOrganizer.V2.Web.Data.Auth;
  using AppControlOrganizer.V2.Web.Services.Localization;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.AspNetCore.Http;
  using Microsoft.AspNetCore.Identity;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.JSInterop;

  /// <summary>
  /// Code-behind for MainLayout.razor.
  /// Loads the authenticated user's display name, profile photo and language preference for the navbar.
  /// </summary>
  public partial class MainLayout : LayoutComponentBase, IDisposable
  {
    /// <summary>Routes that render monetary values, and therefore offer the show/hide toggle.</summary>
    /// <remarks>
    /// Kept in sync with the pages that consume <c>ValuesVisibilityService</c>. Screens without any
    /// monetary value (routine, vehicles, planning, profile) must not show a control that does nothing.
    /// </remarks>
    private static readonly HashSet<string> ValueBearingRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      string.Empty,
      "accounts",
      "allocations",
      "transactions",
      "finance/year-review",
      "plan",
      "routine/vehicles",
      "planning/okrs",
    };

    /// <summary>Gets or sets the navigation manager used to resolve the active route.</summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Gets or sets the service scope factory for creating isolated DbContext scopes.</summary>
    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = null!;

    /// <summary>Gets or sets the authentication state provider.</summary>
    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    /// <summary>Gets or sets the JS runtime used to obtain the client's local hour.</summary>
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>Gets or sets the HTTP context accessor used to read the timezone cookie during SSR.</summary>
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

    private string? profilePhotoData;
    private string? userLanguage;
    private string navDisplayName = string.Empty;
    private string navInitials = "?";

    // -1 = not yet resolved; updated after first render via JS interop (client local time).
    private int clientHour = -1;

    /// <summary>Returns a time-based greeting key resolved through the current locale.</summary>
    private string Greeting => this.clientHour < 0
      ? string.Empty
      : Loc[this.clientHour < 12 ? "Good morning" : this.clientHour < 18 ? "Good afternoon" : "Good evening"];

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (firstRender)
      {
        // window.__clientHour is set synchronously by the inline script in App.razor
        // before Blazor.start() is called, so it is guaranteed to exist here.
        // This also corrects the value on mobile where the cookie may be stale.
        try
        {
          this.clientHour = await this.JSRuntime.InvokeAsync<int>(
            "lpGetClientHour", TimeSpan.FromSeconds(5));
          await this.InvokeAsync(this.StateHasChanged);
        }
        catch
        {
          // Extremely unlikely — window.__clientHour is always set before Blazor starts.
          // If it somehow fails, clientHour keeps whatever the cookie provided (or -1).
        }
      }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
      this.Navigation.LocationChanged += this.HandleLocationChanged;

      // During SSR prerender the HttpContext is available — read the timezone cookie
      // set by the client-side script on the previous request so the greeting is
      // already correct in the server-rendered HTML (no flash for returning visitors).
      HttpContext? ctx = HttpContextAccessor.HttpContext;
      if (ctx is not null
          && ctx.Request.Cookies.TryGetValue("tz_hour", out string? hourStr)
          && int.TryParse(hourStr, out int cookieHour)
          && cookieHour is >= 0 and <= 23)
      {
        clientHour = cookieHour;
      }

      AuthenticationState authState = await AuthStateProvider.GetAuthenticationStateAsync();
      if (authState.User.Identity?.IsAuthenticated == true)
      {
        // Use a dedicated scope so this DbContext does not conflict with other
        // concurrent OnInitializedAsync calls (e.g. child page components).
        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? appUser = await userManager.GetUserAsync(authState.User);
        if (appUser is not null)
        {
          profilePhotoData = appUser.ProfilePhotoData;
          string src = !string.IsNullOrWhiteSpace(appUser.DisplayName)
            ? appUser.DisplayName
            : appUser.UserName ?? string.Empty;
          navDisplayName = src;
          navInitials = BuildInitials(src);

          // Apply the user's language preference to the shared LocService instance and hand it
          // to the interactive LanguageLoader, which applies it in the circuit scope.
          userLanguage = appUser.Language;
          Loc.Set(appUser.Language);
        }
      }
      else
      {
        // Unauthenticated visitor: show localized "Visitor" label.
        navDisplayName = Loc["Visitor"];
        navInitials = Loc["Visitor"][0].ToString().ToUpperInvariant();
      }
    }

    /// <summary>Derives up to two initials from a display name or username.</summary>
    /// <param name="src">The source string (display name or username).</param>
    /// <returns>One or two uppercase initials.</returns>
    private static string BuildInitials(string src)
    {
      if (string.IsNullOrWhiteSpace(src))
      {
        return "?";
      }

      string[] parts = src.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2)
      {
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
      }

      return parts[0].Length >= 2
        ? parts[0][..2].ToUpperInvariant()
        : parts[0][..1].ToUpperInvariant();
    }

    /// <summary>Gets a value indicating whether the active route displays maskable monetary values.</summary>
    private bool ShowValuesToggle
    {
      get
      {
        string relative = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
        int marker = relative.IndexOfAny(new[] { '?', '#' });
        if (marker >= 0)
        {
          relative = relative[..marker];
        }

        return ValueBearingRoutes.Contains(relative.Trim('/'));
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      this.Navigation.LocationChanged -= this.HandleLocationChanged;
    }

    /// <summary>Re-renders the header so the values toggle follows the active route.</summary>
    private void HandleLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs args)
    {
      this.InvokeAsync(this.StateHasChanged);
    }
  }
}
