//-----------------------------------------------------------------------
// <copyright file="20260920164500_AddTransactionTimeAndSourceDetails.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
  /// <summary>
  /// Adds the optional time of day and original source details to transactions. Both columns are nullable,
  /// so existing rows and older readers are unaffected.
  /// </summary>
  public partial class AddTransactionTimeAndSourceDetails : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "SourceDetails",
        table: "Transactions",
        type: "text",
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "TransactionTime",
        table: "Transactions",
        type: "text",
        nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
        name: "SourceDetails",
        table: "Transactions");

      migrationBuilder.DropColumn(
        name: "TransactionTime",
        table: "Transactions");
    }
  }
}
