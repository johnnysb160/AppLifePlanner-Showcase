using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns idempotently — the bank may already have these from prior migrations.
            migrationBuilder.Sql(@"
                ALTER TABLE ""MarketQuotes"" ADD COLUMN IF NOT EXISTS ""Symbol"" character varying(32);
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""AccountId"" integer;
                ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""Balance"" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""TotalInvested"" numeric(18,2) NOT NULL DEFAULT 0;
            ");

            // AlterColumn operations — these are safe to re-run as they only change defaults/constraints.
            migrationBuilder.AlterColumn<int>(
                name: "SyncIntervalMinutes",
                table: "GoogleCalendarIntegrations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 15);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Deleted",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Deleted",
                table: "GoogleCalendarEventMappings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "GoogleCalendarEventMappings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldDefaultValue: "en");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // Create indexes idempotently.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Risks_PillarId"" ON ""Risks"" (""PillarId"");
                CREATE INDEX IF NOT EXISTS ""IX_ReviewRuns_TemplateId"" ON ""ReviewRuns"" (""TemplateId"");
                CREATE INDEX IF NOT EXISTS ""IX_Objectives_PillarId"" ON ""Objectives"" (""PillarId"");
                CREATE INDEX IF NOT EXISTS ""IX_MarketQuotes_AllocationType_Symbol_RetrievedAtUtc"" ON ""MarketQuotes"" (""AllocationType"" ASC, ""Symbol"" ASC, ""RetrievedAtUtc"" DESC);
                CREATE INDEX IF NOT EXISTS ""IX_KeyResults_ObjectiveId"" ON ""KeyResults"" (""ObjectiveId"");
                CREATE INDEX IF NOT EXISTS ""IX_Habits_PillarId"" ON ""Habits"" (""PillarId"");
                CREATE INDEX IF NOT EXISTS ""IX_HabitLogs_OwnerUserId_LogDate"" ON ""HabitLogs"" (""OwnerUserId"", ""LogDate"");
                CREATE INDEX IF NOT EXISTS ""IX_GoogleCalendarEventMappings_LocalEventId"" ON ""GoogleCalendarEventMappings"" (""LocalEventId"");
                CREATE INDEX IF NOT EXISTS ""IX_Appointments_AccountId"" ON ""Appointments"" (""AccountId"");
            ");

            // Add FK only if it does not already exist.
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Appointments_Accounts_AccountId'
                    ) THEN
                        ALTER TABLE ""Appointments""
                            ADD CONSTRAINT ""FK_Appointments_Accounts_AccountId""
                            FOREIGN KEY (""AccountId"")
                            REFERENCES ""Accounts"" (""Id"")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Accounts_AccountId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Risks_PillarId",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_ReviewRuns_TemplateId",
                table: "ReviewRuns");

            migrationBuilder.DropIndex(
                name: "IX_Objectives_PillarId",
                table: "Objectives");

            migrationBuilder.DropIndex(
                name: "IX_MarketQuotes_AllocationType_Symbol_RetrievedAtUtc",
                table: "MarketQuotes");

            migrationBuilder.DropIndex(
                name: "IX_KeyResults_ObjectiveId",
                table: "KeyResults");

            migrationBuilder.DropIndex(
                name: "IX_Habits_PillarId",
                table: "Habits");

            migrationBuilder.DropIndex(
                name: "IX_HabitLogs_OwnerUserId_LogDate",
                table: "HabitLogs");

            migrationBuilder.DropIndex(
                name: "IX_GoogleCalendarEventMappings_LocalEventId",
                table: "GoogleCalendarEventMappings");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_AccountId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "MarketQuotes");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TotalInvested",
                table: "Accounts");

            migrationBuilder.AlterColumn<int>(
                name: "SyncIntervalMinutes",
                table: "GoogleCalendarIntegrations",
                type: "integer",
                nullable: false,
                defaultValue: 15,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "Deleted",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "GoogleCalendarIntegrations",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "Deleted",
                table: "GoogleCalendarEventMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "GoogleCalendarEventMappings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "AspNetUsers",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "en",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Recreate legacy index only if it does not already exist (idempotent rollback).
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MarketQuotes_AllocationType_RetrievedAtUtc""
                ON ""MarketQuotes"" (""AllocationType"" ASC, ""RetrievedAtUtc"" DESC);
            ");
        }
    }
}
