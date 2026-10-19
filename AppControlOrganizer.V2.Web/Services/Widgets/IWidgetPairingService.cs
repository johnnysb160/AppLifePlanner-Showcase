//-----------------------------------------------------------------------
// <copyright file="IWidgetPairingService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Widgets;

using AppControlOrganizer.V2.Shared.DTOs;

/// <summary>
/// Manages one-time local widget pairing codes and long-lived device tokens.
/// </summary>
public interface IWidgetPairingService
{
  /// <summary>Creates a short one-time code for the authenticated user.</summary>
  Task<WidgetPairingCodeResponseDto> CreatePairingCodeAsync(
    string userId,
    CancellationToken cancellationToken = default);

  /// <summary>Exchanges a one-time code for a device token.</summary>
  Task<string?> ExchangePairingCodeAsync(
    string code,
    string deviceName,
    CancellationToken cancellationToken = default);

  /// <summary>Returns the owner user identifier for a valid device token.</summary>
  Task<string?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

  /// <summary>Revokes all widget tokens for the supplied user.</summary>
  Task<int> RevokeUserTokensAsync(string userId, CancellationToken cancellationToken = default);
}
