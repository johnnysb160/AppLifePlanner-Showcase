//-----------------------------------------------------------------------
// <copyright file="20260409000002_AddHabitFrequencyAndCalendarLink.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using AppControlOrganizer.V2.Web.Data;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Infrastructure;
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <summary>
  /// Adds two columns to the Habits table:
  ///   FrequencyDays        — comma-separated DayOfWeek integers (e.g. "1,3,5" = Mon/Wed/Fri)
  ///   LinkedCalendarEventId — FK to the auto-generated CalendarEvents series row (nullable)
  /// </summary>
  [DbContext(typeof(LifePlannerContext))]
  [Migration("20260409000002_AddHabitFrequencyAndCalendarLink")]
  public partial class AddHabitFrequencyAndCalendarLink : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "FrequencyDays",
        table: "Habits",
        type: "character varying(20)",
        maxLength: 20,
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "LinkedCalendarEventId",
        table: "Habits",
        type: "integer",
        nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "FrequencyDays", table: "Habits");
      migrationBuilder.DropColumn(name: "LinkedCalendarEventId", table: "Habits");
    }
  }
}
