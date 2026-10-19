//-----------------------------------------------------------------------
// <copyright file="BankStatementCsvParser.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Helpers;

using System.Globalization;
using System.Text.RegularExpressions;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data.Mappers;
/// <summary>
/// Parses bank statement CSV files. Supports comma, semicolon, or tab delimiters (auto-detected from header).
/// </summary>
public sealed partial class BankStatementCsvParser
{
  private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

  [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
  private static partial Regex StmtTrnRegex();

  [GeneratedRegex(@"<DTPOSTED>(\d{8})", RegexOptions.IgnoreCase)]
  private static partial Regex DtPostedRegex();

  [GeneratedRegex(@"<TRNAMT>([-\d.,]+)", RegexOptions.IgnoreCase)]
  private static partial Regex TrnAmtRegex();

  [GeneratedRegex(@"<FITID>([^<\r\n]+)", RegexOptions.IgnoreCase)]
  private static partial Regex FitIdRegex();

  [GeneratedRegex(@"<MEMO>([^<\r\n]+)", RegexOptions.IgnoreCase)]
  private static partial Regex MemoRegex();

  [GeneratedRegex(@"<NAME>([^<\r\n]+)", RegexOptions.IgnoreCase)]
  private static partial Regex NameRegex();

  /// <summary>
  /// Parses the bank statement CSV stream and returns import candidates.
  /// </summary>
  public async Task<List<ImportCandidate>> ParseAsync(Stream stream,
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

    // Auto-detect delimiter from header line
    char delimiter = DetectDelimiter(headerLine);

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

      ImportCandidate candidate = ParseLine(currentLine, delimiter);
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
  ///  Generates a manual external identifier for the specified transaction entity based on its date, value, income
  /// status, and normalized description.
  /// </summary>
  /// <remarks>The generated identifier is derived from a hash of key transaction properties. Identifiers may not
  /// be unique across all transactions, especially if properties are identical.</remarks>
  /// <param name="entity">The transaction entity for which to generate the external identifier. Must contain valid date, value, income, and
  /// description properties.</param>
  /// <returns>An integer representing the generated external identifier for the transaction entity.</returns>
  public static int GenerateManualExternalId(TransactionEntity entity)
  {
    string cleanDesc = TransactionImportHasher.NormalizeDescription(entity.Description);
    string rawKey = $"{entity.Date:yyyyMMdd}_{entity.Value:F2}_{entity.Income}_{cleanDesc}";

    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
    byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(inputBytes);
    return BitConverter.ToInt32(hashBytes, 0) & 0x7FFFFFFF;
  }

  /// <summary>
  /// Detects the most likely delimiter character used in a delimited text header line.
  /// </summary>
  /// <remarks>This method checks for a tab character first, then a semicolon, and defaults to a comma if neither
  /// is found. This order is chosen to accommodate common export formats, such as those from Nubank, which often use
  /// tabs as delimiters.</remarks>
  /// <param name="headerLine">The header line of the delimited text file to analyze. Cannot be null.</param>
  /// <returns>A character representing the detected delimiter: tab ('\t') if present, otherwise semicolon (';') if present, or
  /// comma (',') if neither is found.</returns>
  private static char DetectDelimiter(string headerLine)
  {
    // Check for tab first (Nubank export often uses tab)
    if (headerLine.Contains('\t'))
    {
      return '\t';
    }

    // Check for semicolon
    if (headerLine.Contains(';'))
    {
      return ';';
    }

    // Default to comma
    return ',';
  }

  /// <summary>
  /// Parses a single line of delimited text into an import candidate, extracting date, value, identifier, and
  /// description fields.
  /// </summary>
  /// <remarks>The method expects the input line to contain at least four columns: date, value, identifier, and
  /// description. If the date or value cannot be parsed, or if the column count is insufficient, the returned
  /// ImportCandidate will be marked as pending with an appropriate reason. The description is normalized and cleaned
  /// before assignment.</remarks>
  /// <param name="line">The line of text to parse, typically representing a single record from a delimited file.</param>
  /// <param name="delimiter">The character used to separate columns in the input line.</param>
  /// <returns>An ImportCandidate object containing the parsed data. If the line is invalid or cannot be parsed, the returned
  /// object will indicate the reason in its PendingReason and PendingMessage properties.</returns>
  private ImportCandidate ParseLine(string line, char delimiter)
  {
    ImportCandidate candidate = new ImportCandidate();

    string[] parts = SplitCsvLine(line, delimiter);

    if (parts.Length < 4)
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = $"Invalid line format (expected 4 columns, found {parts.Length})";
      candidate.Description = line.Length > 100 ? line.Substring(0, 100) : line;
      return candidate;
    }

    // Parse date (dd/MM/yyyy or d/MM/yyyy)
    string dateStr = parts[0].Trim();
    if (!TryParseDate(dateStr, out DateTime parsedDate))
    {
      candidate.PendingReason = PendingReason.InvalidDate;
      candidate.PendingMessage = $"Invalid date format: {dateStr}";
      candidate.Description = TransactionImportHasher.NormalizeDescription(parts.Length > 3 ? parts[3] : line);
      return candidate;
    }

    // Parse value using auto-detecting parser
    string valueStr = parts[1].Trim();
    if (!Helper.TryParseDecimal(valueStr, out decimal value))
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = $"Invalid value format: {valueStr}";
      candidate.Description = TransactionImportHasher.NormalizeDescription(parts.Length > 3 ? parts[3] : line);
      return candidate;
    }

    // Identifier (GUID)
    string identifier = parts[2].Trim();

    // Description - clean before normalizing
    string rawDescription = parts[3];
    string cleanedDescription = Helper.CleanBankStatementDescription(rawDescription);
    string description = TransactionImportHasher.NormalizeDescription(cleanedDescription);

    candidate.Date = parsedDate;
    candidate.Income = value > 0;
    candidate.Value = Math.Abs(value);
    candidate.Description = description;
    candidate.RawIdentifier = identifier;
    candidate.ComputedExternalId = TransactionImportHasher.HashGuid(identifier);

    // Keep the original statement text when cleaning dropped part of it (document numbers, bank details).
    string originalText = TransactionImportHasher.NormalizeDescription(rawDescription);
    if (!string.Equals(originalText, description, StringComparison.Ordinal))
    {
      candidate.SourceDetails = originalText;
    }

    // Use improved category mapping based on description
    candidate.Category = CategoryMapper.GetBankStatementCategory(description, candidate.Income);

    return candidate;
  }

