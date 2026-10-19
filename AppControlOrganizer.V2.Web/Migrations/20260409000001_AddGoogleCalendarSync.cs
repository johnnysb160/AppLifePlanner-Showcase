//-----------------------------------------------------------------------
// <copyright file="20260409000001_AddGoogleCalendarSync.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using AppControlOrganizer.V2.Web.Data;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Infrastructure;
  using Microsoft.EntityFrameworkCore.Migrations;
  using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

  /// <inheritdoc />
  [DbContext(typeof(LifePlannerContext))]
  [Migration("20260409000001_AddGoogleCalendarSync")]
  public partial class AddGoogleCalendarSync : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // ── GoogleCalendarIntegrations ──────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "GoogleCalendarIntegrations",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          GoogleAccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
          AccessToken = table.Column<string>(type: "text", nullable: false),
          RefreshToken = table.Column<string>(type: "text", nullable: false),
          TokenExpiryUtc = table.Column<string>(type: "text", nullable: true),
          TargetCalendarId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
          TargetCalendarName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
          IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
          LastSyncAtUtc = table.Column<string>(type: "text", nullable: true),
          LastSyncStatus = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
          SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
          Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_GoogleCalendarIntegrations", x => x.Id);
        });

      migrationBuilder.CreateIndex(
        name: "IX_GoogleCalendarIntegrations_OwnerUserId",
        table: "GoogleCalendarIntegrations",
        column: "OwnerUserId",
        unique: true);

      // ── GoogleCalendarEventMappings ────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "GoogleCalendarEventMappings",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          LocalEventId = table.Column<int>(type: "integer", nullable: false),
          GoogleEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
          GoogleCalendarId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
          LastSyncedAtUtc = table.Column<string>(type: "text", nullable: false),
          LocalEventUpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
          Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_GoogleCalendarEventMappings", x => x.Id);
          table.ForeignKey(
            name: "FK_GoogleCalendarEventMappings_CalendarEvents_LocalEventId",
            column: x => x.LocalEventId,
            principalTable: "CalendarEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        });

      migrationBuilder.CreateIndex(
        name: "IX_GoogleCalendarEventMappings_OwnerUserId_LocalEventId",
        table: "GoogleCalendarEventMappings",
        columns: new[] { "OwnerUserId", "LocalEventId" },
        unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(name: "GoogleCalendarEventMappings");
      migrationBuilder.DropTable(name: "GoogleCalendarIntegrations");
    }
  }
}
