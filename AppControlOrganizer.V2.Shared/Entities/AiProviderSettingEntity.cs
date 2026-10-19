//-----------------------------------------------------------------------
// <copyright file="AiProviderSettingEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Stores the AI provider configuration for a single user.
/// One record per user; the ApiKey is stored encrypted at rest via EF Core value converter.
/// </summary>
/// <remarks>
/// Future phases will introduce FinancialSnapshotService, MarketContextService and AiInsightService
/// that compose controlled data packages for the AI layer. The AI provider must never receive raw
/// sensitive fields such as account numbers, card numbers, CPF or OAuth secrets.
/// </remarks>
public sealed class AiProviderSettingEntity : BaseEntity
{
  /// <summary>Gets or sets the ASP.NET Identity user ID that owns this record.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the type of AI provider backend.</summary>
  public AiProviderKind ProviderKind { get; set; } = AiProviderKind.OpenAICompatible;

  /// <summary>Gets or sets a friendly name shown in the UI for this provider configuration.</summary>
  public string DisplayName { get; set; } = "Default AI Provider";

  /// <summary>Gets or sets the base URL of the provider API. Required for OpenAICompatible; optional for hosted providers.</summary>
  public string? BaseUrl { get; set; }

  /// <summary>Gets or sets the model identifier (e.g. gpt-4o, claude-3-5-sonnet-20241022).</summary>
  public string Model { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the API key used to authenticate with the provider.
  /// Stored encrypted at rest; decrypted transparently by the EF Core value converter on read.
  /// Never returned or displayed to the client after initial save.
  /// </summary>
  public string? ApiKey { get; set; }

  /// <summary>Gets or sets whether AI features are active for this user.</summary>
  public bool IsEnabled { get; set; } = false;

  /// <summary>Gets or sets the sampling temperature sent to the provider (0.0–2.0). Null uses the provider default.</summary>
  public decimal? Temperature { get; set; }

  /// <summary>Gets or sets the maximum number of tokens the model may generate per request. Null uses the provider default.</summary>
  public int? MaxTokens { get; set; }

  /// <summary>
  /// Gets or sets the privacy mode identifier controlling what data may be forwarded to the AI.
  /// Default: "AggregatedDataOnly" — only aggregated, anonymised snapshots are sent.
  /// </summary>
  public string PrivacyMode { get; set; } = "AggregatedDataOnly";

  /// <summary>Gets or sets whether the AI may receive aggregated transaction data in future analysis calls.</summary>
  public bool AllowTransactionsAnalysis { get; set; } = false;

  /// <summary>Gets or sets whether the AI may receive aggregated allocation/portfolio data in future analysis calls.</summary>
  public bool AllowAllocationsAnalysis { get; set; } = false;

  /// <summary>Gets or sets whether the AI may receive market quote context in future analysis calls.</summary>
  public bool AllowMarketQuotesAnalysis { get; set; } = false;

  /// <summary>Gets or sets the UTC timestamp of the most recent connection test, or null if never tested.</summary>
  public DateTime? LastTestedAtUtc { get; set; }

  /// <summary>Gets or sets whether the most recent connection test succeeded, or null if never tested.</summary>
  public bool? LastTestSucceeded { get; set; }

  /// <summary>Gets or sets the human-readable result message of the most recent connection test.</summary>
  public string? LastTestMessage { get; set; }

  /// <summary>Gets or sets the UTC timestamp of the last configuration update.</summary>
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
