//-----------------------------------------------------------------------
// <copyright file="Setup.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AppControlOrganizer.V2.Web.Components.Pages.Auth;

/// <summary>
/// First-run setup page. Creates the initial admin user, backfills orphan records, and signs in automatically.
/// Redirects to login if any user already exists.
/// </summary>
public partial class Setup : ComponentBase
{
  [Inject]
  public UserManager<ApplicationUser> UserManager { get; set; } = null!;

  [Inject]
  public RoleManager<IdentityRole> RoleManager { get; set; } = null!;

  [Inject]
  public SignInManager<ApplicationUser> SignInManager { get; set; } = null!;

  [Inject]
  public LifePlannerContext DbContext { get; set; } = null!;

  [Inject]
  public NavigationManager Navigation { get; set; } = null!;

  private SetupModel model = new();
  private string errorMessage = string.Empty;
  private bool isSubmitting;

  protected override async Task OnInitializedAsync()
  {
        bool anyUser = await UserManager.Users.AnyAsync();
    if (anyUser)
    {
      Navigation.NavigateTo("/", forceLoad: true);
    }
  }

  private async Task OnSubmitAsync()
  {
    errorMessage = string.Empty;

    if (model.Password != model.ConfirmPassword)
    {
      errorMessage = "Passwords do not match.";
      return;
    }

    // Re-check on the server before creating anything. OnInitializedAsync only redirects the UI, and a
    // crafted circuit can dispatch this handler without ever honouring that navigation; without this
    // guard the page would mint an additional Admin account after first-run setup is complete.
    bool anyUser = await UserManager.Users.AnyAsync();
    if (anyUser)
    {
      Navigation.NavigateTo("/", forceLoad: true);
      return;
    }

    isSubmitting = true;
    StateHasChanged();

    try
    {
      // Ensure Admin role exists
      bool roleExists = await RoleManager.RoleExistsAsync("Admin");
      if (!roleExists)
      {
        IdentityResult roleResult = await RoleManager.CreateAsync(new IdentityRole("Admin"));
        if (!roleResult.Succeeded)
        {
          errorMessage = string.Join(" ", roleResult.Errors.Select(e => e.Description));
          return;
        }
      }

      // Create user
      ApplicationUser user = new()
      {
        UserName = model.Email.Trim(),
        Email = model.Email.Trim()
      };

    // Ensure email is marked confirmed for the initial admin so sign-in is allowed
    user.EmailConfirmed = true;

      IdentityResult createResult = await UserManager.CreateAsync(user, model.Password);
      if (!createResult.Succeeded)
      {
        errorMessage = string.Join(" ", createResult.Errors.Select(e => e.Description));
        return;
      }

      // Assign Admin role
      IdentityResult addRoleResult = await UserManager.AddToRoleAsync(user, "Admin");
      if (!addRoleResult.Succeeded)
      {
        errorMessage = string.Join(" ", addRoleResult.Errors.Select(e => e.Description));
        return;
      }

      // Backfill orphan records (OwnerUserId IS NULL) to the new admin
      string adminId = user.Id;
      await DbContext.Database.ExecuteSqlRawAsync(
        "UPDATE \"Accounts\" SET \"OwnerUserId\" = {0} WHERE \"OwnerUserId\" IS NULL", adminId);
      await DbContext.Database.ExecuteSqlRawAsync(
        "UPDATE \"Allocations\" SET \"OwnerUserId\" = {0} WHERE \"OwnerUserId\" IS NULL", adminId);
      await DbContext.Database.ExecuteSqlRawAsync(
        "UPDATE \"Appointments\" SET \"OwnerUserId\" = {0} WHERE \"OwnerUserId\" IS NULL", adminId);
      await DbContext.Database.ExecuteSqlRawAsync(
        "UPDATE \"CreditCards\" SET \"OwnerUserId\" = {0} WHERE \"OwnerUserId\" IS NULL", adminId);

      // Auto sign-in
      await SignInManager.SignInAsync(user, isPersistent: true);

      Navigation.NavigateTo("/", forceLoad: true);
    }
    finally
    {
      isSubmitting = false;
    }
  }

  private sealed class SetupModel
  {
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
}
