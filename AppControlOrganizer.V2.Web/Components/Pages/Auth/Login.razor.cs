//-----------------------------------------------------------------------
// <copyright file="Login.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace AppControlOrganizer.V2.Web.Components.Pages.Auth;

/// <summary>
/// Unified auth page: login, register, forgot-password and reset-password flows.
/// Active mode is determined by the current URL path.
/// </summary>
public partial class Login : ComponentBase, IDisposable
{
  private const string AllowPublicRegistrationKey = "AllowPublicRegistration";

  [Inject] public UserManager<AppControlOrganizer.V2.Web.Data.Auth.ApplicationUser> UserManager { get; set; } = null!;
  [Inject] public IAppSettingRepository AppSettings { get; set; } = null!;
  [Inject] public NavigationManager Navigation { get; set; } = null!;
  [Inject] public ILogger<Login> Logger { get; set; } = null!;
  [Inject] public IJSRuntime JS { get; set; } = null!;

  // ── State ──
  private LoginMode currentMode;
  private string pageTitle = string.Empty;
  private string pageSubtitle = string.Empty;
  private string headerIcon = string.Empty;
  private string errorMessage = string.Empty;
  private string successMessage = string.Empty;
  private bool isSubmitting;
  private bool showSetupLink;
  private bool resetCompleted;
  private bool publicRegistrationAllowed;

  // ── Models ──
  private LoginModel loginModel = new();
  private ForgotPasswordModel forgotModel = new();
  private ResetPasswordModel resetModel = new();
  private RegisterModel registerModel = new();

  // ── Query params for reset ──
  private string? resetEmail;
  private string? resetToken;

  protected override async Task OnInitializedAsync()
  {
    Navigation.LocationChanged += OnLocationChanged;
    DetectMode();
    await LoadPublicRegistrationSettingAsync();

    Uri uri = new(Navigation.Uri);
    ApplyQueryMessages(uri);

    if (currentMode == LoginMode.Login)
    {
      showSetupLink = !await UserManager.Users.AnyAsync();
    }
  }

  private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
  {
    _ = InvokeAsync(async () =>
    {
      errorMessage = string.Empty;
      successMessage = string.Empty;
      loginModel = new();
      forgotModel = new();
      resetModel = new();
      registerModel = new();
      DetectMode();
      await LoadPublicRegistrationSettingAsync();
      ApplyQueryMessages(new Uri(Navigation.Uri));
      StateHasChanged();
    });
  }

  public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

  private void DetectMode()
  {
    Uri uri = new(Navigation.Uri);
    string path = uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();

    if (path.EndsWith("/forgot-password"))
    {
      currentMode = LoginMode.ForgotPassword;
      pageTitle = "Forgot Password";
      pageSubtitle = "Enter your email to receive a reset link.";
      headerIcon = "bi-envelope-paper";
    }
    else if (path.EndsWith("/reset-password"))
    {
      currentMode = LoginMode.ResetPassword;
      pageTitle = "Reset Password";
      pageSubtitle = "Choose a new password for your account.";
      headerIcon = "bi-key";
      ParseResetQueryParams(uri);
    }
    else if (path.EndsWith("/register"))
    {
      currentMode = LoginMode.Register;
      pageTitle = "Create Account";
      pageSubtitle = "Sign up to start using Life Planner.";
      headerIcon = "bi-person-plus";
    }
    else
    {
      currentMode = LoginMode.Login;
      pageTitle = "Sign In";
      pageSubtitle = "Enter your credentials to continue.";
      headerIcon = "bi-box-arrow-in-right";
    }
  }

  private void ParseResetQueryParams(Uri uri)
  {
    Dictionary<string, string> query = QueryHelpers.ParseQuery(uri.Query)
      .ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    query.TryGetValue("email", out resetEmail);
    query.TryGetValue("token", out string? encodedToken);

    if (!string.IsNullOrWhiteSpace(encodedToken))
    {
      try
      {
        resetToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
      }
      catch
      {
        errorMessage = Loc["Invalid reset link. Please request a new one."];
      }
    }

    if (string.IsNullOrWhiteSpace(resetEmail) || string.IsNullOrWhiteSpace(resetToken))
    {
      errorMessage = Loc["Invalid reset link. Please request a new one."];
    }
  }

  // ═══════════ LOGIN ═══════════
  private async Task OnLoginAsync()
  {
    errorMessage = string.Empty;
    isSubmitting = true;
    StateHasChanged();

    Uri uri = new(Navigation.Uri);
    Dictionary<string, string> query = QueryHelpers.ParseQuery(uri.Query)
      .ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    string returnUrl = "/";
    if (query.TryGetValue("returnUrl", out string? r) && !string.IsNullOrWhiteSpace(r))
    {
      returnUrl = r;
    }

    await SubmitFormAsync(
      "/auth/login-local",
      new Dictionary<string, string>
      {
        ["Email"] = loginModel.Email ?? string.Empty,
        ["Password"] = loginModel.Password ?? string.Empty,
        ["RememberMe"] = loginModel.RememberMe ? "true" : "false",
        ["returnUrl"] = returnUrl,
      });
  }

