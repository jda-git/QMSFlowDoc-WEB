using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementIsoControlArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "Risks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Risks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                table: "Risks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "Risks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "QualityIndicators",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "QualityIndicators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                table: "QualityIndicators",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "QualityIndicators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "ManagementReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ManagementReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                table: "ManagementReviews",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "AuditPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "AuditPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                table: "AuditPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditorIndependenceStatement",
                table: "AuditPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalAuditors",
                table: "AuditPlans",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgramYear",
                table: "AuditPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "AuditFindings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Risks_EvidenceDocumentId",
                table: "Risks",
                column: "EvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIndicators_EvidenceDocumentId",
                table: "QualityIndicators",
                column: "EvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_EvidenceDocumentId",
                table: "AuditFindings",
                column: "EvidenceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditFindings_Documents_EvidenceDocumentId",
                table: "AuditFindings",
                column: "EvidenceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_QualityIndicators_Documents_EvidenceDocumentId",
                table: "QualityIndicators",
                column: "EvidenceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Documents_EvidenceDocumentId",
                table: "Risks",
                column: "EvidenceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.DropForeignKey(
                name: "FK_AuditFindings_Documents_EvidenceDocumentId",
                table: "AuditFindings");

            migrationBuilder.DropForeignKey(
                name: "FK_QualityIndicators_Documents_EvidenceDocumentId",
                table: "QualityIndicators");

            migrationBuilder.DropForeignKey(
                name: "FK_Risks_Documents_EvidenceDocumentId",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_Risks_EvidenceDocumentId",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_QualityIndicators_EvidenceDocumentId",
                table: "QualityIndicators");

            migrationBuilder.DropIndex(
                name: "IX_AuditFindings_EvidenceDocumentId",
                table: "AuditFindings");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "ApprovedByName",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "QualityIndicators");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "QualityIndicators");

            migrationBuilder.DropColumn(
                name: "ApprovedByName",
                table: "QualityIndicators");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "QualityIndicators");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "ManagementReviews");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ManagementReviews");

            migrationBuilder.DropColumn(
                name: "ApprovedByName",
                table: "ManagementReviews");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "ApprovedByName",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "AuditorIndependenceStatement",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "InternalAuditors",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "ProgramYear",
                table: "AuditPlans");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "AuditFindings");
        }
    }
}
