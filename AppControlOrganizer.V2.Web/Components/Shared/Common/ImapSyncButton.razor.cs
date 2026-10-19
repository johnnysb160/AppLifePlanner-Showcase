//-----------------------------------------------------------------------
// <copyright file="ImapSyncButton.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common
{
  using System.Security.Claims;
  using AppControlOrganizer.V2.Web.Data;
  using AppControlOrganizer.V2.Web.Services.Analytics;
  using AppControlOrganizer.V2.Web.Services.Imap;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  /// <summary>
  /// Renders a "Sync Now" button when the current user has IMAP mappings configured.
  /// All sync logic lives here; consuming pages only declare the component tag.
  /// </summary>
  public partial class ImapSyncButton : ComponentBase
  {
    /// <summary>Gets or sets the callback invoked after a successful sync so the parent page can refresh its data.</summary>
    [Parameter]
    public EventCallback OnSynced { get; set; }

    /// <summary>Gets or sets the CSS classes applied to the label span. Defaults to the responsive hide used by the action bar.</summary>
    [Parameter]
    public string LabelCssClass { get; set; } = "d-none d-xl-inline ms-1";

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = null!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    [Inject]
    private IAccessAnalyticsRecorder AccessAnalyticsRecorder { get; set; } = null!;

    private bool hasImapMappings;
    private bool running;
    private string? userId;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
      AuthenticationState auth = await AuthStateProvider.GetAuthenticationStateAsync();
      userId = auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      using IServiceScope scope = ScopeFactory.CreateScope();
      IDbContextFactory<LifePlannerContext> dbFactory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();
      await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

      hasImapMappings = await db.ImapAccountMappings
        .AnyAsync(m => m.OwnerUserId == userId);
    }

    private async Task OnClickAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      running = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IImapEmailSyncService syncService =
          scope.ServiceProvider.GetRequiredService<IImapEmailSyncService>();

        await syncService.SyncSingleUserAsync(userId);
        AccessAnalyticsRecorder.RecordFeatureUsage("imap.sync_triggered", userId);
        await OnSynced.InvokeAsync();
      }
      finally
      {
        running = false;
      }
    }
  }
}
