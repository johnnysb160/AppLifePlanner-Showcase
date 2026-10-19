//-----------------------------------------------------------------------
// <copyright file="CardInvoiceNubankCsvParser.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;

using System.Globalization;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data.Mappers;
/// <summary>
/// Parses Nubank card invoice CSV files. Format: semicolon-delimited with headers: Data de Compra;Nome no Cartão;Final
/// do Cartão;Categoria;Descrição;Parcela;Valor (em US$);Cotação (em R$);Valor (em R$)
/// </summary>
public sealed class CardInvoiceNubankCsvParser
{
  private static readonly CultureInfo PtBrCulture = new CultureInfo("pt-BR");

  /// <summary>
  /// Parses the card invoice CSV stream and returns import candidates. Uses purchaseDate as the Transaction date (not
  /// effectiveDate).
  /// </summary>
  public async Task<List<ImportCandidate>> ParseAsync(Stream stream,
                            int creditCardId,
                            string expectedCardLastFour,
                            IProgress<int>? progress = null,
                            CancellationToken cancellationToken = default)
  {
    List<ImportCandidate> candidates = new List<ImportCandidate>();

    using StreamReader reader = new StreamReader(stream);
    string? headerLine = await reader.ReadLineAsync(cancellationToken);

    if (string.IsNullOrWhiteSpace(headerLine))
    {
            return candidates;
    }

    List<string> allLines = new List<string>();
    string? line;
    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
    {
      if (!string.IsNullOrWhiteSpace(line))
      {
                allLines.Add(line);
      }
    }

    int totalLines = allLines.Count;
    int processed = 0;

    foreach (string currentLine in allLines)
    {
      cancellationToken.ThrowIfCancellationRequested();

      ImportCandidate candidate = ParseLine(currentLine, creditCardId, expectedCardLastFour);
      candidates.Add(candidate);

      processed++;
      if (progress != null && totalLines > 0)
      {
        int percent = (int)((processed * 100.0) / totalLines);
        progress.Report(percent);
      }
    }

    return candidates;
  }

  /// <summary>
  /// Overload for backward compatibility - ignores effectiveDate parameter.
  /// </summary>
  public Task<List<ImportCandidate>> ParseAsync(Stream stream,
                        int creditCardId,
                        string expectedCardLastFour,
                        DateTime effectiveDate,
                        IProgress<int>? progress = null,
                        CancellationToken cancellationToken = default)
  {
    // effectiveDate is now ignored - we use purchaseDate from CSV
    return ParseAsync(stream, creditCardId, expectedCardLastFour, progress, cancellationToken);
  }

  private ImportCandidate ParseLine(string line, int creditCardId, string expectedCardLastFour)
  {
    ImportCandidate candidate = new ImportCandidate();

    // CSV delimiter is semicolon for card invoice
    string[] parts = SplitCsvLine(line, ';');

    if (parts.Length < 9)
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = "Invalid line format (expected 9 columns)";
      candidate.Description = line;
      return candidate;
    }

    // Parse purchase date (column 0) - this is the actual transaction date
    string purchaseDateStr = parts[0].Trim();
    if (!TryParseDate(purchaseDateStr, out DateTime purchaseDate))
    {
      candidate.PendingReason = PendingReason.InvalidDate;
      candidate.PendingMessage = $"Invalid purchase date: {purchaseDateStr}";
      candidate.Description = TransactionImportHasher.NormalizeDescription(parts[4]);
      return candidate;
    }

    // Validate card last four digits (column 2)
    string cardLastFour = parts[2].Trim();
    if (!string.IsNullOrWhiteSpace(expectedCardLastFour)
      && !string.Equals(cardLastFour, expectedCardLastFour, StringComparison.Ordinal))
    {
      candidate.PendingReason = PendingReason.CardMismatch;
      candidate.PendingMessage = $"Card mismatch: expected {expectedCardLastFour}, found {cardLastFour}";
      candidate.Description = TransactionImportHasher.NormalizeDescription(parts[4]);
      candidate.Date = purchaseDate;
      return candidate;
    }

    // Category (column 3)
    string categoryText = parts[3].Trim();

    // Description (column 4)
    string rawDescription = parts[4].Trim();

    // Installment (column 5)
    string installment = parts[5].Trim();

