//-----------------------------------------------------------------------
// <copyright file="20260729164251_AddPlanningActions.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningActionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Manual = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<string>(type: "text", nullable: false),
                    CompletedAtUtc = table.Column<string>(type: "text", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    OpenActionCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningActionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanningActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IconCss = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DetailKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DestinationHref = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PrimaryActionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GeneratedAtUtc = table.Column<string>(type: "text", nullable: false),
                    ResolvedAtUtc = table.Column<string>(type: "text", nullable: true),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningActionRuns_OwnerUserId_ReferenceDate_Manual",
                table: "PlanningActionRuns",
                columns: new[] { "OwnerUserId", "ReferenceDate", "Manual" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningActions_OwnerUserId_SourceCode_ReferenceDate",
                table: "PlanningActions",
                columns: new[] { "OwnerUserId", "SourceCode", "ReferenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningActions_OwnerUserId_Status_Priority",
                table: "PlanningActions",
                columns: new[] { "OwnerUserId", "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningActionRuns");

            migrationBuilder.DropTable(
                name: "PlanningActions");
        }
    }
}
