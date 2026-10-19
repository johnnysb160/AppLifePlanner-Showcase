//-----------------------------------------------------------------------
// <copyright file="20260407000000_AddUserProfileFields.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddUserProfileFields : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "DisplayName",
        table: "AspNetUsers",
        type: "character varying(100)",
        maxLength: 100,
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "ProfilePhotoPath",
        table: "AspNetUsers",
        type: "character varying(500)",
        maxLength: 500,
        nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
        name: "DisplayName",
        table: "AspNetUsers");

      migrationBuilder.DropColumn(
        name: "ProfilePhotoPath",
        table: "AspNetUsers");
    }
  }
}
