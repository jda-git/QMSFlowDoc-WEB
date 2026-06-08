using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorEffectivenessReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("Sqlite", System.StringComparison.OrdinalIgnoreCase))
            {
                // Columns are ensured by DbInitializer for SQLite compatibility.
                return;
            }

            migrationBuilder.AddColumn<string>(
                name: "EffectivenessReview",
                table: "QualityIndicators",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectivenessReviewDate",
                table: "QualityIndicators",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("Sqlite", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.DropColumn(
                name: "EffectivenessReview",
                table: "QualityIndicators");

            migrationBuilder.DropColumn(
                name: "EffectivenessReviewDate",
                table: "QualityIndicators");
        }
    }
}
