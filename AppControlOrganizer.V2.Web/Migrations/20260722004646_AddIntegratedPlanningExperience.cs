//-----------------------------------------------------------------------
// <copyright file="20260722004646_AddIntegratedPlanningExperience.cs" company="AppLifePlanner">
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
    public partial class AddIntegratedPlanningExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditCardId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlanningClassificationConfirmed",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlanningClassificationSource",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlanningImpact",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceKind",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AnnualPlanId",
                table: "QuarterPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveId",
                table: "QuarterPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnnualPlanId",
                table: "Objectives",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricSource",
                table: "KeyResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "America/Sao_Paulo");

            migrationBuilder.AddColumn<int>(
                name: "PlanningPurpose",
                table: "Allocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BalanceMode",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                table: "Accounts",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpeningBalanceDate",
                table: "Accounts",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnualPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    TargetNetWorth = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlannedMonthlyIncome = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MonthlySpendingCeiling = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PlannedAnnualExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlannedMonthlyContribution = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PlannedOneOffNetFlows = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EmergencyReserveMonths = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CalculatedAtUtc = table.Column<string>(type: "text", nullable: false),
                    NetWorth = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConfirmedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OperatingCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EmergencyReserve = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IdleCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LongTermInvestments = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthlyOperatingIncome = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthlyOperatingExpense = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthlyContributions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnpricedPositionCount = table.Column<int>(type: "integer", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutineTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActiveTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyFocuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ObjectiveId = table.Column<int>(type: "integer", nullable: true),
                    KeyResultId = table.Column<int>(type: "integer", nullable: true),
                    QuarterTaskId = table.Column<int>(type: "integer", nullable: true),
                    RoutineBlockId = table.Column<int>(type: "integer", nullable: true),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyFocuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanningReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReviewType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProgressNotes = table.Column<string>(type: "text", nullable: false),
                    BlockerNotes = table.Column<string>(type: "text", nullable: false),
                    DecisionNotes = table.Column<string>(type: "text", nullable: false),
                    FinancialSnapshotId = table.Column<int>(type: "integer", nullable: true),
                    CompletedAtUtc = table.Column<string>(type: "text", nullable: true),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningReviews_FinancialSnapshots_FinancialSnapshotId",
                        column: x => x.FinancialSnapshotId,
                        principalTable: "FinancialSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RoutineBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RoutineTemplateId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    IconCss = table.Column<string>(type: "text", nullable: false),
                    ColorHex = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutineBlocks_RoutineTemplates_RoutineTemplateId",
                        column: x => x.RoutineTemplateId,
                        principalTable: "RoutineTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreditCardId",
                table: "Transactions",
                column: "CreditCardId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarterPlans_AnnualPlanId",
                table: "QuarterPlans",
                column: "AnnualPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarterPlans_ObjectiveId",
                table: "QuarterPlans",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_AnnualPlanId",
                table: "Objectives",
                column: "AnnualPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPlans_OwnerUserId_Year",
                table: "AnnualPlans",
                columns: new[] { "OwnerUserId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialSnapshots_OwnerUserId_SnapshotDate",
                table: "FinancialSnapshots",
                columns: new[] { "OwnerUserId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningReviews_FinancialSnapshotId",
                table: "PlanningReviews",
                column: "FinancialSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningReviews_OwnerUserId_ReviewType_ReviewDate",
                table: "PlanningReviews",
                columns: new[] { "OwnerUserId", "ReviewType", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBlocks_OwnerUserId_DayOfWeek_StartTime",
                table: "RoutineBlocks",
                columns: new[] { "OwnerUserId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBlocks_RoutineTemplateId",
                table: "RoutineBlocks",
                column: "RoutineTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutineTemplates_OwnerUserId_IsActiveTemplate",
                table: "RoutineTemplates",
                columns: new[] { "OwnerUserId", "IsActiveTemplate" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyFocuses_OwnerUserId_WeekStartDate_SortOrder",
                table: "WeeklyFocuses",
                columns: new[] { "OwnerUserId", "WeekStartDate", "SortOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Objectives_AnnualPlans_AnnualPlanId",
                table: "Objectives",
                column: "AnnualPlanId",
                principalTable: "AnnualPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_QuarterPlans_AnnualPlans_AnnualPlanId",
                table: "QuarterPlans",
                column: "AnnualPlanId",
                principalTable: "AnnualPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_QuarterPlans_Objectives_ObjectiveId",
                table: "QuarterPlans",
                column: "ObjectiveId",
                principalTable: "Objectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_CreditCards_CreditCardId",
                table: "Transactions",
                column: "CreditCardId",
                principalTable: "CreditCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Objectives_AnnualPlans_AnnualPlanId",
                table: "Objectives");

            migrationBuilder.DropForeignKey(
                name: "FK_QuarterPlans_AnnualPlans_AnnualPlanId",
                table: "QuarterPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_QuarterPlans_Objectives_ObjectiveId",
                table: "QuarterPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_CreditCards_CreditCardId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "AnnualPlans");

            migrationBuilder.DropTable(
                name: "PlanningReviews");

            migrationBuilder.DropTable(
                name: "RoutineBlocks");

            migrationBuilder.DropTable(
                name: "WeeklyFocuses");

            migrationBuilder.DropTable(
                name: "FinancialSnapshots");

            migrationBuilder.DropTable(
                name: "RoutineTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreditCardId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_QuarterPlans_AnnualPlanId",
                table: "QuarterPlans");

            migrationBuilder.DropIndex(
                name: "IX_QuarterPlans_ObjectiveId",
                table: "QuarterPlans");

            migrationBuilder.DropIndex(
                name: "IX_Objectives_AnnualPlanId",
                table: "Objectives");

            migrationBuilder.DropColumn(
                name: "CreditCardId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlanningClassificationConfirmed",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlanningClassificationSource",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlanningImpact",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AnnualPlanId",
                table: "QuarterPlans");

            migrationBuilder.DropColumn(
                name: "ObjectiveId",
                table: "QuarterPlans");

            migrationBuilder.DropColumn(
                name: "AnnualPlanId",
                table: "Objectives");

            migrationBuilder.DropColumn(
                name: "MetricSource",
                table: "KeyResults");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PlanningPurpose",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "BalanceMode",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceDate",
                table: "Accounts");
        }
    }
}