    // Value in BRL (column 8) - use auto-detecting parser
    string valueBrlStr = parts[8].Trim();
    if (!Helper.TryParseDecimal(valueBrlStr, out decimal valueBrl))
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = $"Invalid BRL value: {valueBrlStr}";
      candidate.Description = TransactionImportHasher.NormalizeDescription(rawDescription);
      return candidate;
    }

    // Clean description without date prefix
    string description = Helper.CleanCardInvoiceDescription(rawDescription, installment);

    // Determine income/expense based on sign
    // Negative values = refunds/credits (Income=true)
    // Positive values = purchases (Income=false)
        // Zero values = parse error
        bool isIncome;
    decimal absValue = Math.Abs(valueBrl);

    if (valueBrl < 0)
    {
      // Refund/credit - treat as income
      isIncome = true;
    }
    else if (valueBrl > 0)
    {
      // Purchase - treat as expense
      isIncome = false;
    }
    else
    {
      // Zero amount - flag as parse error
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = "Zero amount value";
      candidate.Date = purchaseDate;
      candidate.Value = 0;
      candidate.Income = false;
      candidate.Description = description;
      candidate.Category = CategoryMapper.MapFromInvoiceCategory(categoryText, rawDescription);
      return candidate;
    }

    // Use purchaseDate as the Transaction date (not effectiveDate)
    candidate.Date = purchaseDate;
    candidate.Income = isIncome;
    candidate.Value = absValue;
    candidate.Description = description;
    candidate.Category = CategoryMapper.MapFromInvoiceCategory(categoryText, rawDescription);
    candidate.SourceDetails = BuildSourceDetails(categoryText, parts[6].Trim(), parts[7].Trim());
    candidate.RawIdentifier = $"{cardLastFour}|{purchaseDateStr}|{valueBrlStr}|{rawDescription}";

    // Include income flag in hash to differentiate purchase from refund of same amount
    candidate.ComputedExternalId = HashComponentsWithSign(creditCardId,
                              purchaseDate,
                              valueBrl,
                              rawDescription,
                              installment);

    return candidate;
  }

  /// <summary>
  /// Creates deterministic hash including the sign to differentiate purchase from refund.
  /// </summary>
  private static int HashComponentsWithSign(int cardId,
                      DateTime purchaseDate,
                      decimal signedAmount,
                      string description,
                      string installment)
  {
    string normalizedDesc = TransactionImportHasher.NormalizeDescription(description);

    // Include signed amount to differentiate purchase (+100) from refund (-100)
    string combined = $"{cardId}|{purchaseDate:yyyyMMdd}|{signedAmount:F2}|{normalizedDesc}|{installment}";

    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(combined);
    byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(inputBytes);

    int result = BitConverter.ToInt32(hashBytes, 0) & 0x7FFFFFFF;
    return result;
  }

  /// <summary>
  /// Builds the source details kept with the transaction: the issuer category and, for foreign purchases,
  /// the original US$ amount and exchange rate. Both are otherwise discarded after the value in BRL is read.
  /// </summary>
  /// <param name="categoryText">The issuer category column.</param>
  /// <param name="usdText">The US$ amount column.</param>
  /// <param name="rateText">The exchange rate column.</param>
  /// <returns>The joined details, or <see langword="null"/> when the row carries nothing worth keeping.</returns>
  private static string? BuildSourceDetails(string categoryText, string usdText, string rateText)
  {
    List<string> details = new List<string>();

    if (!string.IsNullOrWhiteSpace(categoryText) && categoryText.Trim() != "-")
    {
      details.Add(categoryText.Trim());
    }

    if (Helper.TryParseDecimal(usdText, out decimal usd)
      && usd != 0m
      && Helper.TryParseDecimal(rateText, out decimal rate)
      && rate != 0m)
    {
      details.Add(string.Format(CultureInfo.InvariantCulture, "US$ {0:0.00} × R$ {1:0.00}", usd, rate));
    }

    return details.Count == 0 ? null : string.Join(" · ", details);
  }

  /// <summary>
  /// Attempts to parse a date string in Brazilian Portuguese day/month/year format.
  /// </summary>
  /// <remarks>Parsing is performed using the Brazilian Portuguese culture and expects the day/month/year order.
  /// The method does not throw an exception if parsing fails.</remarks>
  /// <param name="dateStr">The date string to parse. The string should be in the format "dd/MM/yyyy", "d/MM/yyyy", or "d/M/yyyy" using the
  /// Brazilian Portuguese culture.</param>
  /// <param name="result">When this method returns, contains the parsed date and time value if the parse operation succeeds; otherwise,
  /// contains the default value for <see cref="DateTime"/>. This parameter is passed uninitialized.</param>
  /// <returns><see langword="true"/> if the date string was parsed successfully; otherwise, <see langword="false"/>.</returns>
  private static bool TryParseDate(string dateStr, out DateTime result)
  {
    if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", PtBrCulture, DateTimeStyles.None, out result))
    {
            return true;
    }

    if (DateTime.TryParseExact(dateStr, "d/MM/yyyy", PtBrCulture, DateTimeStyles.None, out result))
    {
            return true;
    }

    if (DateTime.TryParseExact(dateStr, "d/M/yyyy", PtBrCulture, DateTimeStyles.None, out result))
    {
            return true;
    }

    return false;
  }

  /// <summary>
  /// Splits a single line of delimited text into an array of fields, handling quoted values according to CSV
  /// conventions.
  /// </summary>
  /// <remarks>Quoted fields containing the delimiter character are parsed as a single field. Double quotes
  /// surrounding a field are removed in the result. This method does not handle escaped quotes within quoted
  /// fields.</remarks>
  /// <param name="line">The line of text to split into fields. Fields may be enclosed in double quotes to allow delimiters within values.</param>
  /// <param name="delimiter">The character used to separate fields in the line.</param>
  /// <returns>An array of strings containing the individual fields parsed from the input line. Fields are unquoted and trimmed of
  /// surrounding double quotes.</returns>
  private static string[] SplitCsvLine(string line, char delimiter)
  {
    List<string> result = new List<string>();
    bool inQuotes = false;
    int start = 0;

    for (int i = 0; i < line.Length; i++)
    {
      char c = line[i];

      if (c == '"')
      {
        inQuotes = !inQuotes;
      }
      else if (c == delimiter && !inQuotes)
      {
        result.Add(line.Substring(start, i - start).Trim('"'));
        start = i + 1;
      }
    }

    result.Add(line.Substring(start).Trim('"'));
    return result.ToArray();
  }
}
