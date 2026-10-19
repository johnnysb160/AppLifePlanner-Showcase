//-----------------------------------------------------------------------
// <copyright file="AiProviderTestService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Ai;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests AI provider connectivity by sending a minimal chat completion request.
/// Only OpenAICompatible is fully implemented in Phase 1.
/// All other provider kinds return an informational "not yet implemented" result.
/// </summary>
public sealed class AiProviderTestService : IAiProviderTestService
{
  private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

  private readonly IHttpClientFactory httpClientFactory;
  private readonly ILogger<AiProviderTestService> logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="AiProviderTestService"/> class.
  /// </summary>
  /// <param name="httpClientFactory">Factory used to create isolated HTTP clients.</param>
  /// <param name="logger">Logger for diagnostic output (must never log secrets).</param>
  public AiProviderTestService(IHttpClientFactory httpClientFactory, ILogger<AiProviderTestService> logger)
  {
    this.httpClientFactory = httpClientFactory;
    this.logger = logger;
  }

  /// <inheritdoc />
  public async Task<AiProviderTestResult> TestAsync(AiProviderSettingEntity setting, string? newApiKey)
  {
    DateTime testedAt = DateTime.UtcNow;

    string effectiveKey = !string.IsNullOrWhiteSpace(newApiKey)
      ? newApiKey
      : setting.ApiKey ?? string.Empty;

    return setting.ProviderKind switch
    {
      // OpenAI-compatible path: all providers that expose a /v1/chat/completions endpoint
      AiProviderKind.OpenAICompatible => await TestOpenAiCompatibleAsync(setting, effectiveKey, testedAt),
      AiProviderKind.OpenAI           => await TestOpenAiCompatibleAsync(setting, effectiveKey, testedAt),
      AiProviderKind.Local            => await TestOpenAiCompatibleAsync(setting, effectiveKey, testedAt),
      // Gemini exposes an official OpenAI-compatible endpoint at generativelanguage.googleapis.com/v1beta/openai/
      AiProviderKind.Gemini           => await TestOpenAiCompatibleAsync(setting, effectiveKey, testedAt),
      AiProviderKind.Anthropic        => NotImplementedResult(testedAt, "Anthropic"),
      _                               => NotImplementedResult(testedAt, setting.ProviderKind.ToString()),
    };
  }

  private async Task<AiProviderTestResult> TestOpenAiCompatibleAsync(
    AiProviderSettingEntity setting,
    string apiKey,
    DateTime testedAt)
  {
    if (string.IsNullOrWhiteSpace(setting.BaseUrl))
    {
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = "Base URL is not configured. Enter the provider base URL.",
        TestedAtUtc = testedAt,
      };
    }

    if (string.IsNullOrWhiteSpace(setting.Model))
    {
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = "Model is not configured. Enter the model name.",
        TestedAtUtc = testedAt,
      };
    }

    if (string.IsNullOrWhiteSpace(apiKey))
    {
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = "API key was not found. Save the configuration with an API key before testing.",
        TestedAtUtc = testedAt,
      };
    }

    // BaseUrl must already include the full path to the OpenAI-compatible base
    // (e.g. https://api.openai.com/v1 or https://generativelanguage.googleapis.com/v1beta/openai).
    // Always append only /chat/completions — never inject an extra /v1 segment.
    string baseUrl = setting.BaseUrl.TrimEnd('/');

    string endpoint = $"{baseUrl}/chat/completions";

    // Validate that the resulting URI is well-formed before making the network call
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsedUri) ||
        (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
    {
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = $"Invalid base URL: \"{setting.BaseUrl}\". Enter a valid absolute URL (for example, https://api.openai.com/v1).",
        TestedAtUtc = testedAt,
      };
    }

    string body = JsonSerializer.Serialize(new
    {
      model    = setting.Model,
      messages = new[] { new { role = "user", content = "Return only OK" } },
      max_tokens = 5,
    });

    try
    {
      using HttpClient client = httpClientFactory.CreateClient();
      client.Timeout = RequestTimeout;
      client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);


      using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
      using HttpResponseMessage response = await client.PostAsync(parsedUri, content);

      int statusCode = (int)response.StatusCode;

      if (response.IsSuccessStatusCode)
      {
        logger.LogInformation("AI provider test succeeded for model {Model}.", setting.Model);
        return new AiProviderTestResult
        {
          Succeeded   = true,
          Message     = "Connection established successfully.",
          TestedAtUtc = testedAt,
        };
      }

      // HTTP 429 means the server was reached and recognised the request — connectivity is confirmed.
      // Rate limiting is an account/quota issue, not a configuration problem.
      if (statusCode == 429)
      {
        logger.LogInformation("AI provider test reached server for model {Model} (HTTP 429 rate limit).", setting.Model);
        return new AiProviderTestResult
        {
          Succeeded   = true,
          Message     = "Connection established. The server responded with rate limiting (HTTP 429); the endpoint and key appear valid, but the account has reached its current usage limit.",
          TestedAtUtc = testedAt,
        };
      }

      // Log status code only — never log headers or response body (may contain key echoes)
      logger.LogWarning("AI provider test failed with HTTP {StatusCode}.", statusCode);

      string friendlyError = statusCode switch
      {
        401 => "Authentication failed (HTTP 401). Check the API key.",
        403 => "Access denied (HTTP 403). Check the API key permissions.",
        404 => "Endpoint not found (HTTP 404). Check the base URL and model.",
        _   => $"HTTP error {statusCode}. Check the provider configuration.",
      };

      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = friendlyError,
        TestedAtUtc = testedAt,
      };
    }
    catch (TaskCanceledException)
    {
      logger.LogWarning("AI provider test timed out after {Timeout}s.", RequestTimeout.TotalSeconds);
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = $"Request timed out ({RequestTimeout.TotalSeconds}s). Check the base URL and connectivity.",
        TestedAtUtc = testedAt,
      };
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning("AI provider test network error: {Message}.", ex.Message);
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = $"Network error: {ex.Message}",
        TestedAtUtc = testedAt,
      };
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Unexpected error during AI provider test.");
      return new AiProviderTestResult
      {
        Succeeded   = false,
        Message     = "Unexpected error while testing the connection.",
        TestedAtUtc = testedAt,
      };
    }
  }

  private static AiProviderTestResult NotImplementedResult(DateTime testedAt, string providerName)
  {
    return new AiProviderTestResult
    {
      Succeeded   = false,
      Message     = $"Connection test for {providerName} is not implemented in this phase yet.",
      TestedAtUtc = testedAt,
    };
  }
}
