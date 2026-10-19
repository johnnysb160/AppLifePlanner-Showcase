//-----------------------------------------------------------------------
// <copyright file="ImapEmailSyncService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Imap;

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Data.Mappers;
using AppControlOrganizer.V2.Web.Services.Logging;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using Serilog.Context;

/// <summary>
/// Scoped service that connects to each user's IMAP account, identifies unread emails from
/// mapped senders, parses Brazilian bank transaction notifications via regex, and persists
/// the resulting <see cref="TransactionEntity"/> records.
/// </summary>
public sealed class ImapEmailSyncService : IImapEmailSyncService
{
  // BRL amount: R$ 1.234,56 or R$ 100,00 (handles thousands separator and decimal comma)
  private static readonly Regex AmountRegex = new Regex(
    @"R\$\s*([0-9]{1,3}(?:\.[0-9]{3})*(?:,[0-9]{2})|[0-9]+,[0-9]{2})",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Date in DD/MM/YYYY format
  private static readonly Regex ShortDateRegex = new Regex(
    @"\b(\d{2}/\d{2}/\d{4})\b",
    RegexOptions.Compiled);

  // Date in "DD de mês de YYYY" format (Portuguese)
  private static readonly Regex LongDateRegex = new Regex(
    @"\b(\d{1,2})\s+de\s+(janeiro|fevereiro|março|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\s+de\s+(\d{4})\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Date in "DD MON às" format without year, as printed in bank notifications (e.g. "20 SET às 13:18")
  private static readonly Regex ShortMonthDateRegex = new Regex(
    @"\b(\d{1,2})\s+(jan|fev|mar|abr|mai|jun|jul|ago|set|out|nov|dez)\.?\s+às\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Recipient of an outbound transfer: "para <Name> foi" — excludes commas/semicolons to avoid matching footer sentences
  private static readonly Regex RecipientRegex = new Regex(
    @"\bpara\s+([^,;\n]{3,60}?)\s+foi\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Payer of an inbound transfer: "de <Name> foi" or "recebida de <Name>"
  // Excludes commas/semicolons so sentence-break characters stop the match before footer text is captured
  private static readonly Regex PayerRegex = new Regex(
    @"(?:\bde\s+([^,;\n]{3,60}?)\s+foi\b|\brecebid[ao]\s+de\s+([^,;\n]{3,60})(?=\s*[.\n]|$))",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Time in "às HH:mm"
  private static readonly Regex TimeRegex = new Regex(
    @"\bàs\s+(\d{1,2}:\d{2})\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Keywords that indicate the transaction is income (credit to the user's account)
  private static readonly string[] IncomeKeywords =
  {
    "recebeu", "recebido", "recebida", "crédito", "credito",
    "depósito", "deposito", "pix recebido", "ted recebida",
    "transferência recebida", "transferencia recebida",
    "entrada", "reembolso", "estorno", "cashback"
  };

  // Portuguese month names mapped to their month number
  private static readonly Dictionary<string, int> PortugueseMonths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
  {
    { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
    { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
    { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
  };

  // Abbreviated Portuguese month names mapped to their month number
  private static readonly Dictionary<string, int> PortugueseShortMonths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
  {
    { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
    { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
  };

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;
  private readonly FinancialSyncService financialSyncService;
  private readonly ILogger<ImapEmailSyncService> logger;

  /// <summary>
  /// Initializes a new instance of <see cref="ImapEmailSyncService"/>.
  /// </summary>
  /// <param name="contextFactory">Factory used to create isolated DbContext instances.</param>
  /// <param name="financialSyncService">Service used to recompute account balances after new transactions are saved.</param>
  /// <param name="logger">Logger for diagnostic output.</param>
  public ImapEmailSyncService(
    IDbContextFactory<LifePlannerContext> contextFactory,
    FinancialSyncService financialSyncService,
    ILogger<ImapEmailSyncService> logger)
  {
    this.contextFactory = contextFactory;
    this.financialSyncService = financialSyncService;
    this.logger = logger;
  }

  /// <inheritdoc />
  public async Task SyncAllUsersAsync(CancellationToken cancellationToken = default)
  {
    List<ImapSettingEntity> settings = await LoadDueSettingsAsync(cancellationToken);

    foreach (ImapSettingEntity setting in settings)
    {
      try
      {
        await SyncUserAsync(setting, cancellationToken);
      }
      catch (Exception ex)
      {
        logger.LogError(
          ex,
          "IMAP sync failed for user. UserIdHash={UserIdHash}.",
          LogSafe.Hash(setting.OwnerUserId));
      }
    }
  }

  /// <inheritdoc />
  public async Task SyncSingleUserAsync(string ownerUserId, CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(cancellationToken);

    ImapSettingEntity? setting = await db.ImapSettings
      .FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId && !s.Deleted, cancellationToken);

    if (setting is null)
    {
      logger.LogWarning(
        "Manual sync requested for user but no IMAP setting found. UserIdHash={UserIdHash}.",
        LogSafe.Hash(ownerUserId));
      return;
    }

    await SyncUserAsync(setting, cancellationToken);
  }

  // ── Private helpers ─────────────────────────────────────────────────────

  private async Task<List<ImapSettingEntity>> LoadDueSettingsAsync(CancellationToken ct)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(ct);

    DateTime now = DateTime.UtcNow;

    // effectiveUserId is null in background context → query filter returns all rows.
    // Only return settings whose sync interval has elapsed since the last successful sync.
    return await db.ImapSettings
      .Where(s => !s.Deleted && (
        s.LastSyncedAt == null ||
        s.LastSyncedAt.Value.AddHours(s.SyncIntervalHours) <= now))
      .ToListAsync(ct);
  }

  private async Task<List<ImapAccountMappingEntity>> LoadMappingsAsync(string ownerUserId, CancellationToken ct)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(ct);

    return await db.ImapAccountMappings
      .Where(m => m.OwnerUserId == ownerUserId && !m.Deleted)
      .ToListAsync(ct);
  }

  private async Task SyncUserAsync(ImapSettingEntity setting, CancellationToken ct)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    using IDisposable userScope = LogContext.PushProperty("UserIdHash", LogSafe.Hash(setting.OwnerUserId));
    List<ImapAccountMappingEntity> mappings = await LoadMappingsAsync(setting.OwnerUserId, ct);

    if (mappings.Count == 0)
    {
      logger.LogDebug("No IMAP mappings found for user. Skipping.");
      return;
    }

    // ImapPassword is decrypted transparently by the EF Core value converter on read
    using ImapClient client = new ImapClient();
    client.Timeout = 30_000;

    await client.ConnectAsync(setting.ImapHost, setting.ImapPort, SecureSocketOptions.SslOnConnect, ct);
    await client.AuthenticateAsync(setting.ImapEmail, setting.ImapPassword, ct);

    await client.Inbox.OpenAsync(FolderAccess.ReadWrite, ct);

    try
    {
      foreach (ImapAccountMappingEntity mapping in mappings)
      {
        await ProcessMappingAsync(client.Inbox, mapping, setting.OwnerUserId, ct);
      }

      await UpdateLastSyncedAtAsync(setting.Id, ct);

      logger.LogInformation(
        "IMAP sync completed for user. Status={Status}. Mappings={Mappings}. ElapsedMs={ElapsedMs}.",
        "Success",
        mappings.Count,
        stopwatch.ElapsedMilliseconds);
    }
    finally
    {
      await client.DisconnectAsync(quit: true, ct);
    }
  }

  private async Task UpdateLastSyncedAtAsync(int settingId, CancellationToken ct)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(ct);

    ImapSettingEntity? setting = await db.ImapSettings
      .FirstOrDefaultAsync(s => s.Id == settingId, ct);

    if (setting is not null)
    {
      setting.LastSyncedAt = DateTime.UtcNow;
      await db.SaveChangesAsync(ct);
    }
  }

  private async Task ProcessMappingAsync(
    IMailFolder inbox,
    ImapAccountMappingEntity mapping,
    string ownerUserId,
    CancellationToken ct)
  {
    SearchQuery query = SearchQuery.And(
      SearchQuery.NotSeen,
      SearchQuery.FromContains(mapping.SenderIdentifier));

    IList<UniqueId> uids = await inbox.SearchAsync(query, ct);

    if (uids.Count == 0)
    {
      return;
    }

    logger.LogInformation(
      "Found {Count} unread email(s) from sender hash {SenderHash}.",
      uids.Count,
      LogSafe.Hash(mapping.SenderIdentifier));

    string accountName = await LoadAccountNameAsync(mapping.AccountId, ct);

    foreach (UniqueId uid in uids)
    {
      try
      {
        MimeMessage message = await inbox.GetMessageAsync(uid, ct);
        await ProcessEmailAsync(message, uid, inbox, mapping, ownerUserId, accountName, ct);
      }
      catch (Exception ex)
      {
        logger.LogError(
          ex,
          "Failed to process email UID {Uid} from sender hash {SenderHash}.",
          uid,
          LogSafe.Hash(mapping.SenderIdentifier));
      }
    }
  }

  private async Task<string> LoadAccountNameAsync(int accountId, CancellationToken ct)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(ct);

    AccountEntity? account = await db.Accounts
      .FirstOrDefaultAsync(a => a.Id == accountId && !a.Deleted, ct);

    return account?.Name ?? string.Empty;
  }

  private async Task ProcessEmailAsync(
    MimeMessage message,
    UniqueId uid,
    IMailFolder inbox,
    ImapAccountMappingEntity mapping,
    string ownerUserId,
    string accountName,
    CancellationToken ct)
  {
    string subject = message.Subject ?? string.Empty;
    string body = message.TextBody ?? StripHtmlTags(message.HtmlBody ?? string.Empty);
    DateTime receivedDate = message.Date.LocalDateTime;

    ParsedTransaction? parsed = TryParseTransaction(subject, body, receivedDate, accountName);

    if (parsed is not null)
    {
      await SaveTransactionAsync(parsed, mapping.AccountId, ownerUserId, ct);

      logger.LogInformation(
        "Transaction saved from IMAP email. TransactionType={TransactionType}. TransactionDate={TransactionDate}.",
        parsed.IsIncome ? "income" : "expense",
        parsed.Date);
    }
    else
    {
      logger.LogDebug(
        "Email UID {Uid} contains no parseable transaction. Marking as read.",
        uid);
    }

    // Mark as read after successful processing (transaction saved or confirmed non-financial)
    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, ct);
  }

  private async Task SaveTransactionAsync(
    ParsedTransaction parsed,
    int accountId,
    string ownerUserId,
    CancellationToken ct)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync(ct);

    TransactionEntity transaction = new TransactionEntity
    {
      Date = parsed.Date,
      Value = parsed.Amount,
      Description = parsed.Description,
      Income = parsed.IsIncome,
      AccountId = accountId,
      Category = CategoryMapper.GetBankStatementCategory(parsed.Description, parsed.IsIncome),
      SourceKind = TransactionSourceKind.Email,
      TransactionTime = parsed.Time,
      SourceDetails = parsed.Details,
    };

    db.Transactions.Add(transaction);

    // effectiveUserId is null in background context; stamp OwnerUserId manually
    db.Entry(transaction).Property("OwnerUserId").CurrentValue = ownerUserId;

    await db.SaveChangesAsync(ct);

	await financialSyncService.SyncAccountAsync(accountId, ct);
	}

	// ── Transaction parsing ──────────────────────────────────────────────────

  private static ParsedTransaction? TryParseTransaction(
    string subject,
    string body,
    DateTime fallbackDate,
    string accountName)
  {
    string searchText = $"{subject} {body}";

    Match amountMatch = AmountRegex.Match(searchText);
    if (!amountMatch.Success)
    {
      return null;
    }

    decimal amount = ParseBrlAmount(amountMatch.Groups[1].Value);
    if (amount <= 0m)
    {
      return null;
    }

    bool isIncome = DetermineIsIncome(searchText);
    DateTime date = ExtractDate(searchText, fallbackDate) ?? fallbackDate.Date;
    string? party = ExtractParty(body, isIncome);
    string? time = ExtractTime(body);
    string description = BuildDescription(subject, body, amount, isIncome, party, time, accountName);

    return new ParsedTransaction(date, amount, description, isIncome, time?.PadLeft(5, '0'), BuildSourceDetails(subject, body));
  }

  /// <summary>
  /// Builds the source details kept with the transaction: the subject and the opening of the notification body,
  /// so the original wording is still available after the description is rewritten.
  /// </summary>
  private static string BuildSourceDetails(string subject, string body)
  {
    string text = Regex.Replace($"{subject} {body}", @"\s+", " ").Trim();
    return text.Length > 300 ? text[..300] : text;
  }

  private static decimal ParseBrlAmount(string raw)
  {
    // pt-BR format: thousands separator = '.', decimal separator = ','
    // e.g. "1.234,56" → remove '.' → "1234,56" → replace ',' with '.' → "1234.56"
    string normalized = raw.Replace(".", string.Empty).Replace(",", ".");
    return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)
      ? d
      : 0m;
  }

  private static bool DetermineIsIncome(string text)
  {
    string lower = text.ToLowerInvariant();
    foreach (string keyword in IncomeKeywords)
    {
      // Both sides are already lowercase; ordinal comparison is correct and fastest.
      if (lower.Contains(keyword, StringComparison.Ordinal))
      {
        return true;
      }
    }
    return false;
  }

  private static DateTime? ExtractDate(string text, DateTime referenceDate)
  {
    Match shortMatch = ShortDateRegex.Match(text);
    if (shortMatch.Success &&
        DateTime.TryParseExact(
          shortMatch.Groups[1].Value,
          "dd/MM/yyyy",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out DateTime d1))
    {
      return d1;
    }

    Match longMatch = LongDateRegex.Match(text);
    if (longMatch.Success &&
        PortugueseMonths.TryGetValue(longMatch.Groups[2].Value, out int month))
    {
      if (int.TryParse(longMatch.Groups[1].Value, out int day) &&
          int.TryParse(longMatch.Groups[3].Value, out int year))
      {
        return new DateTime(year, month, day);
      }
    }

    // "DD MON às HH:mm" carries no year: use the reference year, or the previous one when that would
    // land in the future (a notification received just after New Year describing 31 DEZ).
    Match shortMonthMatch = ShortMonthDateRegex.Match(text);
    if (shortMonthMatch.Success
      && PortugueseShortMonths.TryGetValue(shortMonthMatch.Groups[2].Value, out int shortMonth)
      && int.TryParse(shortMonthMatch.Groups[1].Value, out int shortDay)
      && shortDay >= 1
      && shortDay <= DateTime.DaysInMonth(referenceDate.Year, shortMonth))
    {
      DateTime resolved = new DateTime(referenceDate.Year, shortMonth, shortDay);
      return resolved > referenceDate.Date.AddDays(1) ? resolved.AddYears(-1) : resolved;
    }

    return null;
  }

  /// <summary>
  /// Extracts the counterpart name (recipient for expenses, payer for income) from the email body.
  /// </summary>
  private static string? ExtractParty(string body, bool isIncome)
  {
    if (isIncome)
    {
      Match m = PayerRegex.Match(body);
      if (m.Success)
      {
        // PayerRegex has two capture groups; use whichever one matched.
        string raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        return CleanPartyName(raw);
      }
      return null;
    }

    Match recipientMatch = RecipientRegex.Match(body);
    return recipientMatch.Success ? CleanPartyName(recipientMatch.Groups[1].Value) : null;
  }

  /// <summary>
  /// Trims a party name captured from the notification. Ownership wording such as "sua conta no BCO C6 S.A."
  /// is kept on purpose: it is the only sign in the e-mail that the transfer went to the user's own account.
  /// </summary>
  private static string CleanPartyName(string raw)
  {
    return raw.Trim();
  }

  /// <summary>
  /// Extracts the transaction time (HH:mm) from an "às HH:mm" pattern in the email body.
  /// </summary>
  private static string? ExtractTime(string body)
  {
    Match m = TimeRegex.Match(body);
    return m.Success ? m.Groups[1].Value : null;
  }

  /// <summary>
  /// Builds an enriched description from parsed email fields.
  /// Uses extracted party name and time when available; falls back to the email subject.
  /// Appends the originating account name as a suffix when provided.
  /// </summary>
  private static string BuildDescription(
    string subject,
    string body,
    decimal amount,
    bool isIncome,
    string? party,
    string? time,
    string accountName)
  {
    bool isPix = body.Contains("pix", StringComparison.OrdinalIgnoreCase)
              || subject.Contains("pix", StringComparison.OrdinalIgnoreCase);

    string label = isPix
      ? (isIncome ? "PIX recebido" : "PIX enviado")
      : (isIncome ? "Transferência recebida" : "Transferência enviada");

    string accountSuffix = !string.IsNullOrWhiteSpace(accountName)
      ? $" · {accountName}"
      : string.Empty;

    if (!string.IsNullOrWhiteSpace(party) || !string.IsNullOrWhiteSpace(time))
    {
      string direction = isIncome ? "de" : "para";
      string partyPart = !string.IsNullOrWhiteSpace(party) ? $" {direction} {party}" : string.Empty;
      string timePart = !string.IsNullOrWhiteSpace(time) ? $" às {time}" : string.Empty;
      return $"{label}{partyPart}{timePart}{accountSuffix}";
    }

    // Fallback: use subject when no enrichment was possible
    if (!string.IsNullOrWhiteSpace(subject))
    {
      string trimmed = subject.Trim();
      string capped = trimmed.Length > 200 ? trimmed[..200] : trimmed;
      return $"{capped}{accountSuffix}";
    }

    string fallback = isIncome
      ? $"Receita via email: R$ {amount:N2}"
      : $"Despesa via email: R$ {amount:N2}";

    return $"{fallback}{accountSuffix}";
  }

  private static string StripHtmlTags(string html)
  {
    return Regex.Replace(html, "<[^>]+>", " ");
  }

  // ── Inner record ─────────────────────────────────────────────────────────

  private sealed record ParsedTransaction(
    DateTime Date,
    decimal Amount,
    string Description,
    bool IsIncome,
    string? Time,
    string Details);
}
