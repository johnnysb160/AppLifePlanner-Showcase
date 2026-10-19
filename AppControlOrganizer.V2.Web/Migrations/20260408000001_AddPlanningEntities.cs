//-----------------------------------------------------------------------
// <copyright file="20260408000001_AddPlanningEntities.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;
  using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

  /// <inheritdoc />
  public partial class AddPlanningEntities : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // ── CalendarEvents — new planning integration columns ──────────────
      migrationBuilder.AddColumn<int>(
        name: "PillarId",
        table: "CalendarEvents",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "ObjectiveId",
        table: "CalendarEvents",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "KeyResultId",
        table: "CalendarEvents",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "HabitId",
        table: "CalendarEvents",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "ReviewType",
        table: "CalendarEvents",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "PlanningLabel",
        table: "CalendarEvents",
        type: "character varying(200)",
        maxLength: 200,
        nullable: true);

      // ── Pillars ────────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "Pillars",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          Name = table.Column<string>(type: "text", nullable: false),
          Description = table.Column<string>(type: "text", nullable: true),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          ColorHex = table.Column<string>(type: "text", nullable: false),
          IconCss = table.Column<string>(type: "text", nullable: true),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_Pillars", x => x.Id));

      migrationBuilder.CreateIndex(name: "IX_Pillars_OwnerUserId", table: "Pillars", column: "OwnerUserId");

      // ── Objectives ─────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "Objectives",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          PillarId = table.Column<int>(type: "integer", nullable: false),
          Title = table.Column<string>(type: "text", nullable: false),
          Description = table.Column<string>(type: "text", nullable: true),
          Deadline = table.Column<string>(type: "text", nullable: true),
          Status = table.Column<int>(type: "integer", nullable: false),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Objectives", x => x.Id);
          table.ForeignKey(name: "FK_Objectives_Pillars_PillarId", column: x => x.PillarId, principalTable: "Pillars", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      migrationBuilder.CreateIndex(name: "IX_Objectives_OwnerUserId_PillarId", table: "Objectives", columns: new[] { "OwnerUserId", "PillarId" });

      // ── KeyResults ─────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "KeyResults",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          ObjectiveId = table.Column<int>(type: "integer", nullable: false),
          Title = table.Column<string>(type: "text", nullable: false),
          MetricUnit = table.Column<string>(type: "text", nullable: true),
          Baseline = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
          Target = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
          Current = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
          WeeklyNote = table.Column<string>(type: "text", nullable: true),
          Deadline = table.Column<string>(type: "text", nullable: true),
          Status = table.Column<int>(type: "integer", nullable: false),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_KeyResults", x => x.Id);
          table.ForeignKey(name: "FK_KeyResults_Objectives_ObjectiveId", column: x => x.ObjectiveId, principalTable: "Objectives", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      migrationBuilder.CreateIndex(name: "IX_KeyResults_OwnerUserId_ObjectiveId", table: "KeyResults", columns: new[] { "OwnerUserId", "ObjectiveId" });

      // ── QuarterPlans ───────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "QuarterPlans",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          Year = table.Column<int>(type: "integer", nullable: false),
          Quarter = table.Column<int>(type: "integer", nullable: false),
          Focus = table.Column<string>(type: "text", nullable: false),
          Delivery = table.Column<string>(type: "text", nullable: true),
          TargetDate = table.Column<string>(type: "text", nullable: true),
          WeeklyCadence = table.Column<string>(type: "text", nullable: true),
          KpiDescription = table.Column<string>(type: "text", nullable: true),
          Status = table.Column<int>(type: "integer", nullable: false),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_QuarterPlans", x => x.Id));

      migrationBuilder.CreateIndex(name: "IX_QuarterPlans_OwnerUserId_Year_Quarter", table: "QuarterPlans", columns: new[] { "OwnerUserId", "Year", "Quarter" });

      // ── QuarterTasks ───────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "QuarterTasks",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          QuarterPlanId = table.Column<int>(type: "integer", nullable: false),
          Text = table.Column<string>(type: "text", nullable: false),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          Done = table.Column<bool>(type: "boolean", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_QuarterTasks", x => x.Id);
          table.ForeignKey(name: "FK_QuarterTasks_QuarterPlans_QuarterPlanId", column: x => x.QuarterPlanId, principalTable: "QuarterPlans", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      migrationBuilder.CreateIndex(name: "IX_QuarterTasks_QuarterPlanId", table: "QuarterTasks", column: "QuarterPlanId");

      // ── ReviewTemplates ────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "ReviewTemplates",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          Name = table.Column<string>(type: "text", nullable: false),
          ReviewType = table.Column<int>(type: "integer", nullable: false),
          IsDefault = table.Column<bool>(type: "boolean", nullable: false),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_ReviewTemplates", x => x.Id));

      migrationBuilder.CreateIndex(name: "IX_ReviewTemplates_OwnerUserId_ReviewType", table: "ReviewTemplates", columns: new[] { "OwnerUserId", "ReviewType" });

      // ── ReviewTemplateItems ────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "ReviewTemplateItems",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          TemplateId = table.Column<int>(type: "integer", nullable: false),
          Text = table.Column<string>(type: "text", nullable: false),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_ReviewTemplateItems", x => x.Id);
          table.ForeignKey(name: "FK_ReviewTemplateItems_ReviewTemplates_TemplateId", column: x => x.TemplateId, principalTable: "ReviewTemplates", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      // ── ReviewRuns ─────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "ReviewRuns",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          TemplateId = table.Column<int>(type: "integer", nullable: false),
          ReviewType = table.Column<int>(type: "integer", nullable: false),
          RunDate = table.Column<string>(type: "text", nullable: false),
          Notes = table.Column<string>(type: "text", nullable: true),
          ScoreEnergy = table.Column<int>(type: "integer", nullable: true),
          ScoreFocus = table.Column<int>(type: "integer", nullable: true),
          ScoreFinances = table.Column<int>(type: "integer", nullable: true),
          ScoreRelations = table.Column<int>(type: "integer", nullable: true),
          CalendarEventId = table.Column<int>(type: "integer", nullable: true),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_ReviewRuns", x => x.Id);
          table.ForeignKey(name: "FK_ReviewRuns_ReviewTemplates_TemplateId", column: x => x.TemplateId, principalTable: "ReviewTemplates", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });

      migrationBuilder.CreateIndex(name: "IX_ReviewRuns_OwnerUserId_RunDate", table: "ReviewRuns", columns: new[] { "OwnerUserId", "RunDate" });
      migrationBuilder.CreateIndex(name: "IX_ReviewRuns_OwnerUserId_ReviewType", table: "ReviewRuns", columns: new[] { "OwnerUserId", "ReviewType" });

      // ── ReviewRunItems ─────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "ReviewRunItems",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          ReviewRunId = table.Column<int>(type: "integer", nullable: false),
          TemplateItemId = table.Column<int>(type: "integer", nullable: true),
          Text = table.Column<string>(type: "text", nullable: false),
          SortOrder = table.Column<int>(type: "integer", nullable: false),
          Done = table.Column<bool>(type: "boolean", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_ReviewRunItems", x => x.Id);
          table.ForeignKey(name: "FK_ReviewRunItems_ReviewRuns_ReviewRunId", column: x => x.ReviewRunId, principalTable: "ReviewRuns", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      // ── Risks ──────────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "Risks",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          Title = table.Column<string>(type: "text", nullable: false),
          Description = table.Column<string>(type: "text", nullable: true),
          Probability = table.Column<int>(type: "integer", nullable: false),
          Impact = table.Column<int>(type: "integer", nullable: false),
          EarlyWarning = table.Column<string>(type: "text", nullable: true),
          Contingency = table.Column<string>(type: "text", nullable: true),
          PlanB = table.Column<string>(type: "text", nullable: true),
          PillarId = table.Column<int>(type: "integer", nullable: true),
          Status = table.Column<int>(type: "integer", nullable: false),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Risks", x => x.Id);
          table.ForeignKey(name: "FK_Risks_Pillars_PillarId", column: x => x.PillarId, principalTable: "Pillars", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        });

      migrationBuilder.CreateIndex(name: "IX_Risks_OwnerUserId", table: "Risks", column: "OwnerUserId");

      // ── Habits ─────────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "Habits",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          Name = table.Column<string>(type: "text", nullable: false),
          Description = table.Column<string>(type: "text", nullable: true),
          ColorHex = table.Column<string>(type: "text", nullable: false),
          IconCss = table.Column<string>(type: "text", nullable: true),
          PillarId = table.Column<int>(type: "integer", nullable: true),
          IsActive = table.Column<bool>(type: "boolean", nullable: false),
          UpdatedAt = table.Column<string>(type: "text", nullable: false),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Habits", x => x.Id);
          table.ForeignKey(name: "FK_Habits_Pillars_PillarId", column: x => x.PillarId, principalTable: "Pillars", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        });

      migrationBuilder.CreateIndex(name: "IX_Habits_OwnerUserId", table: "Habits", column: "OwnerUserId");

      // ── HabitLogs ──────────────────────────────────────────────────────
      migrationBuilder.CreateTable(
        name: "HabitLogs",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
          HabitId = table.Column<int>(type: "integer", nullable: false),
          LogDate = table.Column<string>(type: "text", nullable: false),
          Done = table.Column<bool>(type: "boolean", nullable: false),
          CalendarEventId = table.Column<int>(type: "integer", nullable: true),
          RefId = table.Column<string>(type: "text", nullable: false),
          ExternalId = table.Column<int>(type: "integer", nullable: true),
          CreationDate = table.Column<string>(type: "text", nullable: false),
          Active = table.Column<bool>(type: "boolean", nullable: false),
          Deleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_HabitLogs", x => x.Id);
          table.ForeignKey(name: "FK_HabitLogs_Habits_HabitId", column: x => x.HabitId, principalTable: "Habits", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });

      migrationBuilder.CreateIndex(name: "IX_HabitLogs_OwnerUserId_LogDate", table: "HabitLogs", columns: new[] { "OwnerUserId", "LogDate" });
      migrationBuilder.CreateIndex(name: "IX_HabitLogs_HabitId_LogDate", table: "HabitLogs", columns: new[] { "HabitId", "LogDate" }, unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(name: "HabitLogs");
      migrationBuilder.DropTable(name: "Habits");
      migrationBuilder.DropTable(name: "Risks");
      migrationBuilder.DropTable(name: "ReviewRunItems");
      migrationBuilder.DropTable(name: "ReviewRuns");
      migrationBuilder.DropTable(name: "ReviewTemplateItems");
      migrationBuilder.DropTable(name: "ReviewTemplates");
      migrationBuilder.DropTable(name: "QuarterTasks");
      migrationBuilder.DropTable(name: "QuarterPlans");
      migrationBuilder.DropTable(name: "KeyResults");
      migrationBuilder.DropTable(name: "Objectives");
      migrationBuilder.DropTable(name: "Pillars");

      migrationBuilder.DropColumn(name: "PillarId", table: "CalendarEvents");
      migrationBuilder.DropColumn(name: "ObjectiveId", table: "CalendarEvents");
      migrationBuilder.DropColumn(name: "KeyResultId", table: "CalendarEvents");
      migrationBuilder.DropColumn(name: "HabitId", table: "CalendarEvents");
      migrationBuilder.DropColumn(name: "ReviewType", table: "CalendarEvents");
      migrationBuilder.DropColumn(name: "PlanningLabel", table: "CalendarEvents");
    }
  }
}
