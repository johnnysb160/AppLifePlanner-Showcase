//-----------------------------------------------------------------------
// <copyright file="TransactionImportService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;
using System.Globalization;
using System.Text.RegularExpressions;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Helpers;

/// <summary>
/// Service responsible for parsing, validating, and persisting imported transactions.
/// </summary>
public sealed class TransactionImportService
{
  // Description shape written by the e-mail sync: "<label> [para|de <party>] [às HH:mm] · <account>".
  private static readonly Regex EmailNotificationRegex = new Regex(
    @"^(PIX|Transfer[eê]ncia) (enviad|recebid)[ao]\b.*\s·\s",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

  // Time of day embedded in an e-mail notification description ("às 13:18").
  private static readonly Regex EmailTimeRegex = new Regex(
    @"\bàs\s+(\d{1,2}:\d{2})\b",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

  private readonly ITransactionRepository transactionRepository;
  private readonly IAccountRepository accountRepository;
  private readonly FinancialSyncService financialSyncService;
  private readonly BankStatementCsvParser bankStatementParser;
  private readonly CardInvoiceNubankCsvParser cardInvoiceParser;

  /// <summary>
  /// Initializes a new instance of the TransactionImportService class with the specified Transaction and account
  /// repositories.
  /// </summary>
  /// <param name="transactionRepository">The repository used to access and manage Transaction data. Cannot be null.</param>
  /// <param name="accountRepository">The repository used to access and manage account data. Cannot be null.</param>
  public TransactionImportService(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    FinancialSyncService financialSyncService)
  {
    this.transactionRepository = transactionRepository;
    this.accountRepository = accountRepository;
    this.financialSyncService = financialSyncService;
    this.bankStatementParser = new BankStatementCsvParser();
    this.cardInvoiceParser = new CardInvoiceNubankCsvParser();
  }

  /// <summary>
  /// Executes the import operation.
  /// </summary>
  public async Task<ImportResult> ImportAsync(ImportRequest request,
                                              IProgress<ImportProgressInfo>? progress = null,
                                              CancellationToken cancellationToken = default)
  {
    ImportResult result = new ImportResult();

    try
    {
      progress?.Report(new ImportProgressInfo { Percent = 0, Message = "Parsing file..." });

      List<ImportCandidate> candidates = await ParseFileAsync(
        request,
        new StageProgress(progress, "Parsing file...", 0, 40),
        cancellationToken);

      progress?.Report(new ImportProgressInfo { Percent = 40, Message = "Validating duplicates..." });

      await ValidateCandidatesAsync(
        candidates,
        request.AccountId,
        request.SourceType == ImportSourceType.BankStatement,
        new StageProgress(progress, "Validating duplicates...", 40, 70),
        cancellationToken);

      progress?.Report(new ImportProgressInfo { Percent = 70, Message = "Importing..." });

      await InsertCandidatesAsync(candidates, result, request, progress, cancellationToken);

      if (result.Inserted > 0)
      {
        await this.financialSyncService.SyncAccountAsync(request.AccountId, cancellationToken);
      }

      progress?.Report(new ImportProgressInfo { Percent = 100, Message = "Import completed" });

      result.TotalParsed = candidates.Count;

      if (candidates.Count == 0)
      {
        result.ErrorMessage = "No valid records found in file";
        return result;
      }

      // Attach account metadata to result for UI currency formatting
      result.AccountId = request.AccountId;

      // Determine account currency if available
      try
      {
        AccountEntity? acct = await this.accountRepository.SelectAsync(request.AccountId);
        if (acct != null)
        {
          result.AccountCurrency = acct.Currency;
        }
      }
      catch { }
    }
    catch (OperationCanceledException)
    {
      result.ErrorMessage = "Import was cancelled";
    }
    catch (Exception ex)
    {
      result.ErrorMessage = string.Format("Import failed: {0}", ex.Message);
      result.Errors++;
    }

    return result;
  }

  /// <summary>
  /// Inserts a list of pending items that were reviewed and accepted by user.
  /// </summary>
  public async Task<ImportResult> InsertPendingItemsAsync(List<PendingItem> items,
                                                          IProgress<int>? progress = null,
                                                          CancellationToken cancellationToken = default)
  {
    ImportResult result = new ImportResult();
    result.TotalParsed = items.Count;

    int processed = 0;
    int total = items.Count;

    foreach (PendingItem item in items)
    {
      cancellationToken.ThrowIfCancellationRequested();

      // Re-validate for exact duplicates before insert (account-scoped)
      bool isExactDuplicate = await IsExactDuplicateAsync(item.ComputedExternalId,
                                                          item.Date,
                                                          item.Value,
                                                          item.Income,
                                                          item.Description,
                                                          item.AccountId,
                                                          cancellationToken);

      if (isExactDuplicate)
      {
        result.SkippedDuplicates++;
      }
      else
      {
        try
        {
          TransactionEntity transaction = new TransactionEntity
          {
            Date = item.Date,
            Category = item.Category,
            Value = item.Value,
            Description = item.Description,
            Income = item.Income,
            ExternalId = item.ComputedExternalId,
            AccountId = item.AccountId,
            CreditCardId = item.CreditCardId,
            SourceKind = item.SourceKind,
            TransactionTime = item.TransactionTime,
            SourceDetails = item.SourceDetails,
          };

          await this.transactionRepository.InsertAsync(transaction);
          result.Inserted++;
        }
        catch (Exception)
        {
          result.Errors++;
        }
      }

      processed++;
      if (progress != null && total > 0)
      {
        int percent = (int)((processed * 100.0) / total);
        progress.Report(percent);
      }
    }

    foreach (int accountId in items
      .Where(item => item.AccountId.HasValue)
      .Select(item => item.AccountId!.Value)
      .Distinct())
    {
      await this.financialSyncService.SyncAccountAsync(accountId, cancellationToken);
    }

    return result;
  }

  /// <summary>
  /// Parses the provided import file asynchronously and extracts a list of import candidates based on the specified
  /// import request.
  /// </summary>
  /// <remarks>The parsing behavior depends on the source type and file format specified in the request. For
  /// bank statements, the method supports both OFX and other formats. For card invoices, additional credit card
  /// information from the request is used. The method does not modify the input stream position after
  /// parsing.</remarks>
  /// <param name="request">An object containing information about the import source, file format, file stream, and related metadata
  /// required for parsing.</param>
  /// <param name="progress">An optional progress reporter that receives updates indicating the percentage of the file parsing operation
  /// completed.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous parsing operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a list of import candidates
  /// extracted from the file. The list will be empty if no candidates are found.</returns>
  private async Task<List<ImportCandidate>> ParseFileAsync(ImportRequest request,
                                                            IProgress<int>? progress,
                                                            CancellationToken cancellationToken)
  {
    if (request.SourceType == ImportSourceType.BankStatement)
    {
      if (request.FileFormat == ImportFileFormat.Ofx)
      {
        return await this.bankStatementParser.ParseOfxAsync(request.FileStream, progress, cancellationToken);
      }
      else
      {
        return await this.bankStatementParser.ParseAsync(request.FileStream, progress, cancellationToken);
      }
    }
    else
    {
      // Card invoice - uses purchaseDate from CSV, no effectiveDate needed
      return await this.cardInvoiceParser.ParseAsync(request.FileStream,
                                                      request.CreditCardId ?? 0,
                                                      request.CreditCardLastFour ?? string.Empty,
                                                      progress,
                                                      cancellationToken);
    }
  }

  /// <summary>
  /// Validates a list of import candidates by checking for potential duplicates based on external identifiers and
  /// transaction details.
  /// </summary>
  /// <remarks>This method updates the PendingReason and PendingMessage properties of each candidate to
  /// indicate possible duplicates. Duplicate detection is based on matching external IDs or matching transaction
  /// details such as date, value, income, and description. The method reports progress if a progress reporter is
  /// provided and throws an OperationCanceledException if cancellation is requested.</remarks>
  /// <param name="candidates">The list of import candidates to validate for possible duplicates. Candidates with a non-None pending reason are
  /// ignored.</param>
  /// <param name="progress">An optional progress reporter that receives the percentage of candidates processed. If null, progress is not
  /// reported.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the validation operation.</param>
  /// <returns>A task that represents the asynchronous validation operation.</returns>
  private async Task ValidateCandidatesAsync(List<ImportCandidate> candidates,
                                              int accountId,
                                              bool allowEnrichment,
                                              IProgress<int>? progress,
                                              CancellationToken cancellationToken)
  {
    List<ImportCandidate> validCandidates = candidates.Where(c => c.PendingReason == PendingReason.None).ToList();

    if (validCandidates.Count == 0)
    {
      return;
    }

    DateTime minDate = validCandidates.Min(c => c.Date).AddDays(-1);
    DateTime maxDate = validCandidates.Max(c => c.Date).AddDays(1);

    List<int> externalIds = validCandidates
      .Where(c => c.ComputedExternalId.HasValue)
      .Select(c => c.ComputedExternalId!.Value)
      .Distinct()
      .ToList();

    List<TransactionEntity>? allTransactionsOrNull = await this.transactionRepository.SelectAsync(CancellationToken.None);
    List<TransactionEntity> allTransactions = allTransactionsOrNull ?? new List<TransactionEntity>();

    // Scope duplicate detection to the same account when known.
    // Cross-account checks produce false positives: e.g. a transfer of R$500 appears in both
    // the source and destination account on the same day, but they are distinct transactions.
    IEnumerable<TransactionEntity> scopedTransactions = accountId > 0
      ? allTransactions.Where(a => a.AccountId == accountId)
      : allTransactions;

    List<TransactionEntity> existingByExternalId = scopedTransactions
      .Where(a => a.ExternalId.HasValue && externalIds.Contains(a.ExternalId.Value))
      .ToList();

    List<TransactionEntity> existingByDateRange = scopedTransactions
      .Where(a => a.Date >= minDate && a.Date <= maxDate)
      .ToList();

    HashSet<int> exactExternalIdMatches = existingByExternalId.Select(a => a.ExternalId!.Value).ToHashSet();

    // A candidate may only enrich an existing row when the file holds no other candidate for the same
    // day, amount and type: with several look-alikes there is no safe one-to-one pairing.
    HashSet<(DateTime Date, decimal Value, bool Income)> ambiguousSlots = validCandidates
      .GroupBy(c => (c.Date.Date, c.Value, c.Income))
      .Where(group => group.Count() > 1)
      .Select(group => group.Key)
      .ToHashSet();
    HashSet<int> enrichedTransactionIds = new HashSet<int>();

    int processed = 0;
    int total = validCandidates.Count;

    foreach (ImportCandidate candidate in validCandidates)
    {
      cancellationToken.ThrowIfCancellationRequested();

      ApplyDuplicateFlags(
        candidate,
        exactExternalIdMatches,
        existingByDateRange,
        allowEnrichment,
        ambiguousSlots,
        enrichedTransactionIds);

      processed++;
      if (progress != null && total > 0)
      {
        int percent = (int)((processed * 100.0) / total);
        progress.Report(percent);
      }
    }
  }

  /// <summary>
  /// Evaluates a candidate against known existing transactions and marks it as a possible
  /// duplicate when a match is found. Three checks are applied in priority order:
  /// exact ExternalId match, exact content match, and uncertain content match.
  /// </summary>
  /// <param name="candidate">The import candidate to evaluate.</param>
  /// <param name="exactExternalIdMatches">Set of ExternalId values already present in the target account.</param>
  /// <param name="existingByDateRange">Transactions already stored within the candidate's date window, scoped to the account.</param>
  /// <param name="allowEnrichment">Whether an uncertain match may enrich the existing row instead of going to review.</param>
  /// <param name="ambiguousSlots">Day, amount and type combinations that appear more than once in the file.</param>
  /// <param name="enrichedTransactionIds">Existing rows already claimed by another candidate during this validation.</param>
  private static void ApplyDuplicateFlags(
    ImportCandidate candidate,
    HashSet<int> exactExternalIdMatches,
    List<TransactionEntity> existingByDateRange,
    bool allowEnrichment,
    HashSet<(DateTime Date, decimal Value, bool Income)> ambiguousSlots,
    HashSet<int> enrichedTransactionIds)
  {
    // Check 1: ExternalId collision — definite duplicate, auto-skip.
    if (candidate.ComputedExternalId.HasValue
      && exactExternalIdMatches.Contains(candidate.ComputedExternalId.Value))
    {
      candidate.PendingReason = PendingReason.PossibleDuplicate;
      candidate.PendingMessage = "Exact duplicate (ExternalId already exists in this account)";
      return;
    }

    string normalizedCandidateDesc = candidate.Description;

    // Check 2: Same date + amount + type + description — probable duplicate.
    // Normalises both sides so minor whitespace/case differences do not cause false negatives.
    bool exactContentMatch = existingByDateRange.Any(a =>
      a.Date.Date == candidate.Date.Date
      && a.Value == candidate.Value
      && a.Income == candidate.Income
      && string.Equals(
        TransactionImportHasher.NormalizeDescription(a.Description),
        normalizedCandidateDesc,
        StringComparison.OrdinalIgnoreCase));

    if (exactContentMatch)
    {
      candidate.PendingReason = PendingReason.PossibleDuplicate;
      candidate.PendingMessage = "Probable duplicate (same date, amount, type and description in this account)";
      return;
    }

    // Check 3: Same date + amount + type but different description — uncertain.
    // Typical cause: IMAP saved the bank's notification text while the statement uses a
    // different internal description for the same operation. Send to review so the user decides.
    List<TransactionEntity> sameSlotMatches = existingByDateRange
      .Where(a => a.Date.Date == candidate.Date.Date
        && a.Value == candidate.Value
        && a.Income == candidate.Income
        && !string.Equals(
          TransactionImportHasher.NormalizeDescription(a.Description),
          normalizedCandidateDesc,
          StringComparison.OrdinalIgnoreCase))
      .ToList();

    if (sameSlotMatches.Count == 0)
    {
      return;
    }

    // The statement describes the same operation as an e-mail notification that was never edited:
    // keep a single row and complete it with the richer statement text instead of asking for review.
    if (allowEnrichment
      && sameSlotMatches.Count == 1
      && !ambiguousSlots.Contains((candidate.Date.Date, candidate.Value, candidate.Income))
      && IsEnrichable(sameSlotMatches[0], candidate)
      && enrichedTransactionIds.Add(sameSlotMatches[0].Id))
    {
      candidate.EnrichTransactionId = sameSlotMatches[0].Id;
      return;
    }

    candidate.PendingReason = PendingReason.PossibleDuplicate;
    candidate.PendingMessage = "Possible duplicate (same date/amount/type in this account — descriptions differ, possibly imported from IMAP or entered manually)";
  }

  /// <summary>
  /// Determines whether an existing transaction is a weaker copy of the candidate that can be completed in place.
  /// Only e-mail notifications that were never edited and never imported from a file qualify: an edit or a file
  /// import always stamps an <c>ExternalId</c>, so a null value proves nobody has refined the row.
  /// </summary>
  /// <param name="existing">The existing transaction that matches the candidate on day, amount and type.</param>
  /// <param name="candidate">The import candidate carrying the fuller description.</param>
  /// <returns><see langword="true"/> when the existing row can safely be enriched with the candidate.</returns>
  private static bool IsEnrichable(TransactionEntity existing, ImportCandidate candidate)
  {
    if (existing.Deleted || existing.ExternalId.HasValue)
    {
      return false;
    }

    bool isEmailDerived = existing.SourceKind == TransactionSourceKind.Email
      || (existing.SourceKind == TransactionSourceKind.Legacy && EmailNotificationRegex.IsMatch(existing.Description));

    return isEmailDerived
      && candidate.Description.Length > existing.Description.Length;
  }

  /// <summary>
  /// Asynchronously inserts a collection of import candidates into the transaction repository, reporting progress and
  /// updating the import result with the outcome of each candidate.
  /// </summary>
  /// <remarks>Candidates that cannot be inserted due to a pending reason or an error are added to the pending
  /// list in the result. Progress is reported throughout the operation, and the method periodically yields control to
  /// keep the UI responsive during large imports.</remarks>
  /// <param name="candidates">The list of import candidates to be processed and inserted. Each candidate represents a potential transaction to be
  /// imported.</param>
  /// <param name="result">An object that accumulates the results of the import operation, including the number of successfully inserted
  /// transactions and any errors or pending candidates.</param>
  /// <param name="progress">An optional progress reporter that receives updates about the current progress of the import operation. May be null
  /// if progress reporting is not required.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation before completion.</param>
  private async Task InsertCandidatesAsync(
    List<ImportCandidate> candidates,
    ImportResult result,
    ImportRequest request,
    IProgress<ImportProgressInfo>? progress,
    CancellationToken cancellationToken)
  {
    int processed = 0;
    int total = candidates.Count;

    Currency? currency = result.AccountCurrency;

    foreach (ImportCandidate candidate in candidates)
    {
      cancellationToken.ThrowIfCancellationRequested();
      processed++;

      int raw = total > 0 ? (int)((processed * 100.0) / total) : 100;
      int mapped = 70 + ((Math.Clamp(raw, 0, 100) * 30) / 100);

      string itemText = BuildImportingText(candidate, currency);

      progress?.Report(new ImportProgressInfo
      {
        Percent = mapped,
        Message = string.Format("Importing: {0}", itemText)
      });

      if (candidate.EnrichTransactionId.HasValue)
      {
        await EnrichExistingAsync(candidate, result, request);
      }
      else if (candidate.PendingReason != PendingReason.None)
      {
        AddPending(result, candidate, request);
      }
      else
      {
        try
        {
          TransactionEntity transaction = new TransactionEntity
          {
            Date = candidate.Date,
            Category = candidate.Category,
            Value = candidate.Value,
            Description = candidate.Description,
            Income = candidate.Income,
            ExternalId = candidate.ComputedExternalId,
            AccountId = request.AccountId,
            CreditCardId = request.CreditCardId,
            SourceKind = ResolveSourceKind(request.SourceType),
            TransactionTime = candidate.TransactionTime,
            SourceDetails = candidate.SourceDetails,
          };

          await this.transactionRepository.InsertAsync(transaction);
          result.Inserted++;
        }
        catch (Exception ex)
        {
          candidate.PendingReason = PendingReason.InsertError;
          candidate.PendingMessage = ex.Message;

          AddPending(result, candidate, request);
          result.Errors++;
        }
      }

      await Task.Yield();
    }
  }

  /// <summary>
  /// Completes an existing e-mail transaction with the fuller text of a statement candidate instead of inserting
  /// a second row. This path never adds a transaction: any doubt sends the candidate to manual review.
  /// </summary>
  /// <param name="candidate">The statement candidate paired with an existing transaction.</param>
  /// <param name="result">The import result that accumulates outcomes.</param>
  /// <param name="request">The import request that produced the candidate.</param>
  /// <returns>A task that completes when the existing row was updated or the candidate was sent to review.</returns>
  private async Task EnrichExistingAsync(ImportCandidate candidate, ImportResult result, ImportRequest request)
  {
    try
    {
      TransactionEntity? existing = await this.transactionRepository.SelectAsync(candidate.EnrichTransactionId!.Value);

      // Re-check on the fresh row: it may have been edited or removed since validation.
      if (existing == null || !IsEnrichable(existing, candidate))
      {
        candidate.EnrichTransactionId = null;
        candidate.PendingReason = PendingReason.PossibleDuplicate;
        candidate.PendingMessage = "Possible duplicate (same date/amount/type in this account — descriptions differ, possibly imported from IMAP or entered manually)";
        AddPending(result, candidate, request);
        return;
      }

      // The e-mail is the only source of the time of day: keep it before the description is replaced.
      Match timeMatch = EmailTimeRegex.Match(existing.Description);
      if (string.IsNullOrWhiteSpace(existing.TransactionTime) && timeMatch.Success)
      {
        existing.TransactionTime = timeMatch.Groups[1].Value.PadLeft(5, '0');
      }

      // Replace only the generic fallback category; a specific one is either the user's or already right.
      Category fallbackCategory = existing.Income ? Category.OthersIncome : Category.ExtraExpenses;
      if (existing.Category == fallbackCategory && candidate.Category != fallbackCategory)
      {
        existing.Category = candidate.Category;
      }

      existing.Description = candidate.Description;
      existing.SourceDetails = candidate.SourceDetails ?? existing.SourceDetails;

      // Stamping the statement identifier makes the next import of this line an exact duplicate.
      existing.ExternalId = candidate.ComputedExternalId;

      await this.transactionRepository.UpdateAsync(existing);
      result.Enriched++;
    }
    catch (Exception ex)
    {
      candidate.EnrichTransactionId = null;
      candidate.PendingReason = PendingReason.InsertError;
      candidate.PendingMessage = ex.Message;

      AddPending(result, candidate, request);
      result.Errors++;
    }
  }

  /// <summary>
  /// Adds a new pending item to the specified import result using the details from the provided candidate.
  /// </summary>
  /// <remarks>After adding the pending item, the method increments the pending count in the import result. Ensure
  /// that both parameters are valid before calling this method.</remarks>
  /// <param name="result">The import result to which the pending item will be added. This parameter must not be null.</param>
  /// <param name="candidate">The candidate containing the information for the pending item, including date, category, value, description,
  /// income, computed external ID, pending reason, and pending message. This parameter must not be null.</param>
  private static void AddPending(ImportResult result, ImportCandidate candidate, ImportRequest request)
  {
    result.PendingItems.Add(new PendingItem
    {
      Date = candidate.Date,
      Category = candidate.Category,
      Value = candidate.Value,
      Description = candidate.Description,
      Income = candidate.Income,
      ComputedExternalId = candidate.ComputedExternalId,
      AccountId = request.AccountId,
      CreditCardId = request.CreditCardId,
      SourceKind = ResolveSourceKind(request.SourceType),
      TransactionTime = candidate.TransactionTime,
      SourceDetails = candidate.SourceDetails,
      Reason = candidate.PendingReason,
      ReasonText = candidate.PendingMessage
    });

    result.PendingCount++;
  }

  private static TransactionSourceKind ResolveSourceKind(ImportSourceType sourceType)
  {
    return sourceType == ImportSourceType.CardInvoice
      ? TransactionSourceKind.CardInvoice
      : TransactionSourceKind.BankStatement;
  }

  /// <summary>
  /// Builds a formatted string that summarizes the import candidate's description, value, and date using the specified
  /// currency format.
  /// </summary>
  /// <remarks>The description is truncated to a maximum of 40 characters. The value is always displayed as a
  /// positive amount. The currency format is determined by the specified currency, using either US or Brazilian
  /// conventions.</remarks>
  /// <param name="c">The import candidate containing the description, value, and date to be formatted.</param>
  /// <param name="currency">The currency to use when formatting the value. Determines the culture-specific currency symbol and formatting.</param>
  /// <returns>A string that includes the truncated description, the absolute value formatted as currency, and the date of the
  /// import candidate.</returns>
  private static string BuildImportingText(ImportCandidate c, Currency? currency)
  {
    string desc = (c.Description ?? string.Empty).Trim();
    if (desc.Length > 40)
    {
      desc = desc.Substring(0, 40) + "...";
    }

    decimal value = Math.Abs(c.Value);

    string amount = currency == Currency.USD
      ? value.ToString("C", CultureInfo.GetCultureInfo("en-US"))
      : value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    string date = c.Date.ToString("dd/MM/yyyy");
    return string.Format("{0} {1} {2}", desc, amount, date);
  }

  /// <summary>
  /// Determines whether an transaction with the specified details is an exact duplicate of an existing transaction.
  /// </summary>
  /// <remarks>An transaction is considered an exact duplicate if it matches an existing, non-deleted
  /// transaction by external ID (if provided), or by date, value, income status, and normalized
  /// description.</remarks>
  /// <param name="externalId">The external identifier of the transaction to check for duplication. If specified, the method checks for an
  /// existing transaction with the same external ID.</param>
  /// <param name="date">The date of the transaction to check for duplication. Only the date component is considered.</param>
  /// <param name="value">The monetary value associated with the transaction to check for duplication.</param>
  /// <param name="income">A value indicating whether the transaction is an income entry. Set to <see langword="true"/> for income;
  /// otherwise, <see langword="false"/>.</param>
  /// <param name="description">The description of the transaction to check for duplication. The description is normalized before comparison.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if an exact
  /// duplicate exists; otherwise, <see langword="false"/>.</returns>
  private async Task<bool> IsExactDuplicateAsync(int? externalId,
                                                  DateTime date,
                                                  decimal value,
                                                  bool income,
                                                  string description,
                                                  int? accountId,
                                                  CancellationToken cancellationToken)
  {
    List<TransactionEntity>? allOrNull = await this.transactionRepository.SelectAsync(cancellationToken);
    IEnumerable<TransactionEntity> all = (allOrNull ?? new List<TransactionEntity>())
      .Where(a => !a.Deleted);

    // Scope to same account to avoid false positives between accounts
    if (accountId.HasValue && accountId.Value > 0)
    {
      all = all.Where(a => a.AccountId == accountId.Value);
    }

    List<TransactionEntity> scoped = all.ToList();

    if (externalId.HasValue && scoped.Any(a => a.ExternalId == externalId.Value))
    {
      return true;
    }

    string normalizedDesc = TransactionImportHasher.NormalizeDescription(description);
    return scoped.Any(a =>
      a.Date.Date == date.Date
      && a.Value == value
      && a.Income == income
      && string.Equals(
        TransactionImportHasher.NormalizeDescription(a.Description),
        normalizedDesc,
        StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// Helper class for progress reporting with offset ranges.
  /// </summary>
  private sealed class StageProgress : IProgress<int>
  {
    private readonly IProgress<ImportProgressInfo>? inner;
    private readonly string message;
    private readonly int start;
    private readonly int end;

    /// <summary>
    /// Initializes a new instance of the StageProgress class to track the progress of an import operation within a
    /// specified range.
    /// </summary>
    /// <remarks>The start and end parameters define the inclusive range of progress values for this stage. Ensure
    /// that start is less than or equal to end to avoid unexpected behavior when reporting progress.</remarks>
    /// <param name="inner">An optional progress reporter that receives updates about the import operation's progress. May be null if progress
    /// reporting is not required.</param>
    /// <param name="message">A message that describes the current stage of the import process. This message is used to provide context for
    /// progress updates.</param>
    /// <param name="start">The starting value of the progress range represented by this stage. Must be less than or equal to end.</param>
    /// <param name="end">The ending value of the progress range represented by this stage. Must be greater than or equal to start.</param>
    public StageProgress(IProgress<ImportProgressInfo>? inner, string message, int start, int end)
    {
      this.inner = inner;
      this.message = message;
      this.start = start;
      this.end = end;
    }

    /// <summary>
    /// Reports the progress of an import operation by mapping a percentage value to a specific range.
    /// </summary>
    /// <remarks>This method adjusts the input value to fit within the specified range defined by the start and
    /// end values, and then reports the progress using an internal reporting mechanism.</remarks>
    /// <param name="value">The percentage of progress, clamped between 0 and 100, that indicates the completion status of the import
    /// operation.</param>
    public void Report(int value)
    {
      if (this.inner == null)
      {
        return;
      }

      value = Math.Clamp(value, 0, 100);
      int mapped = this.start + ((value * (this.end - this.start)) / 100);
      mapped = Math.Clamp(mapped, this.start, this.end);

      this.inner.Report(new ImportProgressInfo { Percent = mapped, Message = this.message });
    }
  }
}
