//-----------------------------------------------------------------------
// <copyright file="AppToast.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using Microsoft.AspNetCore.Components;

namespace AppControlOrganizer.V2.Web.Components.Shared.Common
{
  public partial class AppToast : ComponentBase, IDisposable
  {
    private CancellationTokenSource? cts;

    /// <summary>Toast message to display.</summary>
    [Parameter] public string Message { get; set; } = string.Empty;

    /// <summary>Whether toast is an error style.</summary>
    [Parameter] public bool IsError { get; set; }

    /// <summary>Auto-hide delay in milliseconds.</summary>
    [Parameter] public int AutoHideMs { get; set; } = 3000;

    /// <summary>Raised when toast should be dismissed (auto or manual).</summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    protected override void OnParametersSet()
    {
      // Restart auto-hide timer whenever message changes.
      cts?.Cancel();
      cts?.Dispose();
      cts = null;

      if (!string.IsNullOrWhiteSpace(Message) && AutoHideMs > 0)
      {
        cts = new CancellationTokenSource();
        _ = AutoDismissAsync(cts.Token);
      }
    }

    // Ignore cancellation without throwing exceptions (debugger-friendly).
    private async Task AutoDismissAsync(CancellationToken token)
    {
      Task delayTask = Task.Delay(AutoHideMs);
      Task cancelTask = Task.Delay(Timeout.Infinite, token);

      Task finished = await Task.WhenAny(delayTask, cancelTask);

      if (finished == cancelTask)
      {
        return;
      }

      await DismissAsync();
    }

    private async Task DismissAsync()
    {
      if (OnDismiss.HasDelegate)
      {
        await OnDismiss.InvokeAsync();
      }
    }

    public void Dispose()
    {
      cts?.Cancel();
      cts?.Dispose();
    }
  }
}
