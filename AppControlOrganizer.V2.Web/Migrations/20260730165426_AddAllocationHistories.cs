//-----------------------------------------------------------------------
// <copyright file="20260730165426_AddAllocationHistories.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationHistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AllocationId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PlanningPurpose = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<string>(type: "text", nullable: false),
                    PreviousNativeAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    NewNativeAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    DeltaNativeAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    AmountBrl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    AllocationName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationHistories_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationHistories_AllocationId",
                table: "AllocationHistories",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationHistories_OwnerUserId_OccurredAt",
                table: "AllocationHistories",
                columns: new[] { "OwnerUserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationHistories");
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="20260730165426_AddAllocationHistories.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------