  /// <summary>
  /// Attempts to parse the specified string as a date in one of several supported formats.
  /// </summary>
  /// <remarks>Parsing is performed using the Brazilian Portuguese culture. The method does not throw an exception
  /// if parsing fails.</remarks>
  /// <param name="dateStr">The string representation of the date to parse. Expected formats include "dd/MM/yyyy", "d/MM/yyyy", or "d/M/yyyy".</param>
  /// <param name="result">When this method returns, contains the parsed date and time value if the parse operation succeeds; otherwise,
  /// contains the default value for <see cref="DateTime"/>. This parameter is passed uninitialized.</param>
  /// <returns><see langword="true"/> if the string was successfully parsed as a date; otherwise, <see langword="false"/>.</returns>
  private static bool TryParseDate(string dateStr, out DateTime result)
  {
    // Try dd/MM/yyyy first
    if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", Helper.BrCulture, DateTimeStyles.None, out result))
    {
      return true;
    }

    // Try d/MM/yyyy (single digit day)
    if (DateTime.TryParseExact(dateStr, "d/MM/yyyy", Helper.BrCulture, DateTimeStyles.None, out result))
    {
      return true;
    }

    // Try d/M/yyyy
    if (DateTime.TryParseExact(dateStr, "d/M/yyyy", Helper.BrCulture, DateTimeStyles.None, out result))
    {
      return true;
    }

    return false;
  }

  /// <summary>
  /// Splits a single line of delimited text into an array of fields, handling quoted values according to CSV
  /// conventions.
  /// </summary>
  /// <remarks>Quoted fields are supported and may contain delimiter characters. Quotes are removed from the start
  /// and end of each field if present. This method does not handle escaped quotes within quoted fields.</remarks>
  /// <param name="line">The line of text to split into fields. May contain quoted values and embedded delimiters.</param>
  /// <param name="delimiter">The character used to separate fields in the line.</param>
  /// <returns>An array of strings containing the individual fields parsed from the input line. Fields that were quoted in the
  /// input will have surrounding quotes removed.</returns>
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

