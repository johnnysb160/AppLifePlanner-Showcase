using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppointmentsToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the new Transactions table with the exact same structure as Appointments
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: true),
                    Income = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RefId = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Category",
                table: "Transactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OwnerUserId",
                table: "Transactions",
                column: "OwnerUserId");

            // Copy all existing data from Appointments to Transactions
            // The identity sequence must be set after insert to avoid conflicts
            migrationBuilder.Sql(@"
INSERT INTO ""Transactions"" (
    ""Id"",
    ""AccountId"",
    ""Active"",
    ""Category"",
    ""CategoryId"",
    ""CreationDate"",
    ""Date"",
    ""Deleted"",
    ""Description"",
    ""ExternalId"",
    ""Income"",
    ""OwnerUserId"",
    ""RefId"",
    ""Value""
)
SELECT
    ""Id"",
    ""AccountId"",
    ""Active"",
    ""Category"",
    ""CategoryId"",
    ""CreationDate"",
    ""Date"",
    ""Deleted"",
    ""Description"",
    ""ExternalId"",
    ""Income"",
    ""OwnerUserId"",
    ""RefId"",
    ""Value""
FROM ""Appointments"";
");

            // Reset the identity sequence so new rows don't collide with copied rows
            migrationBuilder.Sql(@"
SELECT setval(
    pg_get_serial_sequence('""Transactions""', 'Id'),
    COALESCE((SELECT MAX(""Id"") FROM ""Transactions""), 0) + 1,
    false
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Transactions");
        }
    }
}
