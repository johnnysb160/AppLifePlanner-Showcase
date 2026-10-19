//-----------------------------------------------------------------------
// <copyright file="IAiProviderTestService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Ai;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Verifies that a saved AI provider configuration can reach its endpoint and authenticate successfully.
/// </summary>
public interface IAiProviderTestService
{
  /// <summary>
  /// Sends a minimal test request to the configured provider and returns the result.
  /// </summary>
  /// <param name="setting">The persisted provider configuration (ApiKey already decrypted by EF).</param>
  /// <param name="newApiKey">
  /// An unsaved API key typed by the user for this session.
  /// When non-empty, takes precedence over the stored key so the user can test before saving.
  /// </param>
  /// <returns>An <see cref="AiProviderTestResult"/> describing success or failure. Never throws.</returns>
  Task<AiProviderTestResult> TestAsync(AiProviderSettingEntity setting, string? newApiKey);
}
