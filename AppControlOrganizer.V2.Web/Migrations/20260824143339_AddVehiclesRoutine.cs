//-----------------------------------------------------------------------
// <copyright file="20260824143339_AddVehiclesRoutine.cs" company="AppLifePlanner">
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
    public partial class AddVehiclesRoutine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Brand = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Plate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CurrentOdometer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OdometerUnit = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IconCss = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    RecordType = table.Column<int>(type: "integer", nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Odometer = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleRecords_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    CompletedByRecordId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueOdometer = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleReminders_VehicleRecords_CompletedByRecordId",
                        column: x => x.CompletedByRecordId,
                        principalTable: "VehicleRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleReminders_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleRecords_OwnerUserId_VehicleId_RecordDate",
                table: "VehicleRecords",
                columns: new[] { "OwnerUserId", "VehicleId", "RecordDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleRecords_OwnerUserId_VehicleId_RecordType",
                table: "VehicleRecords",
                columns: new[] { "OwnerUserId", "VehicleId", "RecordType" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleRecords_VehicleId",
                table: "VehicleRecords",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleReminders_CompletedByRecordId",
                table: "VehicleReminders",
                column: "CompletedByRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleReminders_OwnerUserId_DueDate",
                table: "VehicleReminders",
                columns: new[] { "OwnerUserId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleReminders_OwnerUserId_DueOdometer",
                table: "VehicleReminders",
                columns: new[] { "OwnerUserId", "DueOdometer" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleReminders_OwnerUserId_VehicleId_Status",
                table: "VehicleReminders",
                columns: new[] { "OwnerUserId", "VehicleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleReminders_VehicleId",
                table: "VehicleReminders",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerUserId_Active",
                table: "Vehicles",
                columns: new[] { "OwnerUserId", "Active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleReminders");

            migrationBuilder.DropTable(
                name: "VehicleRecords");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
