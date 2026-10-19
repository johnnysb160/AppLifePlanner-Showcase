//-----------------------------------------------------------------------
// <copyright file="20260408000002_AddUserLanguage.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddUserLanguage : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "Language",
        table: "AspNetUsers",
        type: "character varying(5)",
        maxLength: 5,
        nullable: false,
        defaultValue: "en");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
        name: "Language",
        table: "AspNetUsers");
    }
  }
}
