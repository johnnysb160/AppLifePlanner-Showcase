//-----------------------------------------------------------------------
// <copyright file="20260824154952_AddVehicleOilChanged.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleOilChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OilChanged",
                table: "VehicleRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OilChanged",
                table: "VehicleRecords");
        }
    }
}
