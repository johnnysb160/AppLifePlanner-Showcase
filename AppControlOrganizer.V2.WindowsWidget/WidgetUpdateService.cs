//-----------------------------------------------------------------------
// <copyright file="WidgetUpdateService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Velopack;
using Velopack.Sources;

/// <summary>
/// Handles low-frequency Velopack update checks from GitHub Releases.
/// </summary>
public sealed class WidgetUpdateService
{
  private static readonly TimeSpan MinimumUpdateInterval = TimeSpan.FromMinutes(15);
  private static readonly TimeSpan UpdateOperationTimeout = TimeSpan.FromSeconds(45);
  private const string LatestReleaseApiUrl = "https://api.github.com/repos/johnnysb160/AppLifePlanner-Showcase/releases/latest";
  private const string RepositoryUrl = "https://github.com/johnnysb160/AppLifePlanner-Showcase";

  private readonly WidgetSettingsStore settingsStore;

  /// <summary>
  /// Initializes a new instance of the <see cref="WidgetUpdateService"/> class.
  /// </summary>
  /// <param name="settingsStore">Settings store.</param>
  public WidgetUpdateService(WidgetSettingsStore settingsStore)
  {
    this.settingsStore = settingsStore;
  }

  /// <summary>Checks for updates when enough time has elapsed.</summary>
  public async Task<WidgetUpdateCheckResult> CheckForUpdatesAsync(
    WidgetSettings settings,
    bool forceTokenValidation)
  {
    string accessToken = WidgetSettingsStore.Unprotect(settings.ProtectedGitHubToken);
    if (string.IsNullOrWhiteSpace(accessToken))
    {
      return WidgetUpdateCheckResult.MissingGitHubToken;
    }

    bool shouldCheckUpdate = forceTokenValidation
      || !settings.LastUpdateCheckUtc.HasValue
      || DateTime.UtcNow - settings.LastUpdateCheckUtc.Value >= MinimumUpdateInterval;
    if (!shouldCheckUpdate)
    {
      return WidgetUpdateCheckResult.Skipped;
    }

    WidgetUpdateCheckResult tokenValidation = await ValidateGitHubTokenAsync(accessToken);
    if (tokenValidation != WidgetUpdateCheckResult.TokenValid)
    {
      return tokenValidation;
    }

    if (!shouldCheckUpdate)
    {
      return WidgetUpdateCheckResult.Skipped;
    }

    if (Debugger.IsAttached)
    {
      settings.LastUpdateCheckUtc = DateTime.UtcNow;
      this.settingsStore.Save(settings);
      return WidgetUpdateCheckResult.Skipped;
    }

    GithubSource source = new GithubSource(RepositoryUrl, accessToken: accessToken, prerelease: false);
    UpdateManager manager = new UpdateManager(source);
    UpdateInfo? update = await manager.CheckForUpdatesAsync().WaitAsync(UpdateOperationTimeout);
    settings.LastUpdateCheckUtc = DateTime.UtcNow;
    this.settingsStore.Save(settings);
    if (update == null)
    {
      return WidgetUpdateCheckResult.NoUpdate;
    }

    await manager.DownloadUpdatesAsync(update).WaitAsync(UpdateOperationTimeout);
    manager.ApplyUpdatesAndRestart(update);
    return WidgetUpdateCheckResult.UpdateApplied;
  }

  private static async Task<WidgetUpdateCheckResult> ValidateGitHubTokenAsync(string accessToken)
  {
    using HttpClient httpClient = new HttpClient
    {
      Timeout = TimeSpan.FromSeconds(20),
    };
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AppLifePlanner-WindowsWidget");
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    using HttpResponseMessage response = await httpClient.GetAsync(LatestReleaseApiUrl);
    if (response.IsSuccessStatusCode)
    {
      return WidgetUpdateCheckResult.TokenValid;
    }

    if (response.StatusCode == HttpStatusCode.Unauthorized
      || response.StatusCode == HttpStatusCode.Forbidden
      || response.StatusCode == HttpStatusCode.NotFound)
    {
      return WidgetUpdateCheckResult.InvalidGitHubToken;
    }

    response.EnsureSuccessStatusCode();
    return WidgetUpdateCheckResult.TokenValid;
  }
}

/// <summary>
/// Result of the widget update check.
/// </summary>
public enum WidgetUpdateCheckResult
{
  /// <summary>The GitHub token was accepted.</summary>
  TokenValid,

  /// <summary>The update check was skipped by the low-frequency gate.</summary>
  Skipped,

  /// <summary>No GitHub token is stored locally.</summary>
  MissingGitHubToken,

  /// <summary>The stored GitHub token is missing repository access or was revoked.</summary>
  InvalidGitHubToken,

  /// <summary>No release newer than the installed version is available.</summary>
  NoUpdate,

  /// <summary>An update was downloaded and applied.</summary>
  UpdateApplied,
}
