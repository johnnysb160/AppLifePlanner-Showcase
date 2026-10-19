//-----------------------------------------------------------------------
// <copyright file="LocService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Localization;

using System.Globalization;
using Microsoft.Extensions.Localization;

/// <summary>
/// Scoped UI localization facade backed by ASP.NET Core resource files.
/// </summary>
public sealed class LocService
{
  private const string EnglishLanguage = "en";
  private const string PortugueseLanguage = "pt";
  private const string EnglishCultureName = "en-US";
  private const string PortugueseCultureName = "pt-BR";

  private readonly IStringLocalizer<SharedResource> localizer;

  /// <summary>
  /// Initializes a new instance of the <see cref="LocService"/> class.
  /// </summary>
  /// <param name="localizer">The shared resource localizer.</param>
  public LocService(IStringLocalizer<SharedResource> localizer)
  {
    this.localizer = localizer;
  }

  /// <summary>
  /// Gets the active language code for this circuit.
  /// </summary>
  public string Language { get; private set; } = EnglishLanguage;

  /// <summary>
  /// Gets the current culture used by UI formatting.
  /// </summary>
  public CultureInfo Culture
  {
    get
    {
      return CultureInfo.GetCultureInfo(this.Language == PortugueseLanguage ? PortugueseCultureName : EnglishCultureName);
    }
  }

  /// <summary>
  /// Gets the compact day/month pattern for the active language. Used where the culture's full
  /// short-date pattern will not fit, such as chart axis labels.
  /// </summary>
  public string CompactDayMonthPattern
  {
    get
    {
      return this.Language == PortugueseLanguage ? "dd/MM" : "MM/dd";
    }
  }

  /// <summary>
  /// Sets the active UI language for this circuit.
  /// </summary>
  /// <param name="language">The requested language code.</param>
  public void Set(string? language)
  {
    this.Language = NormalizeLanguage(language);
    CultureInfo culture = this.Culture;
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
  }

  /// <summary>
  /// Returns the localized string for a resource key.
  /// </summary>
  /// <param name="key">The resource key.</param>
  public string this[string key]
  {
    get
    {
      return this.Get(key);
    }
  }

  /// <summary>
  /// Formats a localized string using the current UI culture.
  /// </summary>
  /// <param name="key">The resource key.</param>
  /// <param name="arguments">Format arguments.</param>
  /// <returns>The formatted localized string.</returns>
  public string Format(string key, params object[] arguments)
  {
    return string.Format(this.Culture, this.Get(key), arguments);
  }

  /// <summary>
  /// Normalizes a stored language code to the supported language set.
  /// </summary>
  /// <param name="language">The language code to normalize.</param>
  /// <returns><c>pt</c> for Portuguese; otherwise <c>en</c>.</returns>
  public static string NormalizeLanguage(string? language)
  {
    return string.Equals(language, PortugueseLanguage, StringComparison.OrdinalIgnoreCase)
      ? PortugueseLanguage
      : EnglishLanguage;
  }

  private string Get(string key)
  {
    CultureInfo previousCulture = CultureInfo.CurrentCulture;
    CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

    try
    {
      CultureInfo culture = this.Culture;
      CultureInfo.CurrentCulture = culture;
      CultureInfo.CurrentUICulture = culture;

      LocalizedString localized = this.localizer[key];
      if (localized.ResourceNotFound)
      {
        return this.GetCaseInsensitiveFallback(key);
      }

      return localized.Value;
    }
    finally
    {
      CultureInfo.CurrentCulture = previousCulture;
      CultureInfo.CurrentUICulture = previousUiCulture;
    }
  }

  private string GetCaseInsensitiveFallback(string key)
  {
    foreach (LocalizedString candidate in this.localizer.GetAllStrings(includeParentCultures: true))
    {
      if (string.Equals(candidate.Name, key, StringComparison.OrdinalIgnoreCase))
      {
        return MatchLeadingCase(key, candidate.Value);
      }
    }

    return key;
  }

  private static string MatchLeadingCase(string key, string value)
  {
    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || !char.IsLower(key[0]))
    {
      return value;
    }

    return char.ToLower(value[0], CultureInfo.CurrentUICulture) + value[1..];
  }
}
