//-----------------------------------------------------------------------
// <copyright file="WidgetPairingService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Widgets;

using System.Security.Cryptography;
using System.Text.Json;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Stores local widget pairing state in the existing key-value table.
/// </summary>
public sealed class WidgetPairingService : IWidgetPairingService
{
  private const int PairingCodeBytes = 5;
  private const int DeviceTokenBytes = 32;
  private static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;

  /// <summary>
  /// Initializes a new instance of the <see cref="WidgetPairingService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database context factory.</param>
  public WidgetPairingService(IDbContextFactory<LifePlannerContext> contextFactory)
  {
    this.contextFactory = contextFactory;
  }

  /// <inheritdoc />
  public async Task<WidgetPairingCodeResponseDto> CreatePairingCodeAsync(
    string userId,
    CancellationToken cancellationToken = default)
  {
    string code = CreateCode();
    DateTime expiresAtUtc = DateTime.UtcNow.Add(PairingLifetime);
    WidgetPairingCodeRecord record = new WidgetPairingCodeRecord
    {
      OwnerUserId = userId,
      ExpiresAtUtc = expiresAtUtc,
    };

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    context.AppSettings.Add(new AppSetting
    {
      Key = PairingKey(code),
      Value = JsonSerializer.Serialize(record),
    });

    await context.SaveChangesAsync(cancellationToken);
    return new WidgetPairingCodeResponseDto
    {
      Code = code,
      ExpiresAtUtc = expiresAtUtc,
    };
  }

  /// <inheritdoc />
  public async Task<string?> ExchangePairingCodeAsync(
    string code,
    string deviceName,
    CancellationToken cancellationToken = default)
  {
    string normalizedCode = NormalizeCode(code);
    if (string.IsNullOrWhiteSpace(normalizedCode))
    {
      return null;
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    AppSetting? setting = await context.AppSettings
      .FirstOrDefaultAsync(item => item.Key == PairingKey(normalizedCode), cancellationToken);
    if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
    {
      return null;
    }

    WidgetPairingCodeRecord? codeRecord =
      JsonSerializer.Deserialize<WidgetPairingCodeRecord>(setting.Value);
    if (codeRecord == null || codeRecord.ExpiresAtUtc < DateTime.UtcNow)
    {
      context.AppSettings.Remove(setting);
      await context.SaveChangesAsync(cancellationToken);
      return null;
    }

    string token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(DeviceTokenBytes));
    WidgetDeviceTokenRecord tokenRecord = new WidgetDeviceTokenRecord
    {
      OwnerUserId = codeRecord.OwnerUserId,
      DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Windows widget" : deviceName.Trim(),
      CreatedAtUtc = DateTime.UtcNow,
    };

    context.AppSettings.Remove(setting);
    context.AppSettings.Add(new AppSetting
    {
      Key = TokenKey(token),
      Value = JsonSerializer.Serialize(tokenRecord),
    });

    await context.SaveChangesAsync(cancellationToken);
    return token;
  }

  /// <inheritdoc />
  public async Task<string?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(token))
    {
      return null;
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    AppSetting? setting = await context.AppSettings
      .AsNoTracking()
      .FirstOrDefaultAsync(item => item.Key == TokenKey(token), cancellationToken);
    if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
    {
      return null;
    }

    WidgetDeviceTokenRecord? record =
      JsonSerializer.Deserialize<WidgetDeviceTokenRecord>(setting.Value);
    return record?.OwnerUserId;
  }

  /// <inheritdoc />
  public async Task<int> RevokeUserTokensAsync(string userId, CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<AppSetting> tokenSettings = await context.AppSettings
      .Where(item => item.Key.StartsWith("widget-token:"))
      .ToListAsync(cancellationToken);

    int removed = 0;
    foreach (AppSetting setting in tokenSettings)
    {
      if (string.IsNullOrWhiteSpace(setting.Value))
      {
        continue;
      }

      WidgetDeviceTokenRecord? record =
        JsonSerializer.Deserialize<WidgetDeviceTokenRecord>(setting.Value);
      if (record != null && string.Equals(record.OwnerUserId, userId, StringComparison.Ordinal))
      {
        context.AppSettings.Remove(setting);
        removed++;
      }
    }

    await context.SaveChangesAsync(cancellationToken);
    return removed;
  }

  private static string CreateCode()
  {
    string raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(PairingCodeBytes));
    string code = raw.Replace("-", string.Empty, StringComparison.Ordinal)
      .Replace("_", string.Empty, StringComparison.Ordinal)
      .ToUpperInvariant();
    if (code.Length < 6)
    {
      return CreateCode();
    }

    return string.Create(7, code, (span, value) =>
    {
      span[0] = value[0];
      span[1] = value[1];
      span[2] = value[2];
      span[3] = '-';
      span[4] = value[3];
      span[5] = value[4];
      span[6] = value[5];
    });
  }

  private static string NormalizeCode(string code)
  {
    return code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal)
      .ToUpperInvariant();
  }

  private static string PairingKey(string code)
  {
    return $"widget-pair:{NormalizeCode(code)}";
  }

  private static string TokenKey(string token)
  {
    byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
    return $"widget-token:{Base64UrlEncoder.Encode(hash)}";
  }

  private sealed class WidgetPairingCodeRecord
  {
    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
  }

  private sealed class WidgetDeviceTokenRecord
  {
    public string OwnerUserId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
  }
}
