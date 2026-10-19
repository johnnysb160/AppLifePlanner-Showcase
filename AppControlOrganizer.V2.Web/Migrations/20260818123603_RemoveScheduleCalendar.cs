//-----------------------------------------------------------------------
// <copyright file="20260818123603_RemoveScheduleCalendar.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScheduleCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleCalendarEventMappings");

            migrationBuilder.DropTable(
                name: "GoogleCalendarIntegrations");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "OccupationTypes");

            migrationBuilder.DropColumn(
                name: "CalendarEventId",
                table: "ReviewRuns");

            migrationBuilder.DropColumn(
                name: "LinkedCalendarEventId",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "CalendarEventId",
                table: "HabitLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalendarEventId",
                table: "ReviewRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedCalendarEventId",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalendarEventId",
                table: "HabitLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoogleCalendarIntegrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    GoogleAccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAtUtc = table.Column<string>(type: "text", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    TargetCalendarId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TargetCalendarName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenExpiryUtc = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarIntegrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OccupationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: false),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    IconCss = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccupationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccupationTypeId = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    EndUtc = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    HabitId = table.Column<int>(type: "integer", nullable: true),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecurrenceException = table.Column<bool>(type: "boolean", nullable: false),
                    KeyResultId = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDay = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    MonthlyPatternType = table.Column<int>(type: "integer", nullable: false),
                    MonthlyWeekNumber = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ObjectiveId = table.Column<int>(type: "integer", nullable: true),
                    OriginalStartUtc = table.Column<string>(type: "text", nullable: true),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PillarId = table.Column<int>(type: "integer", nullable: true),
                    PlanningLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecurrenceDays = table.Column<string>(type: "text", nullable: true),
                    RecurrenceEndUtc = table.Column<string>(type: "text", nullable: true),
                    RecurrenceType = table.Column<int>(type: "integer", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ReviewType = table.Column<int>(type: "integer", nullable: true),
                    SeriesMasterId = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_OccupationTypes_OccupationTypeId",
                        column: x => x.OccupationTypeId,
                        principalTable: "OccupationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoogleCalendarEventMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocalEventId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    GoogleCalendarId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    GoogleEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    LastSyncedAtUtc = table.Column<string>(type: "text", nullable: false),
                    LocalEventUpdatedAt = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false)
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
                name: "IX_CalendarEvents_OccupationTypeId",
                table: "CalendarEvents",
                column: "OccupationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                table: "CalendarEvents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OwnerUserId_StartUtc",
                table: "CalendarEvents",
                columns: new[] { "OwnerUserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SeriesMasterId",
                table: "CalendarEvents",
                column: "SeriesMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarEventMappings_LocalEventId",
                table: "GoogleCalendarEventMappings",
                column: "LocalEventId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarEventMappings_OwnerUserId_LocalEventId",
                table: "GoogleCalendarEventMappings",
                columns: new[] { "OwnerUserId", "LocalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarIntegrations_OwnerUserId",
                table: "GoogleCalendarIntegrations",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OccupationTypes_OwnerUserId",
                table: "OccupationTypes",
                column: "OwnerUserId");
        }
    }
}
