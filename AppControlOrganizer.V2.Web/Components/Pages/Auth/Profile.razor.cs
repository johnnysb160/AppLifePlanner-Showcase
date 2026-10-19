//-----------------------------------------------------------------------
// <copyright file="Profile.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Auth
{
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;
  using System.IO;
  using System.Security.Claims;
  using AppControlOrganizer.V2.Shared.DTOs;
  using AppControlOrganizer.V2.Shared.Entities;
  using AppControlOrganizer.V2.Shared.Enums;
  using AppControlOrganizer.V2.Web.Data;
  using AppControlOrganizer.V2.Web.Data.Auth;
  using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
  using AppControlOrganizer.V2.Web.Services.Ai;
  using AppControlOrganizer.V2.Web.Services.Imap;
  using AppControlOrganizer.V2.Web.Services.Localization;
  using AppControlOrganizer.V2.Web.Services.Widgets;
  using Microsoft.AspNetCore.Components;
  using Microsoft.AspNetCore.Components.Authorization;
  using Microsoft.AspNetCore.Components.Forms;
  using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Caching.Memory;
  using Microsoft.Extensions.DependencyInjection;

  /// <summary>
  /// Code-behind for Profile.razor.
  /// Handles display name, profile photo (stored as base64 in DB), account info and password changes.
  /// </summary>
  public partial class Profile : ComponentBase
  {
    private const string AllowPublicRegistrationKey = "AllowPublicRegistration";

    /// <summary>Gets or sets the service scope factory for creating isolated DbContext scopes.</summary>
    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = null!;

    /// <summary>Gets or sets the authentication state provider.</summary>
    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    /// <summary>Gets or sets the navigation manager.</summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Gets or sets the local widget pairing service.</summary>
    [Inject]
    private IWidgetPairingService WidgetPairingService { get; set; } = null!;

    /// <summary>Gets or sets the app setting repository.</summary>
    [Inject]
    private IAppSettingRepository AppSettings { get; set; } = null!;

    // ── Loaded user state (display only) ──
    private string? userId;
    private string? currentPhotoData;
    private string? currentDisplayName;
    private bool isAdmin;

    // ── Photo & Display Name ──
    private string displayNameInput = string.Empty;
    private string photoSuccess = string.Empty;
    private string photoError = string.Empty;
    private bool photoSubmitting;

    // ── Account Info ──
    private ProfileInfoModel infoModel = new ProfileInfoModel();
    private string infoSuccess = string.Empty;
    private string infoError = string.Empty;
    private bool infoSubmitting;

    // ── Password ──
    private ChangePasswordModel pwdModel = new ChangePasswordModel();
    private string pwdSuccess = string.Empty;
    private string pwdError = string.Empty;
    private bool pwdSubmitting;

    // ── Language ──
    private string selectedLang = "en";
    private string langSuccess = string.Empty;
    private bool langSubmitting;

    // ── IMAP Settings ──
    private ImapSettingModel imapModel = new ImapSettingModel();
    private string imapSuccess = string.Empty;
    private string imapError = string.Empty;
    private bool imapSubmitting;

    // ── IMAP Mappings ──
    private List<ImapAccountMappingEntity> imapMappings = [];
    private ImapMappingModel newMappingModel = new ImapMappingModel();
    private string mappingSuccess = string.Empty;
    private string mappingError = string.Empty;
    private bool mappingSubmitting;

    // ── IMAP Interval + Manual Sync ──
    private int syncIntervalHours = 2;
    private DateTime? lastSyncedAt;
    private string intervalSuccess = string.Empty;
    private bool intervalSubmitting;
    private string syncNowSuccess = string.Empty;
    private string syncNowError = string.Empty;
    private bool syncNowRunning;

    // ── Windows Widget ──
    private string widgetPairingCode = string.Empty;
    private DateTime? widgetPairingExpiresAtUtc;
    private string widgetSuccess = string.Empty;
    private string widgetError = string.Empty;
    private bool widgetSubmitting;

    // ── Accounts (for mapping dropdown) ──
    private List<AccountEntity> userAccounts = [];

    // ── AI Provider ──
    private AiProviderSettingModel aiModel = new AiProviderSettingModel();
    private string aiSuccess = string.Empty;
    private string aiError = string.Empty;
    private bool aiSubmitting;
    private bool aiTesting;
    private string aiTestSuccess = string.Empty;
    private string aiTestError = string.Empty;
    private bool hasExistingAiApiKey;
    private DateTime? aiLastTestedAtUtc;
    private bool? aiLastTestSucceeded;
    private string? aiLastTestMessage;

    // ── Admin Security ──
    private bool allowPublicRegistration;
    private string registrationSuccess = string.Empty;
    private string registrationError = string.Empty;
    private bool registrationSubmitting;

    // Max upload size: 5 MB (keeps DB rows reasonably sized)
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
      AuthenticationState authState = await AuthStateProvider.GetAuthenticationStateAsync();
      isAdmin = authState.User.IsInRole("Admin");

      // Read the user ID from claims — no DB call, avoids concurrent DbContext issues.
      userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (string.IsNullOrEmpty(userId))
      {
        Navigation.NavigateTo("/auth/login", forceLoad: true);
        return;
      }

      // Use a dedicated scope so this DbContext does not conflict with MainLayout's
      // concurrent OnInitializedAsync call on the same SignalR circuit.
      using IServiceScope scope = ScopeFactory.CreateScope();
      UserManager<ApplicationUser> userManager =
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

      ApplicationUser? user = await userManager.FindByIdAsync(userId);
      if (user is null)
      {
        Navigation.NavigateTo("/auth/login", forceLoad: true);
        return;
      }

      currentPhotoData   = user.ProfilePhotoData;
      currentDisplayName = user.DisplayName;
      infoModel.Email    = user.Email ?? string.Empty;
      infoModel.UserName = user.UserName ?? string.Empty;
      displayNameInput   = user.DisplayName ?? string.Empty;
      selectedLang       = user.Language ?? "en";

      await LoadImapDataAsync();
      await LoadAiProviderDataAsync();
      await LoadRegistrationSettingAsync();
    }

    private async Task LoadRegistrationSettingAsync()
    {
      if (!isAdmin)
      {
        allowPublicRegistration = false;
        return;
      }

      AppSetting? setting = await AppSettings.SelectAsync(AllowPublicRegistrationKey);
      allowPublicRegistration =
        string.Equals(setting?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadImapDataAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      using IServiceScope scope = ScopeFactory.CreateScope();
      IDbContextFactory<LifePlannerContext> dbFactory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

      await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

      // Accounts use a shadow OwnerUserId; IgnoreQueryFilters + explicit filter ensures only
      // the current user's accounts are returned even when HttpContext is absent (Blazor circuit).
      userAccounts = await db.Accounts
        .IgnoreQueryFilters()
        .Where(a => !a.Deleted && EF.Property<string>(a, "OwnerUserId") == userId)
        .OrderBy(a => a.Name)
        .ToListAsync();

      ImapSettingEntity? existingSetting = await db.ImapSettings
        .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

      if (existingSetting is not null)
      {
        imapModel.ImapEmail  = existingSetting.ImapEmail;
        imapModel.ImapPassword = string.Empty; // never pre-fill password fields
        imapModel.ImapHost   = existingSetting.ImapHost;
        imapModel.ImapPort   = existingSetting.ImapPort;
        syncIntervalHours    = existingSetting.SyncIntervalHours;
        lastSyncedAt         = existingSetting.LastSyncedAt;
      }

      imapMappings = await db.ImapAccountMappings
        .Where(m => m.OwnerUserId == userId)
        .ToListAsync();
    }

    // ─────────── Photo & Name ───────────

    /// <summary>Returns the initials derived from the current display name or username.</summary>
    private string GetInitials()
    {
      string src = !string.IsNullOrWhiteSpace(currentDisplayName)
        ? currentDisplayName
        : infoModel.UserName;

      if (string.IsNullOrWhiteSpace(src))
      {
        return "?";
      }

      string[] parts = src.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2)
      {
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
      }

      return src.Length > 0 ? src[0].ToString().ToUpperInvariant() : "?";
    }

    /// <summary>Updates the display name field value as the user types.</summary>
    private void OnDisplayNameInput(ChangeEventArgs e)
    {
      displayNameInput = e.Value?.ToString() ?? string.Empty;
    }

    /// <summary>Persists the display name when the field loses focus.</summary>
    private async Task SaveDisplayNameAsync(ChangeEventArgs e)
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      photoSuccess = string.Empty;
      photoError   = string.Empty;
      photoSubmitting = true;
      StateHasChanged();

      try
      {
        string trimmedName     = displayNameInput.Trim();
        string? newDisplayName = string.IsNullOrEmpty(trimmedName) ? null : trimmedName;

        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
          photoError = "User not found.";
          return;
        }

        user.DisplayName = newDisplayName;

        IdentityResult result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
          currentDisplayName = newDisplayName;
          photoSuccess = "Display name updated.";
        }
        else
        {
          photoError = string.Join(" ", result.Errors.Select(e => e.Description));
        }
      }
      catch (Exception ex)
      {
        photoError = $"Error saving: {ex.Message}";
      }
      finally
      {
        photoSubmitting = false;
      }
    }

    /// <summary>Saves the selected photo immediately after the user picks a file.</summary>
    private async Task OnPhotoSelectedAsync(InputFileChangeEventArgs e)
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      IBrowserFile photo  = e.File;
      photoSuccess        = string.Empty;
      photoError          = string.Empty;
      photoSubmitting     = true;
      StateHasChanged();

      try
      {
        if (photo.Size > MaxPhotoBytes)
        {
          photoError = "File is too large (max 5 MB).";
          return;
        }

        string ext = Path.GetExtension(photo.Name).ToLowerInvariant();
        string mimeType = ext switch
        {
          ".jpg"  => "image/jpeg",
          ".jpeg" => "image/jpeg",
          ".png"  => "image/png",
          ".webp" => "image/webp",
          _       => string.Empty
        };

        if (string.IsNullOrEmpty(mimeType))
        {
          photoError = "Only PNG, JPG and WEBP files are allowed.";
          return;
        }

        using MemoryStream ms = new MemoryStream();
        await photo.OpenReadStream(MaxPhotoBytes).CopyToAsync(ms);
        string base64       = Convert.ToBase64String(ms.ToArray());
        string newPhotoData = $"data:{mimeType};base64,{base64}";

        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
          photoError = "User not found.";
          return;
        }

        user.ProfilePhotoData = newPhotoData;

        IdentityResult result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
          currentPhotoData = newPhotoData;
          photoSuccess     = "Photo updated.";
        }
        else
        {
          photoError = string.Join(" ", result.Errors.Select(e => e.Description));
        }
      }
      catch (Exception ex)
      {
        photoError = $"Error saving: {ex.Message}";
      }
      finally
      {
        photoSubmitting = false;
      }
    }

    /// <summary>Clears the stored profile photo data.</summary>
    private async Task RemovePhoto()
    {
      if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(currentPhotoData))
      {
        return;
      }

      photoSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
          user.ProfilePhotoData = null;
          await userManager.UpdateAsync(user);
        }

        currentPhotoData = null;
        photoSuccess = "Photo removed.";
      }
      finally
      {
        photoSubmitting = false;
      }
    }

    // ─────────── Language ───────────

    /// <summary>Handles the English radio button change and saves immediately.</summary>
    private async Task OnLangEnChangedAsync(ChangeEventArgs e)
    {
      selectedLang = "en";
      await SaveLanguageAsync();
    }

    /// <summary>Handles the Portuguese radio button change and saves immediately.</summary>
    private async Task OnLangPtChangedAsync(ChangeEventArgs e)
    {
      selectedLang = "pt";
      await SaveLanguageAsync();
    }

    /// <summary>Persists the selected UI language preference and updates the active LocService.</summary>
    private async Task SaveLanguageAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      langSuccess    = string.Empty;
      langSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
          return;
        }

        user.Language = selectedLang;
        await userManager.UpdateAsync(user);

        // The request culture provider caches the language per user; drop it so the next request uses the new one.
        UserLanguageRequestCultureProvider.Invalidate(
          scope.ServiceProvider.GetRequiredService<IMemoryCache>(),
          userId);
        Loc.Set(selectedLang);
        langSuccess = Loc["Language saved."];
      }
      finally
      {
        langSubmitting = false;
      }
    }

    // ─────────── Windows Widget ───────────

    /// <summary>Creates a one-time pairing code for the Windows widget.</summary>
    private async Task OnCreateWidgetPairingCodeAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      widgetSuccess = string.Empty;
      widgetError = string.Empty;
      widgetSubmitting = true;
      StateHasChanged();

      try
      {
        WidgetPairingCodeResponseDto response =
          await WidgetPairingService.CreatePairingCodeAsync(userId);
        widgetPairingCode = response.Code;
        widgetPairingExpiresAtUtc = response.ExpiresAtUtc;
        widgetSuccess = Loc["Widget pairing code created."];
      }
      catch (Exception ex)
      {
        widgetError = $"{Loc["Could not create widget pairing code"]}: {ex.Message}";
      }
      finally
      {
        widgetSubmitting = false;
      }
    }

    /// <summary>Revokes every local widget token for the current user.</summary>
    private async Task OnRevokeWidgetTokensAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      widgetSuccess = string.Empty;
      widgetError = string.Empty;
      widgetSubmitting = true;
      StateHasChanged();

      try
      {
        int removed = await WidgetPairingService.RevokeUserTokensAsync(userId);
        widgetPairingCode = string.Empty;
        widgetPairingExpiresAtUtc = null;
        widgetSuccess = string.Format(Loc["{0} widget token(s) revoked."], removed);
      }
      catch (Exception ex)
      {
        widgetError = $"{Loc["Could not revoke widget tokens"]}: {ex.Message}";
      }
      finally
      {
        widgetSubmitting = false;
      }
    }

    // ─────────── Admin Security ───────────

    /// <summary>Persists whether public visitors can create new profiles.</summary>
    private async Task OnSaveRegistrationSettingAsync()
    {
      if (!isAdmin)
      {
        return;
      }

      registrationSuccess = string.Empty;
      registrationError = string.Empty;
      registrationSubmitting = true;
      StateHasChanged();

      try
      {
        AppSetting setting = new AppSetting
        {
          Key = AllowPublicRegistrationKey,
          Value = allowPublicRegistration.ToString(),
        };

        await AppSettings.UpdateAsync(setting);
        registrationSuccess = Loc["Registration setting saved."];
      }
      catch (Exception ex)
      {
        registrationError = $"{Loc["Error saving registration setting"]}: {ex.Message}";
      }
      finally
      {
        registrationSubmitting = false;
      }
    }

    // ─────────── Account Info ───────────

    /// <summary>Handles the account information form submission.</summary>
    private async Task OnUpdateInfoAsync()
    {
      infoSuccess = string.Empty;
      infoError   = string.Empty;
      infoSubmitting = true;
      StateHasChanged();

      try
      {
        if (string.IsNullOrEmpty(userId))
        {
          return;
        }

        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
          infoError = "User not found.";
          return;
        }

        string trimmedEmail    = infoModel.Email.Trim();
        string trimmedUserName = infoModel.UserName.Trim();
        bool changed = false;

        if (!string.Equals(user.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase))
        {
          IdentityResult emailResult = await userManager.SetEmailAsync(user, trimmedEmail);
          if (!emailResult.Succeeded)
          {
            infoError = string.Join(" ", emailResult.Errors.Select(e => e.Description));
            return;
          }

          changed = true;
        }

        if (!string.Equals(user.UserName, trimmedUserName, StringComparison.Ordinal))
        {
          IdentityResult nameResult = await userManager.SetUserNameAsync(user, trimmedUserName);
          if (!nameResult.Succeeded)
          {
            infoError = string.Join(" ", nameResult.Errors.Select(e => e.Description));
            return;
          }

          changed = true;
        }

        infoSuccess = changed ? "Account information updated successfully." : "No changes detected.";
      }
      finally
      {
        infoSubmitting = false;
      }
    }

    // ─────────── Password ───────────

    /// <summary>Handles the change-password form submission.</summary>
    private async Task OnChangePasswordAsync()
    {
      pwdSuccess = string.Empty;
      pwdError   = string.Empty;
      pwdSubmitting = true;
      StateHasChanged();

      try
      {
        if (string.IsNullOrEmpty(userId))
        {
          return;
        }

        using IServiceScope scope = ScopeFactory.CreateScope();
        UserManager<ApplicationUser> userManager =
          scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
          pwdError = "User not found.";
          return;
        }

        IdentityResult result = await userManager.ChangePasswordAsync(user, pwdModel.CurrentPassword, pwdModel.NewPassword);
        if (result.Succeeded)
        {
          pwdSuccess = "Password changed successfully.";
          pwdModel   = new ChangePasswordModel();
        }
        else
        {
          pwdError = string.Join(" ", result.Errors.Select(e => e.Description));
        }
      }
      finally
      {
        pwdSubmitting = false;
      }
    }

    // ─────────── IMAP Settings ───────────

    /// <summary>Auto-fills the IMAP host field when the user types a known email domain.</summary>
    private void OnImapEmailInput(ChangeEventArgs e)
    {
      imapModel.ImapEmail = e.Value?.ToString() ?? string.Empty;
      imapModel.ImapHost  = SuggestImapHost(imapModel.ImapEmail);
    }

    /// <summary>Persists or updates the IMAP credentials for the current user.</summary>
    private async Task OnSaveImapSettingsAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      imapSuccess    = string.Empty;
      imapError      = string.Empty;
      imapSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        ImapSettingEntity? existing = await db.ImapSettings
          .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        if (existing is null)
        {
          ImapSettingEntity newSetting = new ImapSettingEntity
          {
            OwnerUserId   = userId,
            ImapEmail     = imapModel.ImapEmail.Trim(),
            ImapPassword  = imapModel.ImapPassword,
            ImapHost      = imapModel.ImapHost.Trim(),
            ImapPort      = imapModel.ImapPort
          };

          db.ImapSettings.Add(newSetting);
        }
        else
        {
          existing.ImapEmail = imapModel.ImapEmail.Trim();
          existing.ImapHost  = imapModel.ImapHost.Trim();
          existing.ImapPort  = imapModel.ImapPort;

          // Only overwrite the password when the user explicitly types a new one
          if (!string.IsNullOrEmpty(imapModel.ImapPassword))
          {
            existing.ImapPassword = imapModel.ImapPassword;
          }
        }

        await db.SaveChangesAsync();
        imapModel.ImapPassword = string.Empty;
        imapSuccess = Loc["IMAP settings saved successfully."];
      }
      catch (Exception ex)
      {
        imapError = $"{Loc["Error saving IMAP settings"]}: {ex.Message}";
      }
      finally
      {
        imapSubmitting = false;
      }
    }

    // ─────────── IMAP Interval + Manual Sync ───────────

    /// <summary>Persists the auto-sync interval for the current user's IMAP setting.</summary>
    private async Task OnSaveIntervalAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      intervalSuccess   = string.Empty;
      intervalSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        ImapSettingEntity? setting = await db.ImapSettings
          .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        if (setting is null)
        {
          intervalSuccess = Loc["Save your IMAP credentials first."];
          return;
        }

        setting.SyncIntervalHours = syncIntervalHours;
        await db.SaveChangesAsync();
        intervalSuccess = Loc["Sync interval saved."];
      }
      catch (Exception ex)
      {
        intervalSuccess = $"{Loc["Error saving interval"]}: {ex.Message}";
      }
      finally
      {
        intervalSubmitting = false;
      }
    }

    /// <summary>Triggers an immediate IMAP sync for the current user, bypassing the configured interval.</summary>
    private async Task OnSyncNowAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      syncNowSuccess  = string.Empty;
      syncNowError    = string.Empty;
      syncNowRunning  = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IImapEmailSyncService syncService =
          scope.ServiceProvider.GetRequiredService<IImapEmailSyncService>();

        await syncService.SyncSingleUserAsync(userId);

        lastSyncedAt   = DateTime.UtcNow;
        syncNowSuccess = Loc["Sync completed successfully."];
      }
      catch (Exception ex)
      {
        syncNowError = $"{Loc["Sync failed"]}: {ex.Message}";
      }
      finally
      {
        syncNowRunning = false;
      }
    }

    // ─────────── IMAP Mappings ───────────

    /// <summary>Adds a new sender-to-account mapping for the current user.</summary>
    private async Task OnAddMappingAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      mappingSuccess    = string.Empty;
      mappingError      = string.Empty;
      mappingSubmitting = true;
      StateHasChanged();

      try
      {
        if (newMappingModel.AccountId <= 0)
        {
          mappingError = Loc["Please select a destination account."];
          return;
        }

        string trimmedSender = newMappingModel.SenderIdentifier.Trim();
        bool alreadyExists = imapMappings.Exists(m =>
          string.Equals(m.SenderIdentifier, trimmedSender, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
          mappingError = Loc["A mapping for this sender already exists."];
          return;
        }

        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        ImapAccountMappingEntity mapping = new ImapAccountMappingEntity
        {
          OwnerUserId      = userId,
          SenderIdentifier = trimmedSender,
          AccountId        = newMappingModel.AccountId
        };

        db.ImapAccountMappings.Add(mapping);
        await db.SaveChangesAsync();

        imapMappings.Add(mapping);
        newMappingModel   = new ImapMappingModel();
        mappingSuccess    = Loc["Mapping added."];
      }
      catch (Exception ex)
      {
        mappingError = $"{Loc["Error adding mapping"]}: {ex.Message}";
      }
      finally
      {
        mappingSubmitting = false;
      }
    }

    /// <summary>Soft-deletes the specified sender-to-account mapping.</summary>
    private async Task OnRemoveMappingAsync(int mappingId)
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      mappingSuccess    = string.Empty;
      mappingError      = string.Empty;
      mappingSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        ImapAccountMappingEntity? mapping = await db.ImapAccountMappings
          .FirstOrDefaultAsync(m => m.Id == mappingId && m.OwnerUserId == userId);

        if (mapping is not null)
        {
          mapping.Deleted = true;
          await db.SaveChangesAsync();
        }

        imapMappings.RemoveAll(m => m.Id == mappingId);
        mappingSuccess = Loc["Mapping removed."];
      }
      catch (Exception ex)
      {
        mappingError = $"{Loc["Error removing mapping"]}: {ex.Message}";
      }
      finally
      {
        mappingSubmitting = false;
      }
    }

    /// <summary>Returns a suggested IMAP hostname based on the email domain.</summary>
    private static string SuggestImapHost(string email)
    {
      if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
      {
        return string.Empty;
      }

      string domain = email[(email.IndexOf('@') + 1)..].ToLowerInvariant();

      return domain switch
      {
        "gmail.com"     => "imap.gmail.com",
        "googlemail.com" => "imap.gmail.com",
        "outlook.com"   => "outlook.office365.com",
        "hotmail.com"   => "outlook.office365.com",
        "live.com"      => "outlook.office365.com",
        "yahoo.com"     => "imap.mail.yahoo.com",
        "yahoo.com.br"  => "imap.mail.yahoo.com",
        "icloud.com"    => "imap.mail.me.com",
        _               => string.Empty
      };
    }

    // ─────────── AI Provider ───────────

    /// <summary>Loads the AI provider configuration for the current user, or initialises defaults if none exists.</summary>
    private async Task LoadAiProviderDataAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      using IServiceScope scope = ScopeFactory.CreateScope();
      IDbContextFactory<LifePlannerContext> dbFactory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

      await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

      AiProviderSettingEntity? existing = await db.AiProviderSettings
        .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

      if (existing is not null)
      {
        aiModel.IsEnabled                 = existing.IsEnabled;
        aiModel.ProviderKind              = existing.ProviderKind;
        aiModel.DisplayName               = existing.DisplayName;
        aiModel.BaseUrl                   = existing.BaseUrl ?? string.Empty;
        aiModel.Model                     = existing.Model;
        aiModel.NewApiKey                 = string.Empty; // never pre-fill
        aiModel.Temperature               = existing.Temperature;
        aiModel.MaxTokens                 = existing.MaxTokens;
        aiModel.PrivacyMode               = existing.PrivacyMode;
        aiModel.AllowTransactionsAnalysis = existing.AllowTransactionsAnalysis;
        aiModel.AllowAllocationsAnalysis  = existing.AllowAllocationsAnalysis;
        aiModel.AllowMarketQuotesAnalysis = existing.AllowMarketQuotesAnalysis;

        hasExistingAiApiKey = !string.IsNullOrEmpty(existing.ApiKey);
        aiLastTestedAtUtc   = existing.LastTestedAtUtc;
        aiLastTestSucceeded = existing.LastTestSucceeded;
        aiLastTestMessage   = existing.LastTestMessage;
      }
    }

    /// <summary>Persists or updates the AI provider configuration for the current user.</summary>
    private async Task OnSaveAiProviderAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      aiSuccess    = string.Empty;
      aiError      = string.Empty;
      aiSubmitting = true;
      StateHasChanged();

      try
      {
        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        AiProviderSettingEntity? existing = await db.AiProviderSettings
          .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        if (existing is null)
        {
          AiProviderSettingEntity newSetting = new AiProviderSettingEntity
          {
            OwnerUserId               = userId,
            IsEnabled                 = aiModel.IsEnabled,
            ProviderKind              = aiModel.ProviderKind,
            DisplayName               = aiModel.DisplayName.Trim(),
            BaseUrl                   = string.IsNullOrWhiteSpace(aiModel.BaseUrl) ? null : aiModel.BaseUrl.Trim(),
            Model                     = aiModel.Model.Trim(),
            ApiKey                    = string.IsNullOrEmpty(aiModel.NewApiKey) ? null : aiModel.NewApiKey,
            Temperature               = aiModel.Temperature,
            MaxTokens                 = aiModel.MaxTokens,
            PrivacyMode               = aiModel.PrivacyMode,
            AllowTransactionsAnalysis = aiModel.AllowTransactionsAnalysis,
            AllowAllocationsAnalysis  = aiModel.AllowAllocationsAnalysis,
            AllowMarketQuotesAnalysis = aiModel.AllowMarketQuotesAnalysis,
            UpdatedAt                 = DateTime.UtcNow,
          };

          db.AiProviderSettings.Add(newSetting);
        }
        else
        {
          existing.IsEnabled                 = aiModel.IsEnabled;
          existing.ProviderKind              = aiModel.ProviderKind;
          existing.DisplayName               = aiModel.DisplayName.Trim();
          existing.BaseUrl                   = string.IsNullOrWhiteSpace(aiModel.BaseUrl) ? null : aiModel.BaseUrl.Trim();
          existing.Model                     = aiModel.Model.Trim();
          existing.Temperature               = aiModel.Temperature;
          existing.MaxTokens                 = aiModel.MaxTokens;
          existing.PrivacyMode               = aiModel.PrivacyMode;
          existing.AllowTransactionsAnalysis = aiModel.AllowTransactionsAnalysis;
          existing.AllowAllocationsAnalysis  = aiModel.AllowAllocationsAnalysis;
          existing.AllowMarketQuotesAnalysis = aiModel.AllowMarketQuotesAnalysis;
          existing.UpdatedAt                 = DateTime.UtcNow;

          // Only replace the API key when the user explicitly types a new one
          if (!string.IsNullOrEmpty(aiModel.NewApiKey))
          {
            existing.ApiKey = aiModel.NewApiKey;
          }
        }

        await db.SaveChangesAsync();

        aiModel.NewApiKey   = string.Empty;
        hasExistingAiApiKey = true;
        aiSuccess           = Loc["AI provider configuration saved successfully."];
      }
      catch (Exception ex)
      {
        aiError = $"{Loc["Error saving:"]} {ex.Message}";
      }
      finally
      {
        aiSubmitting = false;
      }
    }

    /// <summary>Tests connectivity to the configured AI provider using a minimal request.</summary>
    private async Task OnTestAiConnectionAsync()
    {
      if (string.IsNullOrEmpty(userId))
      {
        return;
      }

      aiTestSuccess = string.Empty;
      aiTestError   = string.Empty;
      aiTesting     = true;
      StateHasChanged();

      try
      {
        // Validate required fields before attempting the network call
        if (aiModel.ProviderKind == AiProviderKind.OpenAICompatible && string.IsNullOrWhiteSpace(aiModel.BaseUrl))
        {
          aiTestError = Loc["Enter the base URL before testing."];
          return;
        }

        if (string.IsNullOrWhiteSpace(aiModel.Model))
        {
          aiTestError = Loc["Enter the model before testing."];
          return;
        }

        if (string.IsNullOrEmpty(aiModel.NewApiKey) && !hasExistingAiApiKey)
        {
          aiTestError = Loc["Save the configuration with an API key before testing."];
          return;
        }

        // Build a temporary entity so the test service can access BaseUrl/Model/ProviderKind.
        // The real (decrypted) ApiKey comes from the DB if the user hasn't typed a new one.
        using IServiceScope scope = ScopeFactory.CreateScope();
        IDbContextFactory<LifePlannerContext> dbFactory =
          scope.ServiceProvider.GetRequiredService<IDbContextFactory<LifePlannerContext>>();

        await using LifePlannerContext db = await dbFactory.CreateDbContextAsync();

        AiProviderSettingEntity? persisted = await db.AiProviderSettings
          .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        AiProviderSettingEntity testEntity = persisted ?? new AiProviderSettingEntity
        {
          OwnerUserId = userId,
        };

        // Apply form values so the test uses whatever the user has currently entered
        testEntity.ProviderKind = aiModel.ProviderKind;
        testEntity.BaseUrl      = string.IsNullOrWhiteSpace(aiModel.BaseUrl) ? null : aiModel.BaseUrl.Trim();
        testEntity.Model        = aiModel.Model.Trim();

        IAiProviderTestService testService =
          scope.ServiceProvider.GetRequiredService<IAiProviderTestService>();

        AiProviderTestResult result = await testService.TestAsync(testEntity, aiModel.NewApiKey);

        // Persist the test outcome so it is visible on next load
        if (persisted is not null)
        {
          persisted.LastTestedAtUtc  = result.TestedAtUtc;
          persisted.LastTestSucceeded = result.Succeeded;
          persisted.LastTestMessage  = result.Message;
          await db.SaveChangesAsync();
        }

        aiLastTestedAtUtc   = result.TestedAtUtc;
        aiLastTestSucceeded = result.Succeeded;
        aiLastTestMessage   = result.Message;

        if (result.Succeeded)
        {
          aiTestSuccess = Loc[result.Message];
        }
        else
        {
          aiTestError = Loc[result.Message];
        }
      }
      catch (Exception ex)
      {
        aiTestError = $"{Loc["Unexpected error:"]} {ex.Message}";
      }
      finally
      {
        aiTesting = false;
      }
    }

    // ─────────── Inner Models ───────────

    private sealed class ProfileInfoModel
    {
      [Required(ErrorMessage = "Email is required.")]
      [EmailAddress(ErrorMessage = "Invalid email address.")]
      public string Email { get; set; } = string.Empty;

      [Required(ErrorMessage = "Username is required.")]
      [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be 3–100 characters.")]
      public string UserName { get; set; } = string.Empty;
    }

    private sealed class ChangePasswordModel
    {
      [Required(ErrorMessage = "Current password is required.")]
      public string CurrentPassword { get; set; } = string.Empty;

      [Required(ErrorMessage = "New password is required.")]
      [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
      public string NewPassword { get; set; } = string.Empty;

      [Required(ErrorMessage = "Please confirm the new password.")]
      [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
      public string ConfirmPassword { get; set; } = string.Empty;
    }

    private sealed class ImapSettingModel
    {
      [Required(ErrorMessage = "Email address is required.")]
      [EmailAddress(ErrorMessage = "Invalid email address.")]
      public string ImapEmail { get; set; } = string.Empty;

      // Not required on edit — an empty value means "keep existing password"
      public string ImapPassword { get; set; } = string.Empty;

      [Required(ErrorMessage = "IMAP server is required.")]
      public string ImapHost { get; set; } = string.Empty;

      [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
      public int ImapPort { get; set; } = 993;
    }

    private sealed class ImapMappingModel
    {
      [Required(ErrorMessage = "Sender identifier is required.")]
      [StringLength(200, MinimumLength = 2, ErrorMessage = "Sender must be 2–200 characters.")]
      public string SenderIdentifier { get; set; } = string.Empty;

      [Range(1, int.MaxValue, ErrorMessage = "Please select a destination account.")]
      public int AccountId { get; set; }
    }

    private sealed class AiProviderSettingModel
    {
      public bool IsEnabled { get; set; } = false;

      public AiProviderKind ProviderKind { get; set; } = AiProviderKind.OpenAICompatible;

      [Required(ErrorMessage = "Display name is required.")]
      [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be 1-200 characters.")]
      public string DisplayName { get; set; } = "Default AI Provider";

      public string BaseUrl { get; set; } = string.Empty;

      [Required(ErrorMessage = "Model name is required.")]
      [StringLength(200, MinimumLength = 1, ErrorMessage = "Model must be 1-200 characters.")]
      public string Model { get; set; } = string.Empty;

      // Not required on edit — empty means "keep existing key"
      public string NewApiKey { get; set; } = string.Empty;

      [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0.")]
      public decimal? Temperature { get; set; }

      [Range(1, 32768, ErrorMessage = "MaxTokens must be between 1 and 32768.")]
      public int? MaxTokens { get; set; }

      public string PrivacyMode { get; set; } = "AggregatedDataOnly";

      public bool AllowTransactionsAnalysis { get; set; } = false;

      public bool AllowAllocationsAnalysis { get; set; } = false;

      public bool AllowMarketQuotesAnalysis { get; set; } = false;
    }
  }
}
