//-----------------------------------------------------------------------
// <copyright file="WidgetClient.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.WindowsWidget;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using AppControlOrganizer.V2.Shared.DTOs;

/// <summary>
/// HTTP client for AppLifePlanner widget endpoints.
/// </summary>
public sealed class WidgetClient
{
  private readonly HttpClient httpClient;

  /// <summary>
  /// Initializes a new instance of the <see cref="WidgetClient"/> class.
  /// </summary>
  public WidgetClient()
  {
    this.httpClient = new HttpClient
    {
      Timeout = TimeSpan.FromSeconds(20),
    };
    this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AppLifePlanner-WindowsWidget");
  }

  /// <summary>Returns whether a local AppLifePlanner URL has no listener before attempting HTTP.</summary>
  public bool IsLocalServerUnavailable(string baseUrl)
  {
    if (!Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out Uri? uri))
    {
      return false;
    }

    if (!IsLocalHost(uri.Host))
    {
      return false;
    }

    int port = uri.IsDefaultPort
      ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
      : uri.Port;

    IPEndPoint[] listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
    return !listeners.Any(listener => listener.Port == port && IsLocalListener(listener.Address));
  }

  /// <summary>Returns whether the base URL points to the current machine.</summary>
  public bool IsLocalBaseUrl(string baseUrl)
  {
    if (!Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out Uri? uri))
    {
      return false;
    }

    return IsLocalHost(uri.Host);
  }

  /// <summary>Exchanges a one-time pairing code for a widget token.</summary>
  public async Task<WidgetPairingExchangeResponseDto> PairAsync(
    string baseUrl,
    string code,
    string deviceName,
    CancellationToken cancellationToken)
  {
    Uri endpoint = BuildUri(baseUrl, "/api/widget/pairing/exchange", null);
    WidgetPairingExchangeRequestDto request = new WidgetPairingExchangeRequestDto
    {
      Code = code,
      DeviceName = deviceName,
    };

    using HttpResponseMessage response = await this.httpClient.PostAsJsonAsync(
      endpoint,
      request,
      cancellationToken);
    response.EnsureSuccessStatusCode();
    WidgetPairingExchangeResponseDto? result =
      await response.Content.ReadFromJsonAsync<WidgetPairingExchangeResponseDto>(cancellationToken);
    return result ?? throw new InvalidOperationException("Pairing response was empty.");
  }

  /// <summary>Gets the current Today snapshot.</summary>
  public async Task<TodayWidgetSnapshotDto> GetTodayAsync(
    string baseUrl,
    string token,
    string language,
    CancellationToken cancellationToken)
  {
    int offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;
    long nonce = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    string query = $"tzOffsetMinutes={offsetMinutes}&lang={Uri.EscapeDataString(language)}&nonce={nonce}";
    Uri endpoint = BuildUri(baseUrl, "/api/widget/today", query);

    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Headers.CacheControl = new CacheControlHeaderValue
    {
      NoCache = true,
      NoStore = true,
    };
    request.Headers.Pragma.ParseAdd("no-cache");
    using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken);
    response.EnsureSuccessStatusCode();
    TodayWidgetSnapshotDto? snapshot =
      await response.Content.ReadFromJsonAsync<TodayWidgetSnapshotDto>(cancellationToken);
    return snapshot ?? throw new InvalidOperationException("Today snapshot response was empty.");
  }

  private static Uri BuildUri(string baseUrl, string path, string? query)
  {
    UriBuilder builder = new UriBuilder(baseUrl.TrimEnd('/'))
    {
      Path = path.TrimStart('/'),
      Query = query ?? string.Empty,
    };
    return builder.Uri;
  }

  private static bool IsLocalHost(string host)
  {
    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
  }

  private static bool IsLocalListener(IPAddress address)
  {
    return IPAddress.IsLoopback(address)
      || address.Equals(IPAddress.Any)
      || address.Equals(IPAddress.IPv6Any);
  }
}
