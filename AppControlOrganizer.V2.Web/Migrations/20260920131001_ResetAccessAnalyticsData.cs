//-----------------------------------------------------------------------
// <copyright file="20260920131001_ResetAccessAnalyticsData.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppControlOrganizer.V2.Web.Migrations
{
    /// <summary>
    /// Clears access analytics rows captured before page-load-only capture and visitor-based sessions
    /// existed: they were dominated by widget polling and could not produce session durations.
    /// </summary>
    public partial class ResetAccessAnalyticsData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"AccessEvents\";");
            migrationBuilder.Sql("DELETE FROM \"AccessDailySummaries\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
