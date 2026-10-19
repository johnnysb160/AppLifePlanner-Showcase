//-----------------------------------------------------------------------
// <copyright file="20260424230243_ResetFinancialSyncFlag.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
  /// <summary>
  /// Removes all financial sync backfill flags so the next startup re-runs the backfill
  /// with the corrected logic (Balance = InvestmentFunds + transactions, TotalInvested = all other allocations).
  /// </summary>
  public partial class ResetFinancialSyncFlag : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.Sql(@"
        DELETE FROM ""AppSettings"" WHERE ""Key"" = 'FinancialSync_BackfillDone';
        DELETE FROM ""AppSettings"" WHERE ""Key"" = 'FinancialSync_BackfillDone_v2';
      ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
  }
}
