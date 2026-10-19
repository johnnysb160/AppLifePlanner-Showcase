using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddImapSyncInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "ImapSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalHours",
                table: "ImapSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "ImapSettings");

            migrationBuilder.DropColumn(
                name: "SyncIntervalHours",
                table: "ImapSettings");
        }
    }
}
