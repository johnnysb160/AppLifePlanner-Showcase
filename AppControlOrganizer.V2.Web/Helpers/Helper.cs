//-----------------------------------------------------------------------
// <copyright file="Helper.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;

using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helper for normalizing and rendering account logos.
/// </summary>
public static class Helper
{
  /// <summary>
  /// Represents the CultureInfo instance for Brazilian Portuguese ("pt-BR").
  /// </summary>
  /// <remarks>This culture is commonly used for formatting dates, numbers, and currencies according to Brazilian
  /// conventions. Use this field when culture-specific formatting for Brazil is required.</remarks>
  public static readonly CultureInfo BrCulture = CreateBrlCulture();

  /// <summary>
  /// Represents the CultureInfo instance for US English ("en-US").
  /// </summary>
  public static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

  /// <summary>
  /// Represents the invariant culture, which is culture-insensitive and associated with the English language but not
  /// with any specific country or region.
  /// </summary>
  /// <remarks>The invariant culture is useful for operations that require consistent results regardless of the
  /// user's locale, such as formatting and parsing data for storage or exchange. It ensures that culture-dependent
  /// operations behave predictably across different environments.</remarks>
  public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

  // The identifier is intentionally spelled as 'unknow' to match business requirement.
  // SVG: simple bank icon with question mark.
  public const string UnknowLogoDataUri = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2NCIgaGVpZ2h0PSI2NCIgdmlld0JveD0iMCAwIDY0IDY0Ij48cmVjdCB3aWR0aD0iNjQiIGhlaWdodD0iNjQiIGZpbGw9IiNlMGUwZTAiIHJ4PSI4Ii8+PHRleHQgeD0iNTAlIiB5PSI1NSUiIGZvbnQtc2l6ZT0iMzIiIGZvbnQtZmFtaWx5PSJBcmlhbCIgZmlsbD0iIzk5OSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+PzwvdGV4dD48L3N2Zz4=";

  /// <summary>
  /// Represents a compiled regular expression that matches currency symbols and whitespace characters commonly found in
  /// currency strings.
  /// </summary>
  /// <remarks>The regular expression matches the dollar sign ("$"), the uppercase letter "R", standard
  /// whitespace, and the non-breaking space (U+00A0). This can be used to remove or identify currency formatting
  /// characters in strings.</remarks>
  private static readonly Regex CurrencyRegex = new Regex(@"[R$\s\u00A0]+", RegexOptions.Compiled);

  /// <summary>
  /// Represents the standard date and time format used for logging.
  /// </summary>
  /// <remarks>This format is consistent with ISO 8601 and includes fractional seconds. It is used to ensure
  /// uniformity in date and time representation across logs.</remarks>
  private const string DateFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";

