//-----------------------------------------------------------------------
// <copyright file="20260408000003_AddSymbolToMarketQuotes.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddSymbolToMarketQuotes : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Add nullable Symbol column to MarketQuotes table.
      // Existing rows get Symbol = NULL which is correct — they are hardcoded
      // specific-type entries (BTC, IBM, SPY…) where AllocationType alone is unique.
      migrationBuilder.AddColumn<string>(
        name: "Symbol",
        table: "MarketQuotes",
        type: "character varying(32)",
        maxLength: 32,
        nullable: true);

      // Drop old index on (AllocationType, RetrievedAtUtc)
      migrationBuilder.DropIndex(
        name: "IX_MarketQuotes_AllocationType_RetrievedAtUtc",
        table: "MarketQuotes");

      // New composite index: (AllocationType, Symbol, RetrievedAtUtc DESC)
      migrationBuilder.CreateIndex(
        name: "IX_MarketQuotes_AllocationType_Symbol_RetrievedAtUtc",
        table: "MarketQuotes",
        columns: new[] { "AllocationType", "Symbol", "RetrievedAtUtc" },
        descending: new[] { false, false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
        name: "IX_MarketQuotes_AllocationType_Symbol_RetrievedAtUtc",
        table: "MarketQuotes");

      migrationBuilder.DropColumn(
        name: "Symbol",
        table: "MarketQuotes");

      migrationBuilder.CreateIndex(
        name: "IX_MarketQuotes_AllocationType_RetrievedAtUtc",
        table: "MarketQuotes",
        columns: new[] { "AllocationType", "RetrievedAtUtc" },
        descending: new[] { false, true });
    }
  }
}