  /// <summary>
  /// Parses an OFX stream and returns import candidates.
  /// </summary>
  public async Task<List<ImportCandidate>> ParseOfxAsync(Stream stream,
                            IProgress<int>? progress = null,
                            CancellationToken cancellationToken = default)
  {
    List<ImportCandidate> candidates = new List<ImportCandidate>();

    using StreamReader reader = new StreamReader(stream);
    string content = await reader.ReadToEndAsync(cancellationToken);

    MatchCollection matches = StmtTrnRegex().Matches(content);
    int total = matches.Count;
    int processed = 0;

    foreach (Match match in matches)
    {
      cancellationToken.ThrowIfCancellationRequested();

      ImportCandidate candidate = ParseTransaction(match.Groups[1].Value);
      candidates.Add(candidate);

      processed++;
      if (progress != null && total > 0)
      {
        int percent = (int)((processed * 100.0) / total);
        progress.Report(percent);
      }
    }

    return candidates;
  }

  /// <summary>
  /// Parses a transaction record from its raw content and returns an import candidate representing the transaction
  /// details.
  /// </summary>
  /// <remarks>If the transaction content is missing required fields or contains invalid data, the returned
  /// ImportCandidate will have its PendingReason and PendingMessage properties set to describe the issue. The method
  /// attempts to extract the transaction date, amount, identifier, and description from the input content.</remarks>
  /// <param name="transactionContent">The raw transaction content to parse. Must contain valid transaction fields such as date, amount, and identifiers
  /// in the expected format.</param>
  /// <returns>An ImportCandidate object containing the parsed transaction details. If required fields are missing or invalid, the
  /// returned object will indicate the reason in its pending state.</returns>
  private ImportCandidate ParseTransaction(string transactionContent)
  {
    ImportCandidate candidate = new ImportCandidate();

    // Parse DTPOSTED (yyyyMMdd)
    Match dateMatch = DtPostedRegex().Match(transactionContent);
    if (!dateMatch.Success
      || !DateTime.TryParseExact(dateMatch.Groups[1].Value,
                  "yyyyMMdd",
                  InvariantCulture,
                  DateTimeStyles.None,
                  out DateTime date))
    {
      candidate.PendingReason = PendingReason.InvalidDate;
      candidate.PendingMessage = "Invalid or missing DTPOSTED";
      return candidate;
    }

    // Parse TRNAMT
    Match amtMatch = TrnAmtRegex().Match(transactionContent);
    if (!amtMatch.Success)
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = "Missing TRNAMT";
      return candidate;
    }

    string amtStr = amtMatch.Groups[1].Value.Replace(",", ".");
    if (!decimal.TryParse(amtStr, NumberStyles.Number, InvariantCulture, out decimal amount))
    {
      candidate.PendingReason = PendingReason.ParseError;
      candidate.PendingMessage = $"Invalid TRNAMT: {amtMatch.Groups[1].Value}";
      return candidate;
    }

    // Parse FITID (identifier)
    Match fitIdMatch = FitIdRegex().Match(transactionContent);
    string fitId = fitIdMatch.Success ? fitIdMatch.Groups[1].Value.Trim() : string.Empty;

    // Parse MEMO or NAME for description
    string description = string.Empty;
    Match memoMatch = MemoRegex().Match(transactionContent);
    if (memoMatch.Success)
    {
      description = memoMatch.Groups[1].Value.Trim();
    }
    else
    {
      Match nameMatch = NameRegex().Match(transactionContent);
      if (nameMatch.Success)
      {
        description = nameMatch.Groups[1].Value.Trim();
      }
    }

    candidate.Date = date;
    candidate.Income = amount > 0;
    candidate.Value = Math.Abs(amount);
    candidate.Description = TransactionImportHasher.NormalizeDescription(description);
    candidate.RawIdentifier = fitId;
    candidate.ComputedExternalId = string.IsNullOrWhiteSpace(fitId) ? null : TransactionImportHasher.HashGuid(fitId);
    candidate.Category = CategoryMapper.GetDefaultBankStatementCategory(candidate.Income);

    return candidate;
  }
}
