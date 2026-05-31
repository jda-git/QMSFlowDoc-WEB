using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEqaSecurityAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EQARounds_ProgramId",
                table: "EQARounds");

            migrationBuilder.DropIndex(
                name: "IX_EQAMappings_ProgramId",
                table: "EQAMappings");

            migrationBuilder.DropIndex(
                name: "IX_EQAEnrollments_ProgramId",
                table: "EQAEnrollments");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EQARounds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EQARounds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EQARounds",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EQAPrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EQAMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EQAMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EQAMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EQAEnrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EQAEnrollments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EQAEnrollments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EQADeviations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EQADeviations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EQADeviations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EQARounds_ProgramId_Year_RoundNumber",
                table: "EQARounds",
                columns: new[] { "ProgramId", "Year", "RoundNumber" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EQAPrograms_InternalCode",
                table: "EQAPrograms",
                column: "InternalCode",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EQAMappings_ProgramId_InternalTestName",
                table: "EQAMappings",
                columns: new[] { "ProgramId", "InternalTestName" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EQAEnrollments_ProgramId_Year",
                table: "EQAEnrollments",
                columns: new[] { "ProgramId", "Year" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EQARounds_ProgramId_Year_RoundNumber",
                table: "EQARounds");

            migrationBuilder.DropIndex(
                name: "IX_EQAPrograms_InternalCode",
                table: "EQAPrograms");

            migrationBuilder.DropIndex(
                name: "IX_EQAMappings_ProgramId_InternalTestName",
                table: "EQAMappings");

            migrationBuilder.DropIndex(
                name: "IX_EQAEnrollments_ProgramId_Year",
                table: "EQAEnrollments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EQARounds");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EQARounds");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EQARounds");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EQAMappings");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EQAMappings");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EQAMappings");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EQAEnrollments");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EQAEnrollments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EQAEnrollments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EQADeviations");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EQADeviations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EQADeviations");

            migrationBuilder.CreateIndex(
                name: "IX_EQARounds_ProgramId",
                table: "EQARounds",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EQAMappings_ProgramId",
                table: "EQAMappings",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EQAEnrollments_ProgramId",
                table: "EQAEnrollments",
                column: "ProgramId");
        }
    }
}
