//-----------------------------------------------------------------------
// <copyright file="ImportTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Imports;

using System.Text;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Shared.Models;
using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services;
using Xunit;

/// <summary>
/// Tests import parsing, sign normalization, and duplicate review behavior.
/// </summary>
public sealed class ImportTests
{
  /// <summary>
  /// Bank statements normalize signs into value plus income direction.
  /// </summary>
  [Fact]
  public async Task BankStatementParserNormalizesSignedValues()
  {
    BankStatementCsvParser parser = new BankStatementCsvParser();
    await using MemoryStream stream = CreateStream(
      "date;value;id;description\r\n15/09/2026;-123,45;fit-1;PIX PADARIA\r\n16/09/2026;200,00;fit-2;SALARIO\r\n");

    List<ImportCandidate> candidates = await parser.ParseAsync(stream);

    Assert.Equal(2, candidates.Count);
    Assert.Equal(123.45m, candidates[0].Value);
    Assert.False(candidates[0].Income);
    Assert.Equal(200m, candidates[1].Value);
    Assert.True(candidates[1].Income);
  }

  /// <summary>
  /// Nubank invoice imports treat negative BRL as refund/credit.
  /// </summary>
  [Fact]
  public async Task CardInvoiceParserTreatsNegativeBrlAsCredit()
  {
    CardInvoiceNubankCsvParser parser = new CardInvoiceNubankCsvParser();
    await using MemoryStream stream = CreateStream(
      "Data de Compra;Nome no Cartão;Final do Cartão;Categoria;Descrição;Parcela;Valor (em US$);Cotação (em R$);Valor (em R$)\r\n" +
      "15/09/2026;JOHN;1234;Serviços;Estorno Loja;;0;0;-50,00\r\n" +
      "16/09/2026;JOHN;1234;Serviços;Compra Loja;;0;0;75,00\r\n");

    List<ImportCandidate> candidates = await parser.ParseAsync(stream, 1, "1234");

    Assert.True(candidates[0].Income);
    Assert.Equal(50m, candidates[0].Value);
    Assert.False(candidates[1].Income);
    Assert.Equal(75m, candidates[1].Value);
  }

  /// <summary>
  /// Duplicate checks are scoped to the target account.
  /// </summary>
  [Fact]
  public async Task ImportDuplicateDetectionIsTargetAccountScoped()
  {
    FakeTransactionRepository transactionRepository = new FakeTransactionRepository(new[]
    {
      new TransactionEntity
      {
        AccountId = 2,
        Date = new DateTime(2026, 9, 15),
        Value = 123.45m,
        Income = false,
        Description = "PIX PADARIA",
        ExternalId = TransactionImportHasher.HashGuid("fit-1"),
      },
    });
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    TransactionImportService service = new TransactionImportService(
      transactionRepository,
      new FakeAccountRepository(new[] { new AccountEntity { Id = 1, Name = "Main" } }),
      TestServices.CreateFinancialSyncService(factory));
    await using MemoryStream stream = CreateStream("date;value;id;description\r\n15/09/2026;-123,45;fit-1;PIX PADARIA\r\n");

    ImportResult result = await service.ImportAsync(new ImportRequest
    {
      AccountId = 1,
      SourceType = ImportSourceType.BankStatement,
      FileFormat = ImportFileFormat.Csv,
      FileStream = stream,
    });

    Assert.Equal(1, result.Inserted);
    Assert.Equal(0, result.PendingCount);
    Assert.Equal(2, transactionRepository.Transactions.Count);
  }

  private static MemoryStream CreateStream(string text)
  {
    return new MemoryStream(Encoding.UTF8.GetBytes(text));
  }
}
