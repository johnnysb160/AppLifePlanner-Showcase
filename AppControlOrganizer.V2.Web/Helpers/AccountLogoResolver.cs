//-----------------------------------------------------------------------
// <copyright file="AccountLogoResolver.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;

using AppControlOrganizer.V2.Shared.Entities;
/// <summary>
/// Resolves account logos from the internet using known bank domains.
/// </summary>
public class AccountLogoResolver
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<AccountLogoResolver> logger;
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountLogoResolver"/> class with required dependencies.
    /// </summary>
    public AccountLogoResolver(IHttpClientFactory httpClientFactory, ILogger<AccountLogoResolver> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

  // Max image size: 512 KB
  private const int MaxImageSizeBytes = 512 * 1024;

  // Known bank domains mapped by name keywords
  private static readonly Dictionary<string, string> KnownDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    { "Nubank", "nubank.com.br" },
    { "Santander", "santander.com.br" },
    { "Bradesco", "bradesco.com.br" },
    { "Itaú", "itau.com.br" },
    { "Itau", "itau.com.br" },
    { "Banco do Brasil", "bb.com.br" },
    { "Caixa", "caixa.gov.br" },
    { "Inter", "bancointer.com.br" },
    { "C6", "c6bank.com.br" },
    { "Mercado Pago", "mercadopago.com.br" },
    { "PagBank", "pagseguro.com.br" },
    { "PicPay", "picpay.com" },
    { "BTG", "btgpactual.com" },
    { "XP", "xpi.com.br" },
    { "Rico", "rico.com.vc" },
    { "Clear", "clear.com.br" },
    { "Sicoob", "sicoob.com.br" },
    { "Sicredi", "sicredi.com.br" }
  };

  /// <summary>
  /// Attempts to resolve logo for account. Returns default if not found.
  /// </summary>
  public async Task<string> ResolveLogoDataUriAsync(AccountEntity account, CancellationToken ct)
  {
    string? domain = TryGetDomain(account);
    if (string.IsNullOrEmpty(domain))
    {
      return Helper.UnknowLogoDataUri;
    }

    // Primary: Google favicon service (most reliable)
    string googleFaviconUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=128";
    string? result = await TryDownloadImageAsDataUriAsync(googleFaviconUrl, ct);
    if (!string.IsNullOrEmpty(result))
    {
      return result;
    }

    // Fallback: Clearbit logo API (may be unavailable)
    string clearbitUrl = $"https://logo.clearbit.com/{domain}?size=256";
    result = await TryDownloadImageAsDataUriAsync(clearbitUrl, ct);
    if (!string.IsNullOrEmpty(result))
    {
      return result;
    }

    return Helper.UnknowLogoDataUri;
  }

  /// <summary>
  /// Tries to match account name or bank code to a known domain.
  /// </summary>
  private static string? TryGetDomain(AccountEntity account)
  {
    string name = account.Name ?? string.Empty;

    foreach (KeyValuePair<string, string> kvp in KnownDomains)
    {
      if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
      {
        return kvp.Value;
      }
    }

    // Check bank code for common codes
    string bankCode = account.BankCode ?? string.Empty;
    return bankCode switch
    {
      "260" => "nubank.com.br",
      "033" => "santander.com.br",
      "237" => "bradesco.com.br",
      "341" => "itau.com.br",
      "001" => "bb.com.br",
      "104" => "caixa.gov.br",
      "077" => "bancointer.com.br",
      "336" => "c6bank.com.br",
      _ => null
    };
  }

  /// <summary>
  /// Downloads image and converts to data URI. Returns null on failure.
  /// </summary>
  private async Task<string?> TryDownloadImageAsDataUriAsync(string url, CancellationToken ct)
  {
    try
    {
      HttpClient client = httpClientFactory.CreateClient("LogoFetch");
      using HttpResponseMessage response = await client.GetAsync(url, ct);

      if (!response.IsSuccessStatusCode)
      {
        return null;
      }

      string? contentType = response.Content.Headers.ContentType?.MediaType;
      if (string.IsNullOrEmpty(contentType)
        || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
      if (bytes.Length == 0 || bytes.Length > MaxImageSizeBytes)
      {
        return null;
      }

      string base64 = Convert.ToBase64String(bytes);
      return $"data:{contentType};base64,{base64}";
    }
    catch (Exception ex)
    {
      logger.LogDebug(ex, "Failed to download logo from {Url}", url);
      return null;
    }
  }
}