  // ═══════════ REGISTER ═══════════
  private async Task OnRegisterAsync()
  {
    errorMessage = string.Empty;
    successMessage = string.Empty;
    isSubmitting = true;
    StateHasChanged();

    await SubmitFormAsync(
      "/auth/register-local",
      new Dictionary<string, string>
      {
        ["Username"] = registerModel.Username,
        ["Email"] = registerModel.Email,
        ["Password"] = registerModel.Password,
        ["ConfirmPassword"] = registerModel.ConfirmPassword,
      });
  }

  private async Task LoadPublicRegistrationSettingAsync()
  {
    AppControlOrganizer.V2.Shared.Entities.AppSetting? setting =
      await AppSettings.SelectAsync(AllowPublicRegistrationKey);
    publicRegistrationAllowed = string.Equals(setting?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
  }

  // ═══════════ FORGOT PASSWORD ═══════════
  private async Task OnForgotPasswordAsync()
  {
    errorMessage = string.Empty;
    successMessage = string.Empty;
    isSubmitting = true;
    StateHasChanged();

    await SubmitFormAsync(
      "/auth/forgot-password-local",
      new Dictionary<string, string>
      {
        ["Email"] = forgotModel.Email,
      });
  }

  // ═══════════ RESET PASSWORD ═══════════
  private async Task OnResetPasswordAsync()
  {
    errorMessage = string.Empty;
    isSubmitting = true;
    StateHasChanged();

    if (string.IsNullOrWhiteSpace(resetEmail) || string.IsNullOrWhiteSpace(resetToken))
    {
      errorMessage = Loc["Invalid reset link. Please request a new one."];
      isSubmitting = false;
      return;
    }

    await SubmitFormAsync(
      "/auth/reset-password-local",
      new Dictionary<string, string>
      {
        ["Email"] = resetEmail,
        ["Token"] = resetToken,
        ["NewPassword"] = resetModel.NewPassword,
        ["ConfirmPassword"] = resetModel.ConfirmPassword,
      });
  }

  private void ApplyQueryMessages(Uri uri)
  {
    Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(uri.Query);
    if (query.TryGetValue("failed", out StringValues failedVal) && failedVal == "1")
    {
      errorMessage = Loc["User or password incorrect. Please try again."];
    }

    if (query.TryGetValue("locked", out StringValues lockedVal) && lockedVal == "1")
    {
      errorMessage = Loc["This account is temporarily locked. Please try again later."];
    }

    if (query.TryGetValue("sent", out StringValues sentVal) && sentVal == "1")
    {
      successMessage = Loc["If an account with that email exists, a reset link has been sent. Check your inbox."];
    }

    if (query.TryGetValue("registration", out StringValues registrationVal))
    {
      successMessage = registrationVal == "created"
        ? Loc["Account created! You can now sign in."]
        : string.Empty;
      errorMessage = registrationVal.ToString() switch
      {
        "disabled" => Loc["Public account creation is currently disabled."],
        "invalid" => Loc["Unable to create the account. Please review the information and try again."],
        "unavailable" => Loc["Unable to create the account with the supplied information."],
        _ => errorMessage,
      };
    }

    if (query.TryGetValue("reset", out StringValues resetVal))
    {
      resetCompleted = resetVal == "success";
      if (resetVal == "failed")
      {
        errorMessage = Loc["Unable to reset password. Please request a new reset link."];
      }
    }
  }

  private async Task SubmitFormAsync(string action, IReadOnlyDictionary<string, string> values)
  {
    try
    {
      // The browser fetches an antiforgery token and posts the form itself; no script text is built from user input.
      await JS.InvokeVoidAsync("lpPostAuthForm", action, values);
    }
    catch (JSException)
    {
      errorMessage = Loc["Could not submit the form. Reload the page and try again."];
      isSubmitting = false;
    }
  }

  // ── Enums & Models ──
  private enum LoginMode { Login, Register, ForgotPassword, ResetPassword }

  private sealed class LoginModel
  {
    [Required(ErrorMessage = "Email or username is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
  }

  private sealed class RegisterModel
  {
    [Required(ErrorMessage = "Username is required.")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
  }

  private sealed class ForgotPasswordModel
  {
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;
  }

  private sealed class ResetPasswordModel
  {
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
  }
}
