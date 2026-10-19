//-----------------------------------------------------------------------
// <copyright file="20260407000001_ReplaceProfilePhotoPathWithData.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class ReplaceProfilePhotoPathWithData : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
        name: "ProfilePhotoPath",
        table: "AspNetUsers");

      migrationBuilder.AddColumn<string>(
        name: "ProfilePhotoData",
        table: "AspNetUsers",
        type: "text",
        nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
        name: "ProfilePhotoData",
        table: "AspNetUsers");

      migrationBuilder.AddColumn<string>(
        name: "ProfilePhotoPath",
        table: "AspNetUsers",
        type: "character varying(500)",
        maxLength: 500,
        nullable: true);
    }
  }
}
