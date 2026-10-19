//-----------------------------------------------------------------------
// <copyright file="20260407000002_AddCalendarEntities.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OccupationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    IconCss = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false)
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
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OccupationTypeId = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<string>(type: "text", nullable: false),
                    EndUtc = table.Column<string>(type: "text", nullable: false),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RecurrenceType = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceDays = table.Column<string>(type: "text", nullable: true),
                    MonthlyPatternType = table.Column<int>(type: "integer", nullable: false),
                    MonthlyDay = table.Column<int>(type: "integer", nullable: true),
                    MonthlyWeekNumber = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceEndUtc = table.Column<string>(type: "text", nullable: true),
                    SeriesMasterId = table.Column<int>(type: "integer", nullable: true),
                    IsRecurrenceException = table.Column<bool>(type: "boolean", nullable: false),
                    OriginalStartUtc = table.Column<string>(type: "text", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_OccupationTypes_OwnerUserId",
                table: "OccupationTypes",
                column: "OwnerUserId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CalendarEvents");
            migrationBuilder.DropTable(name: "OccupationTypes");
        }
    }
}