  /// <summary>
  /// Represents a compiled regular expression that matches masked document numbers containing two or more masking
  /// characters (such as '•' or '*') followed by optional digits, punctuation, or additional masking characters.
  /// </summary>
  /// <remarks>This regular expression is intended to identify strings where sensitive document numbers are
  /// partially or fully masked for privacy or security purposes. It can be used to detect or process masked identifiers
  /// in text, such as credit card or ID numbers that have been obfuscated.</remarks>
  private static readonly Regex MaskedDocumentRegex = new Regex(@"[•\*]{2,}[\d.\-/]*[•\*]*[\d.\-/]*[•\*]*", RegexOptions.Compiled);
  private static readonly Regex NumericDocumentRegex = new Regex(@"\d{2,3}[\.\-]\d{3}[\.\-]\d{3}[\-/]\d{2,4}[\-]?\d{0,2}", RegexOptions.Compiled);
  private static readonly Regex BankAccountBlockRegex = new Regex(@"BCO\s*\d+\s*\(\d+\)\s*Ag[êe]ncia:\s*\d+\s*Conta:\s*[\d\-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex PixTransferSentRegex = new Regex(@"Transfer[êe]ncia\s+enviada\s+pelo\s+Pix\s*-?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex PixTransferReceivedRegex = new Regex(@"Transfer[êe]ncia\s+recebida\s+pelo\s+Pix\s*-?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex PixTransferRegex = new Regex(@"Transfer[êe]ncia\s+Pix\s*-?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex BoletoPaymentRegex = new Regex(@"Pagamento\s+de\s+boleto\s+efetuado\s*-?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex TrailingDashRegex = new Regex(@"\s*-\s*$", RegexOptions.Compiled);
  private static readonly Regex MultipleSpacesRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);

  private static readonly (string Keyword, string Color)[] BrandAccentColors =
  {
    ("NUBANK",        "#8A05BE"),
    ("C6",            "#E6A817"),
    ("DAYCOVAL",      "#003087"),
    ("BRADESCO",      "#CC0000"),
    ("ITAÚ",          "#EC7000"),
    ("ITAU",          "#EC7000"),
    ("SANTANDER",     "#EC0000"),
    ("CAIXA",         "#006495"),
    ("BANCO DO BRASIL", "#FFCC00"),
    ("BB",            "#FFCC00"),
    ("INTER",         "#FF6600"),
    ("WIZE",          "#9FE870"),
    ("WISE",          "#9FE870"),
    ("BITYBANK",      "#F7931A"),
    ("BITY",          "#F7931A"),
    ("XP",            "#888888"),
    ("AVENUE",        "#0062CC"),
    ("BINANCE",       "#F0B90B"),
    ("99",            "#FF0000"),
    ("BISCOINT",      "#F7931A"),
    ("SÓCIOS",        "#EB1A1A"),
    ("SOCIOS",        "#EB1A1A"),
    ("SAMUKAR",       "#2E8B57"),
    ("MERCADO PAGO",  "#009EE3"),
    ("MERCADOPAGO",   "#009EE3"),
    ("PICPAY",        "#21C25E"),
  };

  /// <summary>
  /// Returns the brand accent colour (hex) for a known institution name.
  /// Returns <c>null</c> when the name is not recognised.
  /// </summary>
  /// <param name="name">Account or card name used to identify the institution.</param>
  /// <returns>A hex colour string such as <c>"#8A05BE"</c>, or <c>null</c>.</returns>
  public static string? GetBrandAccentColor(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return null;
    }

    string key = name.ToUpperInvariant().Trim();

    foreach ((string keyword, string color) in BrandAccentColors)
    {
      if (key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
      {
        return color;
      }
    }

    return null;
  }

  /// <summary>
  /// Returns an inline CSS style for the account card header: dark background with a coloured
  /// left border accent derived from the institution brand. Inactive accounts use a muted style.
  /// </summary>
  /// <param name="name">Institution name.</param>
  /// <param name="isActive">Whether the account is active.</param>
  /// <returns>A CSS style string.</returns>
  public static string GetBannerStyle(string? name, bool isActive = true)
  {
    if (!isActive)
    {
      return "background:#2a2d30;color:rgba(255,255,255,.45);border-left:3px solid #6c757d;";
    }

    string accent = GetBrandAccentColor(name) ?? "#6c757d";
    return $"background:#212529;color:rgba(255,255,255,.85);border-left:4px solid {accent};";
  }

  /// <summary>
  /// Returns an inline CSS style for a credit/debit card header: dark background with a coloured
  /// left border accent derived from the associated account brand.
  /// </summary>
  /// <param name="accountName">Name of the account linked to the card.</param>
  /// <param name="isCredit">True for credit cards, false for debit.</param>
  /// <returns>A CSS style string.</returns>
  public static string GetCardBannerStyle(string? accountName, bool isCredit)
  {
    string accent = GetBrandAccentColor(accountName)
      ?? (isCredit ? "#0d6efd" : "#198754");
    return $"background:#212529;color:rgba(255,255,255,.85);border-left:4px solid {accent};";
  }

  /// <summary>
  /// Returns a valid data URI for rendering, applying fallback logic for legacy data.
  /// </summary>
  public static string GetLogoDataUriOrDefault(string? logo)
  {
    if (string.IsNullOrWhiteSpace(logo))
    {
      return UnknowLogoDataUri;
    }

    // Already a data URI
    if (logo.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    {
      return logo;
    }

    // External URL - allow rendering but not stored by backfill
    if (logo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
      || logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      return logo;
    }

    // Assume raw Base64 without prefix - add default image/png prefix
    return $"data:image/png;base64,{logo}";
  }

  /// <summary>
  /// Creates a CultureInfo object for Brazilian Portuguese (pt-BR) with a standardized negative currency format.
  /// </summary>
  /// <remarks>The returned CultureInfo is a clone of the standard "pt-BR" culture with its
  /// NumberFormat.CurrencyNegativePattern property set to 1. This ensures that negative currency values are formatted
  /// with a leading minus sign, which may differ from the system default. The caller is responsible for using this
  /// CultureInfo instance where consistent negative currency formatting is required.</remarks>
  /// <returns>A CultureInfo instance representing Brazilian Portuguese, with the currency negative pattern set to display a
  /// leading minus sign (e.g., "-R$ n").</returns>
  private static CultureInfo CreateBrlCulture()
  {
    CultureInfo cultureInfo = new CultureInfo("pt-BR");
    CultureInfo clone = (CultureInfo)cultureInfo.Clone();

    // Force negative currency to use a leading minus sign ("-n").
    clone.NumberFormat.CurrencyNegativePattern = 1;
    return clone;
  }

  /// <summary>
  /// Amounts are always displayed in BRL, independent of UI language.
  /// </summary>
  public static string FormatBrl(decimal value)
  {
    return value.ToString("C", BrCulture);
  }

  /// <summary>
  /// Formats a decimal value as USD currency.
  /// </summary>
  public static string FormatUsd(decimal value)
  {
    return value.ToString("C", UsCulture);
  }

  /// <summary>
  /// Formats a decimal value according to the specified currency.
  /// </summary>
  public static string FormatCurrency(Currency currency, decimal value)
  {
    switch (currency)
    {
      case Currency.BRL:
        return FormatBrl(value);
      case Currency.USD:
        return FormatUsd(value);
      default:
        if (IsCryptoCurrency(currency))
        {
          return $"⚡{value.ToString(value == 1 ? "1" : "N8", CultureInfo.InvariantCulture)} {currency}";
        }
        return value.ToString("C", UsCulture);
    }
  }

  /// <summary>
  /// Determines whether the specified currency is a cryptocurrency.
  /// </summary>
  private static bool IsCryptoCurrency(Currency currency)
  {
    return (int)currency >= (int)Currency.BTC;
  }

  private static readonly Dictionary<Category, string> CategoryDisplayNames = new Dictionary<Category, string>
  {
    { Category.Salary,       "Salary" },
    { Category.OthersIncome, "Other Income" },
    { Category.Home,         "Home" },
    { Category.Light,        "Electricity" },
    { Category.Food,         "Food" },
    { Category.CompanyCosts, "Company Costs" },
    { Category.Internet,     "Internet" },
    { Category.Health,       "Health" },
    { Category.Vehicles,     "Vehicles" },
    { Category.Leisure,      "Leisure" },
    { Category.ExtraExpenses,"Extra Expenses" },
    { Category.School,       "School/Education" },
  };

  /// <summary>
  /// Returns a user-friendly display name for the specified category value.
  /// </summary>
  /// <param name="category">The category for which to retrieve the display name.</param>
  /// <returns>A string containing the display name associated with the specified category. If the category is not recognized,
  /// returns the category's string representation.</returns>
  /// <summary>
  /// Returns a user-friendly display name for the specified category value.
  /// </summary>
  /// <param name="category">The category for which to retrieve the display name.</param>
  /// <returns>A string containing the display name associated with the specified category. If the category is not recognized,
  /// returns the category's string representation.</returns>
  public static string GetDisplayName(this Category category)
  {
    if (CategoryDisplayNames.TryGetValue(category, out string? name))
    {
      return name;
    }

    return category.ToString();
  }

  private static readonly Dictionary<InstitutionType, string> InstitutionTypeDisplayNames = new Dictionary<InstitutionType, string>
  {
    { InstitutionType.Bank,     "Bank" },
    { InstitutionType.Fintech,  "Fintech" },
    { InstitutionType.Exchange, "Exchange" },
    { InstitutionType.Broker,   "Broker" },
    { InstitutionType.Stopped,  "Inactive" },
    { InstitutionType.Others,   "Others" },
  };

  /// <summary>
  /// Returns a user-friendly display name for the specified institution type.
  /// </summary>
  /// <param name="institutionType">The institution type for which to retrieve the display name.</param>
  /// <returns>A string containing the display name corresponding to the specified institution type. If the type is not
  /// recognized, the method returns the string representation of the value.</returns>
  public static string GetDisplayName(this InstitutionType institutionType)
  {
    if (InstitutionTypeDisplayNames.TryGetValue(institutionType, out string? name))
    {
      return name;
    }

    return institutionType.ToString();
  }

  private static readonly Dictionary<AccountType, string> AccountTypeDisplayNames = new Dictionary<AccountType, string>
  {
    { AccountType.CheckingAccount,  "Checking Account" },
    { AccountType.SavingsAccount,   "Savings Account" },
    { AccountType.InvestmentAccount,"Investment Account" },
    { AccountType.CreditAccount,    "Credit Account" },
    { AccountType.LoanAccount,      "Loan Account" },
    { AccountType.FinancingAccount, "Financing Account" },
    { AccountType.PensionAccount,   "Pension Account" },
    { AccountType.InsuranceAccount, "Insurance Account" },
    { AccountType.ConsortiumAccount,"Consortium Account" },
    { AccountType.OtherAccount,     "Other Account" },
  };

  /// <summary>
  /// Returns a user-friendly display name for the specified account type.
  /// </summary>
  /// <param name="accountType">The account type for which to retrieve the display name.</param>
  /// <returns>A string containing the display name corresponding to the specified account type. If the account type is not
  /// recognized, returns the string representation of the enum value.</returns>
  public static string GetDisplayName(this AccountType accountType)
  {
    if (AccountTypeDisplayNames.TryGetValue(accountType, out string? name))
    {
      return name;
    }

    return accountType.ToString();
  }

  private static readonly Dictionary<Currency, string> CurrencyDisplayNames = new Dictionary<Currency, string>
  {
    { Currency.BRL, "Brazilian Real (BRL)" },
    { Currency.USD, "US Dollar (USD)" },
    { Currency.EUR, "Euro (EUR)" },
    { Currency.GBP, "Pound Sterling (GBP)" },
    { Currency.JPY, "Yen (JPY)" },
    { Currency.CHF, "Swiss Franc (CHF)" },
    { Currency.AUD, "Australian Dollar (AUD)" },
    { Currency.CAD, "Canadian Dollar (CAD)" },
    { Currency.NZD, "New Zealand Dollar (NZD)" },
    { Currency.BTC, "Bitcoin (BTC)" },
    { Currency.ETH, "Ethereum (ETH)" },
  };

  /// <summary>
  /// Returns the user-friendly display name for the specified currency value.
  /// </summary>
  /// <param name="currency">The currency value for which to retrieve the display name.</param>
  /// <returns>A string containing the display name of the specified currency. If the currency is not recognized, returns the
  /// string representation of the currency value.</returns>
  public static string GetDisplayName(this Currency currency)
  {
    if (CurrencyDisplayNames.TryGetValue(currency, out string? name))
    {
      return name;
    }

    return currency.ToString();
  }

  private static readonly Dictionary<CardType, string> CardTypeDisplayNames = new Dictionary<CardType, string>
  {
    { CardType.Credit, "Credit" },
    { CardType.Debit,  "Debit" },
  };

  /// <summary>
  /// Returns a user-friendly display name for the specified card type.
  /// </summary>
  /// <param name="cardType">The card type for which to retrieve the display name.</param>
  /// <returns>A string containing the display name for the specified card type. Returns "Credit" for Credit cards, "Debit" for
  /// Debit cards, or the string representation of the card type if it is unrecognized.</returns>
  public static string GetDisplayName(this CardType cardType)
  {
    if (CardTypeDisplayNames.TryGetValue(cardType, out string? name))
    {
      return name;
    }

    return cardType.ToString();
  }

  /// <summary>
  /// Returns the display name for the specified card flag.
  /// </summary>
  /// <param name="flag">The card flag for which to retrieve the display name.</param>
  /// <returns>A string containing the display name of the specified card flag.</returns>
  public static string GetDisplayName(this CardFlag flag)
  {
    return flag.ToString();
  }

  /// <summary>
  /// Returns the display name for the specified allocation type.
  /// </summary>
  /// <param name="allocationType">The allocation type for which to retrieve the display name.</param>
  /// <returns>A string containing the display name of the specified allocation type.</returns>
  public static string GetDisplayName(this AllocationType allocationType)
  {
    return allocationType.ToString();
  }

  /// <summary>
  /// Cleans bank statement description removing noise like CPF/CNPJ, account info, etc.
  /// </summary>
  public static string CleanBankStatementDescription(string rawDescription)
  {
    if (string.IsNullOrWhiteSpace(rawDescription))
    {
      return string.Empty;
    }

    string cleaned = rawDescription.Trim();

    // Remove masked CPF/CNPJ patterns (e.g., "•••.999.999-••" or "***999.999.999-**")
    cleaned = MaskedDocumentRegex.Replace(cleaned, string.Empty);

    // Remove numeric document patterns (e.g., "123.456.789-00" or "12.345.678/0001-99")
    cleaned = NumericDocumentRegex.Replace(cleaned, string.Empty);

    // Remove bank/branch/account blocks (e.g., "BCO 123 (0001) Agência: 1234 Conta: 12345-6")
    cleaned = BankAccountBlockRegex.Replace(cleaned, string.Empty);

    // Simplify common Pix transfer prefixes
    cleaned = PixTransferSentRegex.Replace(cleaned, "Pix Env - ");
    cleaned = PixTransferReceivedRegex.Replace(cleaned, "Pix Rec - ");
    cleaned = PixTransferRegex.Replace(cleaned, "Pix - ");

    // Simplify boleto payment prefix
    cleaned = BoletoPaymentRegex.Replace(cleaned, "Boleto - ");

    // Remove trailing dash/hyphen
    cleaned = TrailingDashRegex.Replace(cleaned, string.Empty);

    // Collapse multiple spaces
    cleaned = MultipleSpacesRegex.Replace(cleaned, " ").Trim();

    // Limit to 500 chars
    if (cleaned.Length > 500)
    {
      cleaned = cleaned[..500];
    }

    return cleaned;
  }

  /// <summary>
  /// Cleans card invoice description removing date prefixes and formatting installment info.
  /// </summary>
  public static string CleanCardInvoiceDescription(string rawDescription, string installment)
  {
    if (string.IsNullOrWhiteSpace(rawDescription))
    {
      return string.Empty;
    }

    string cleaned = rawDescription.Trim();

    // Remove surrounding quotes
    cleaned = cleaned.Trim('"', '\'');

    // Collapse multiple spaces
    cleaned = MultipleSpacesRegex.Replace(cleaned, " ").Trim();

    // Append installment info if not single payment
    if (!string.IsNullOrWhiteSpace(installment)
      && !string.Equals(installment, "Única", StringComparison.OrdinalIgnoreCase)
      && !string.Equals(installment, "unica", StringComparison.OrdinalIgnoreCase))
    {
      // Format: "2/12" -> "(2/12)"
      string installmentClean = installment.Trim();
      if (!installmentClean.StartsWith('('))
      {
        installmentClean = $"({installmentClean})";
      }
      cleaned = $"{cleaned} {installmentClean}";
    }

    // Limit to 500 chars
    if (cleaned.Length > 500)
    {
      cleaned = cleaned[..500];
    }

    return cleaned;
  }

  /// <summary>
  /// Tries to parse a decimal value from a string, auto-detecting the decimal separator. The last occurrence of '.'
  /// or ',' is treated as the decimal separator.
  /// </summary>
  public static bool TryParseDecimal(string input, out decimal result)
  {
    result = 0m;

    if (string.IsNullOrWhiteSpace(input))
    {
      return false;
    }

    string cleaned = input.Trim();

    // Handle negative sign at any position
    bool isNegative = cleaned.Contains('-');
    cleaned = cleaned.Replace("-", string.Empty).Trim();

    // Remove currency symbols and spaces
    cleaned = CurrencyRegex.Replace(cleaned, string.Empty);

    if (string.IsNullOrEmpty(cleaned))
    {
      return false;
    }

    // Find last occurrence of '.' and ','
    int lastDot = cleaned.LastIndexOf('.');
    int lastComma = cleaned.LastIndexOf(',');

    // Determine which is the decimal separator (the one that appears last)
    if (lastDot > lastComma)
    {
      // US format: 1,234.56 -> remove thousand separator (comma)
      cleaned = cleaned.Replace(",", string.Empty);
    }
    else if (lastComma > lastDot)
    {
      // BR format: 1.234,56 -> remove thousand separator (dot), replace decimal comma with dot
      cleaned = cleaned.Replace(".", string.Empty).Replace(",", ".");
    }

    if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
    {
      return false;
    }

    if (isNegative)
    {
      result = -result;
    }

    return true;
  }

  /// <summary>
  /// Parses a string representation of a date and time using the specified format, or falls back to a general parse if
  /// the format does not match.
  /// </summary>
  /// <param name="value">The string containing the date and time to parse.</param>
  /// <param name="format">The expected date and time format string, as used by DateTime.ParseExact. Must not be null or empty.</param>
  /// <returns>A DateTime value equivalent to the date and time contained in the input string.</returns>
  /// <exception cref="FormatException">Thrown if the input string cannot be parsed as a valid date and time using either the specified format or a general
  /// parse.</exception>
public static DateTime ParseDateTime(string value, string format)
{
    if (DateTime.TryParseExact(value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime result))
    {
        return result;
    }

    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime fallbackResult))
    {
        return fallbackResult;
    }

    throw new FormatException($"Invalid DateTime value: '{value}'");
}

  /// <summary>
  /// Formats the specified value as a SQL-compatible string representation suitable for use in SQL statements.
  /// </summary>
  /// <remarks>This method ensures that values are properly formatted and escaped for safe inclusion in SQL
  /// statements. Strings are escaped to prevent SQL injection, Boolean values are converted to 1 or 0, and DateTime
  /// values are formatted in 'yyyy-MM-dd HH:mm:ss' format. Enums are represented by their integer values. Numeric types
  /// use invariant culture formatting to ensure compatibility across different database drivers.</remarks>
  /// <param name="value">The value to format for SQL. Supported types include Boolean, DateTime, string, Enum, Guid, decimal, double, and
  /// float. May be null.</param>
  /// <returns>A string containing the SQL-formatted representation of the value. Returns "NULL" if the input value is null.</returns>
  public static string FormatSqlValue(object? value)
  {
    if (value == null)
    {
      return "NULL";
    }

    // Handle specific types
    return value switch
    {
      bool b => b ? "1" : "0",
      DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
      string s => $"'{s.Replace("'", "''")}'",
      Enum e => Convert.ToInt32(e).ToString(),
      Guid g => $"'{g}'",
      decimal dec => Convert.ToDouble(dec).ToString(Culture),
      double dbl => dbl.ToString(Culture),
      float flt => flt.ToString(Culture),
      _ => value.ToString()!
    };
  }

  public static ValueConverter<Guid, string> guidConverter = new ValueConverter<Guid, string>(
    v => v.ToString("D", CultureInfo.InvariantCulture),
    v => Guid.Parse(v));

public static ValueConverter<DateTime, string> dateTimeConverter = new ValueConverter<DateTime, string>(
    v => v.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture),
    v => DateTime.SpecifyKind(Helper.ParseDateTime(v, DateFormat), DateTimeKind.Utc));

public static ValueConverter<DateTime?, string?> nullableDateTimeConverter = new ValueConverter<DateTime?, string?>(
    v => v.HasValue ? v.Value.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture) : null,
    v => string.IsNullOrEmpty(v) ? null : DateTime.SpecifyKind(Helper.ParseDateTime(v, DateFormat), DateTimeKind.Utc));

  /// <summary>
  /// Gets the list of currencies available for display in the specified account, ordered alphabetically.
  /// </summary>
  /// <remarks>If the account has supported currencies, they are returned in alphabetical order. Otherwise, the
  /// default currency of the account is returned.</remarks>
  /// <param name="account">The account for which to retrieve the display currencies. This parameter must not be null and should contain
  /// supported currencies if available.</param>
  /// <returns>An IReadOnlyList of Currency objects representing the supported display currencies for the account. If no supported
  /// currencies are available, returns a list containing the account's default currency.</returns>
  public static IReadOnlyList<Currency> GetAccountDisplayCurrencies(AccountEntity account)
  {
    if (account.SupportedCurrencies.Any())
    {
      return account.SupportedCurrencies.OrderBy(c => c.ToString()).ToList();
    }

    return new List<Currency> { account.Currency };
  }

  /// <summary>
  /// Performs an HTTP GET using the provided <see cref="IHttpClientFactory"/>, applying the given timeout and standard headers.
  /// Throws <see cref="HttpRequestException"/> when the response status is not successful.
  /// </summary>
  /// <param name="httpClientFactory">Factory used to create <see cref="HttpClient"/> instances.</param>
  /// <param name="url">The request URL.</param>
  /// <param name="timeout">Timeout to apply to the <see cref="HttpClient"/>.</param>
  /// <param name="cancellationToken">Cancellation token for the operation.</param>
  /// <returns>The response body as string.</returns>
  public static async Task<string> HttpGetAsync(IHttpClientFactory httpClientFactory, string url, TimeSpan timeout, CancellationToken cancellationToken)
  {
    if (httpClientFactory == null)
    {
      throw new ArgumentNullException(nameof(httpClientFactory));
    }

    if (string.IsNullOrEmpty(url))
    {
      throw new ArgumentNullException(nameof(url));
    }

    HttpClient client = httpClientFactory.CreateClient();
    client.Timeout = timeout;
    client.DefaultRequestHeaders.Add("User-Agent", "AppControlOrganizer/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
      throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
    }

    return await response.Content.ReadAsStringAsync(cancellationToken);
  }

  /// <summary>
  /// Executes the provided action with a small retry policy. Logs warnings on failures and returns null when all attempts fail.
  /// </summary>
  /// <typeparam name="T">Reference type returned by the action.</typeparam>
  /// <param name="action">The asynchronous action to execute.</param>
  /// <param name="sourceName">Name of the source used for logging.</param>
  /// <param name="logger">Logger to record warnings.</param>
  /// <param name="cancellationToken">Cancellation token for delays.</param>
  /// <returns>The result of the action or null when all attempts fail.</returns>
  public static async Task<T?> TryWithRetryAsync<T>(Func<Task<T>> action, string sourceName, ILogger logger, CancellationToken cancellationToken) where T : class
  {
    if (action == null)
    {
      throw new ArgumentNullException(nameof(action));
    }

    if (logger == null)
    {
      throw new ArgumentNullException(nameof(logger));
    }

    const int maxAttempts = 2;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
      try
      {
        T result = await action();
        return result;
      }
      catch (Exception ex) when (attempt < maxAttempts)
      {
        logger.LogWarning("Attempt {Attempt} failed for {Source}: {Message}. Retrying...", attempt, sourceName, ex.Message);
        await Task.Delay(500, cancellationToken);
      }
      catch (Exception ex)
      {
        logger.LogWarning("All attempts failed for {Source}: {Message}", sourceName, ex.Message);
      }
    }

    return null;
  }
}
